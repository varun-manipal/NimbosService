using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NimbosService.Data;
using NimbosService.DTOs;
using NimbosService.Models;

namespace NimbosService.Controllers;

[ApiController]
[Route("tasks")]
public class TasksController : ControllerBase
{
    private readonly AppDbContext _db;

    public TasksController(AppDbContext db)
    {
        _db = db;
    }

    // POST /tasks — add a new recurring habit
    [HttpPost]
    public async Task<IActionResult> CreateTask([FromBody] CreateTaskRequest req)
    {
        var user = (User)HttpContext.Items["CurrentUser"]!;

        var task = new TaskItem { UserId = user.Id, Title = req.Title };
        _db.Tasks.Add(task);
        await _db.SaveChangesAsync();

        return Ok(UsersController.MapTask(task));
    }

    // PATCH /tasks/{id} — toggle complete/snooze/dismiss/skipTomorrow or rename
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateTask(Guid id, [FromBody] UpdateTaskRequest req)
    {
        var user = (User)HttpContext.Items["CurrentUser"]!;
        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == user.Id);

        if (task is null) return NotFound();

        var wasCompleted = task.IsCompleted;

        if (req.IsCompleted is not null) task.IsCompleted = req.IsCompleted.Value;
        if (req.IsSnoozed is not null) task.IsSnoozed = req.IsSnoozed.Value;
        if (req.IsDismissedToday is not null) task.IsDismissedToday = req.IsDismissedToday.Value;
        if (req.IsSkippedTomorrow is not null) task.IsSkippedTomorrow = req.IsSkippedTomorrow.Value;
        if (req.Title is not null) task.Title = req.Title;
        task.LastUpdated = DateTime.UtcNow;

        var dbUser = await _db.Users.FindAsync(user.Id);

        if (dbUser is null) return NotFound(new { error = "User not found" });

        // Recalculate stars server-side on completion toggle
        if (req.IsCompleted is not null)
        {
            if (req.IsCompleted.Value && !wasCompleted)
            {
                dbUser.TotalStars++;
                dbUser.DailyStars++;
            }
            else if (!req.IsCompleted.Value && wasCompleted)
            {
                if (dbUser.TotalStars > 0) dbUser.TotalStars--;
                if (dbUser.DailyStars > 0) dbUser.DailyStars--;
            }
            dbUser.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        return Ok(new UpdateTaskResponse(
            Task: UsersController.MapTask(task),
            User: new UserStarsDTO(dbUser.TotalStars, dbUser.DailyStars)
        ));
    }

    // DELETE /tasks/{id} — remove a habit permanently
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteTask(Guid id)
    {
        var user = (User)HttpContext.Items["CurrentUser"]!;
        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == user.Id);

        if (task is null) return NotFound();

        _db.Tasks.Remove(task);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // POST /tasks/tomorrow-extras — add one-time tomorrow task
    [HttpPost("tomorrow-extras")]
    public async Task<IActionResult> CreateTomorrowExtra([FromBody] CreateTaskRequest req)
    {
        var user = (User)HttpContext.Items["CurrentUser"]!;

        var task = new TaskItem { UserId = user.Id, Title = req.Title, IsTomorrowOnly = true };
        _db.Tasks.Add(task);
        await _db.SaveChangesAsync();

        return Ok(UsersController.MapTask(task));
    }

    // DELETE /tasks/tomorrow-extras/{id} — remove tomorrow-only task
    [HttpDelete("tomorrow-extras/{id:guid}")]
    public async Task<IActionResult> DeleteTomorrowExtra(Guid id)
    {
        var user = (User)HttpContext.Items["CurrentUser"]!;
        var task = await _db.Tasks.FirstOrDefaultAsync(
            t => t.Id == id && t.UserId == user.Id && t.IsTomorrowOnly);

        if (task is null) return NotFound();

        _db.Tasks.Remove(task);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
