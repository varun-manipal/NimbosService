using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NimbosService.Data;
using NimbosService.DTOs;
using NimbosService.Models;

namespace NimbosService.Controllers;

[ApiController]
[Route("users")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public UsersController(AppDbContext db)
    {
        _db = db;
    }

    // POST /users — register on onboarding completion
    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        // If registering via Google or Apple Sign-In, skip device conflict check
        if (req.GoogleId is null && req.AppleId is null && await _db.Users.AnyAsync(u => u.DeviceId == req.DeviceId))
            return Conflict(new { error = "Device already registered" });

        var user = new User
        {
            DeviceId = req.DeviceId,
            Name = req.Name,
            Vibe = req.Vibe,
            ListPin = req.Pin,
            GoogleId = req.GoogleId,
            AppleId = req.AppleId,
            Email = req.Email
        };

        _db.Users.Add(user);
        _db.Shields.Add(new Shield { UserId = user.Id });

        foreach (var title in req.Tasks)
        {
            _db.Tasks.Add(new TaskItem { UserId = user.Id, Title = title });
        }

        await _db.SaveChangesAsync();

        var tasks = await _db.Tasks.Where(t => t.UserId == user.Id).ToListAsync();
        var shield = await _db.Shields.FindAsync(user.Id) ?? new Shield();

        return Ok(new RegisterResponse(
            UserId: user.Id,
            Token: user.Id.ToString(),
            User: BuildUserDTO(user, shield, tasks)
        ));
    }

    // GET /users/me — fetch full state on app launch
    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var currentUser = (User)HttpContext.Items["CurrentUser"]!;

        var user = await _db.Users
            .Include(u => u.Tasks)
            .Include(u => u.Shield)
            .FirstAsync(u => u.Id == currentUser.Id);

        var shield = user.Shield ?? new Shield();
        var tasks = user.Tasks.Where(t => !t.IsTomorrowOnly).ToList();
        var tomorrowExtras = user.Tasks.Where(t => t.IsTomorrowOnly).ToList();

        return Ok(new UserDTO(
            Name: user.Name,
            Vibe: user.Vibe,
            TotalStars: user.TotalStars,
            DailyStars: user.DailyStars,
            Shield: new ShieldDTO(shield.Fragments, shield.IsActive),
            Tasks: tasks.Select(MapTask).ToList(),
            TomorrowExtras: tomorrowExtras.Select(MapTask).ToList()
        ));
    }

    // PATCH /users/me — update name, vibe, or pin
    [HttpPatch("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateUserRequest req)
    {
        var currentUser = (User)HttpContext.Items["CurrentUser"]!;
        var user = await _db.Users.FindAsync(currentUser.Id);

        if (req.Name is not null) user!.Name = req.Name;
        if (req.Vibe is not null) user!.Vibe = req.Vibe;
        if (req.Pin is not null) user!.ListPin = req.Pin;
        user!.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(new { name = user.Name, vibe = user.Vibe });
    }

    internal static TaskDTO MapTask(TaskItem t) => new(
        Id: t.Id,
        Title: t.Title,
        IsCompleted: t.IsCompleted,
        IsSnoozed: t.IsSnoozed,
        IsDismissedToday: t.IsDismissedToday,
        IsSkippedTomorrow: t.IsSkippedTomorrow,
        IsTomorrowOnly: t.IsTomorrowOnly
    );

    private static UserDTO BuildUserDTO(User user, Shield shield, List<TaskItem> allTasks) =>
        new(
            Name: user.Name,
            Vibe: user.Vibe,
            TotalStars: user.TotalStars,
            DailyStars: user.DailyStars,
            Shield: new ShieldDTO(shield.Fragments, shield.IsActive),
            Tasks: allTasks.Where(t => !t.IsTomorrowOnly).Select(MapTask).ToList(),
            TomorrowExtras: allTasks.Where(t => t.IsTomorrowOnly).Select(MapTask).ToList()
        );
}
