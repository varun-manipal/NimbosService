using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NimbosService.Data;
using NimbosService.DTOs;
using NimbosService.Models;

namespace NimbosService.Controllers;

[ApiController]
[Route("awards")]
public class AwardsController : ControllerBase
{
    private readonly AppDbContext _db;
    public AwardsController(AppDbContext db) { _db = db; }
    private User CurrentUser => (User)HttpContext.Items["CurrentUser"]!;

    private static readonly int[] ValidMilestones = [12, 35, 50, 100];

    // GET /awards — child sees their own milestone awards
    [HttpGet]
    public async Task<IActionResult> GetMyAwards()
    {
        var user = CurrentUser;
        var awards = await _db.MilestoneAwards
            .Where(a => a.ChildId == user.Id)
            .ToListAsync();

        var result = ValidMilestones.Select(shards =>
        {
            var a = awards.FirstOrDefault(x => x.MilestoneShards == shards);
            return a is not null ? MapAward(a) : new MilestoneAwardDTO(shards, null, null, null, null, null, null);
        }).ToList();

        return Ok(result);
    }

    // POST /awards/{milestoneShards}/claim — child claims an award
    [HttpPost("{milestoneShards:int}/claim")]
    public async Task<IActionResult> ClaimAward(int milestoneShards, [FromBody] ClaimAwardRequest req)
    {
        var user = CurrentUser;

        if (!ValidMilestones.Contains(milestoneShards))
            return BadRequest(new { error = "milestoneShards must be 12, 35, 50, or 100." });

        if (user.TotalStars < milestoneShards)
            return BadRequest(new { error = "You have not reached this milestone yet." });

        if (req.AwardIndex < 1 || req.AwardIndex > 3)
            return BadRequest(new { error = "AwardIndex must be 1, 2, or 3." });

        var award = await _db.MilestoneAwards
            .FirstOrDefaultAsync(a => a.ChildId == user.Id && a.MilestoneShards == milestoneShards);

        if (award is null)
            return NotFound(new { error = "No awards have been configured for this milestone." });

        var chosenText = req.AwardIndex switch
        {
            1 => award.Award1,
            2 => award.Award2,
            3 => award.Award3,
            _ => null
        };
        if (string.IsNullOrWhiteSpace(chosenText))
            return BadRequest(new { error = "The selected award slot is empty." });

        if (award.ClaimedAwardIndex is not null)
            return Conflict(new { error = "You have already claimed an award for this milestone." });

        award.ClaimedAwardIndex = req.AwardIndex;
        award.ClaimedAt = DateTime.UtcNow;
        award.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(MapAward(award));
    }

    private static MilestoneAwardDTO MapAward(MilestoneAward a) =>
        new(a.MilestoneShards, a.Award1, a.Award2, a.Award3, a.ClaimedAwardIndex, a.ClaimedAt, a.ParentViewedAt);
}
