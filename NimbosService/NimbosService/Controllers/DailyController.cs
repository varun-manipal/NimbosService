using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NimbosService.Data;
using NimbosService.DTOs;
using NimbosService.Models;

namespace NimbosService.Controllers;

[ApiController]
[Route("daily")]
public class DailyController : ControllerBase
{
    private readonly AppDbContext _db;

    public DailyController(AppDbContext db)
    {
        _db = db;
    }

    // POST /daily/new-day — trigger daily reset
    [HttpPost("new-day")]
    public async Task<IActionResult> NewDay([FromBody] NewDayRequest req)
    {
        var currentUser = (User)HttpContext.Items["CurrentUser"]!;

        if (!DateOnly.TryParse(req.LastOpenedDate, out var lastOpenedDate))
            return BadRequest(new { error = "Invalid date format. Use yyyy-MM-dd" });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var dbUser = await _db.Users.FindAsync(currentUser.Id);
        var dbShield = await _db.Shields.FindAsync(currentUser.Id);
        var allTasks = await _db.Tasks.Where(t => t.UserId == currentUser.Id).ToListAsync();

        if (lastOpenedDate >= today)
        {
            // Not a new day — return current state
            var shield = dbShield ?? new Shield();
            var currentTasks = allTasks.Where(t => !t.IsTomorrowOnly).ToList();
            return Ok(new NewDayResponse(
                WasNewDay: false,
                Snapshot: null,
                Shield: new ShieldDTO(shield.Fragments, shield.IsActive),
                Tasks: currentTasks.Select(UsersController.MapTask).ToList()
            ));
        }

        // Snapshot: calculate completion for the day that just ended
        var regularTasks = allTasks.Where(t => !t.IsTomorrowOnly).ToList();
        var completedCount = regularTasks.Count(t => t.IsCompleted);
        var totalCount = regularTasks.Count;
        var completionPct = totalCount > 0 ? (double)completedCount / totalCount : 0.0;
        var starsLit = dbUser!.DailyStars;

        // Upsert daily snapshot
        var existing = await _db.DailySnapshots.FirstOrDefaultAsync(
            s => s.UserId == currentUser.Id && s.SnapshotDate == lastOpenedDate);

        if (existing is null)
        {
            _db.DailySnapshots.Add(new DailySnapshot
            {
                UserId = currentUser.Id,
                SnapshotDate = lastOpenedDate,
                CompletionPercentage = completionPct,
                StarsLit = starsLit
            });
        }
        else
        {
            existing.CompletionPercentage = completionPct;
            existing.StarsLit = starsLit;
        }

        // Update shield — earn a fragment if completion >= 80%
        if (dbShield is null)
        {
            dbShield = new Shield { UserId = currentUser.Id };
            _db.Shields.Add(dbShield);
        }

        if (completionPct >= 0.8)
        {
            dbShield.Fragments++;
            if (dbShield.Fragments >= 3)
            {
                dbShield.IsActive = true;
                dbShield.Fragments = 0;
            }
        }
        dbShield.UpdatedAt = DateTime.UtcNow;

        // Reset task flags; apply isSkippedTomorrow → isSnoozed
        foreach (var task in regularTasks)
        {
            task.IsCompleted = false;
            task.IsDismissedToday = false;
            if (task.IsSkippedTomorrow)
            {
                task.IsSnoozed = true;
                task.IsSkippedTomorrow = false;
            }
            else
            {
                task.IsSnoozed = false;
            }
            task.LastUpdated = DateTime.UtcNow;
        }

        // Delete tomorrow-only tasks (consumed after dawn)
        _db.Tasks.RemoveRange(allTasks.Where(t => t.IsTomorrowOnly));

        // Reset daily stars on user
        dbUser.DailyStars = 0;
        dbUser.LastOpened = DateTime.UtcNow;
        dbUser.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        var updatedTasks = await _db.Tasks
            .Where(t => t.UserId == currentUser.Id && !t.IsTomorrowOnly)
            .ToListAsync();

        return Ok(new NewDayResponse(
            WasNewDay: true,
            Snapshot: new SnapshotDTO(lastOpenedDate.ToString("yyyy-MM-dd"), completionPct, starsLit),
            Shield: new ShieldDTO(dbShield.Fragments, dbShield.IsActive),
            Tasks: updatedTasks.Select(UsersController.MapTask).ToList()
        ));
    }

    // GET /daily/snapshots?month=2026-03 — fetch calendar month snapshots
    [HttpGet("snapshots")]
    public async Task<IActionResult> GetSnapshots([FromQuery] string month)
    {
        var currentUser = (User)HttpContext.Items["CurrentUser"]!;

        if (!DateOnly.TryParse(month + "-01", out var startDate))
            return BadRequest(new { error = "Invalid month format. Use yyyy-MM" });

        var endDate = startDate.AddMonths(1);

        var snapshots = await _db.DailySnapshots
            .Where(s => s.UserId == currentUser.Id
                     && s.SnapshotDate >= startDate
                     && s.SnapshotDate < endDate)
            .OrderBy(s => s.SnapshotDate)
            .ToListAsync();

        return Ok(snapshots.Select(s => new SnapshotDTO(
            s.SnapshotDate.ToString("yyyy-MM-dd"),
            s.CompletionPercentage,
            s.StarsLit
        )));
    }
}
