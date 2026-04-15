using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NimbosService.Data;
using NimbosService.DTOs;
using NimbosService.Models;

namespace NimbosService.Controllers;

[ApiController]
[Route("family")]
public class FamilyController : ControllerBase
{
    private readonly AppDbContext _db;

    public FamilyController(AppDbContext db)
    {
        _db = db;
    }

    private User CurrentUser => (User)HttpContext.Items["CurrentUser"]!;

    // POST /family — create a new family, caller becomes Parent.
    // Idempotent: if the caller is already the parent of a family, returns that family.
    [HttpPost]
    public async Task<IActionResult> CreateFamily([FromBody] CreateFamilyRequest req)
    {
        var user = CurrentUser;

        // Check whether the user is already a family member.
        var existingMembership = await _db.FamilyMembers
            .Include(m => m.Family).ThenInclude(f => f.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(m => m.UserId == user.Id);

        if (existingMembership is not null)
        {
            // A child who already joined a family via invite cannot re-register as a parent.
            if (existingMembership.Role != FamilyRole.Parent)
                return Conflict(new { error = "You are already a member of a family as a Child." });

            // Repair case: FamilyMember row was written but User.Role was not persisted
            // (e.g., a crash between the two writes during the original creation).
            if (user.Role != UserRole.Parent)
            {
                user.Role = UserRole.Parent;
                await _db.SaveChangesAsync();
            }

            var existingFamily = existingMembership.Family;
            var existingMembers = existingFamily.Members.Select(m =>
                new FamilyMemberDTO(m.UserId, m.User.Name, m.Role.ToString(), m.User.TotalStars, m.User.DailyStars)
            ).ToList();
            return Ok(new FamilyResponse(existingFamily.Id, existingFamily.Name, existingMembers));
        }

        var family = new Family { Name = req.FamilyName };
        _db.Families.Add(family);

        _db.FamilyMembers.Add(new FamilyMember
        {
            FamilyId = family.Id,
            UserId = user.Id,
            Role = FamilyRole.Parent
        });

        // Always set the DB role to Parent regardless of what value was stored
        // at registration time, so sign-in always returns "parent" for this user.
        user.Role = UserRole.Parent;
        await _db.SaveChangesAsync();

        return Ok(new FamilyResponse(family.Id, family.Name, new List<FamilyMemberDTO>
        {
            new(user.Id, user.Name, "Parent", user.TotalStars, user.DailyStars)
        }));
    }

    // GET /family — get current user's family info
    [HttpGet]
    public async Task<IActionResult> GetFamily()
    {
        var user = CurrentUser;

        var membership = await _db.FamilyMembers
            .AsNoTracking()
            .Include(m => m.Family).ThenInclude(f => f.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(m => m.UserId == user.Id);

        if (membership is null)
            return NotFound(new { error = "You are not in a family." });

        var family = membership.Family;
        var members = family.Members.Select(m =>
            new FamilyMemberDTO(m.UserId, m.User.Name, m.Role.ToString(), m.User.TotalStars, m.User.DailyStars)
        ).ToList();

        return Ok(new FamilyResponse(family.Id, family.Name, members));
    }

    // GET /family/invites — parent lists pending (unused) invites
    [HttpGet("invites")]
    public async Task<IActionResult> GetPendingInvites()
    {
        var user = CurrentUser;
        var family = await GetParentFamily(user.Id);
        if (family is null) return Forbid();

        var pending = await _db.FamilyInvites
            .Where(fi => fi.FamilyId == family.Id && !fi.IsUsed)
            .OrderByDescending(fi => fi.CreatedAt)
            .Select(fi => new InviteResponse(fi.InviteCode, fi.Email, fi.Role))
            .ToListAsync();

        return Ok(pending);
    }

    // DELETE /family/invites/{inviteCode} — parent cancels a pending invite
    [HttpDelete("invites/{inviteCode}")]
    public async Task<IActionResult> DeleteInvite(string inviteCode)
    {
        var user = CurrentUser;
        var family = await GetParentFamily(user.Id);
        if (family is null) return Forbid();

        var invite = await _db.FamilyInvites
            .FirstOrDefaultAsync(fi => fi.FamilyId == family.Id &&
                                       fi.InviteCode == inviteCode.ToUpper() &&
                                       !fi.IsUsed);

        if (invite is null) return NotFound();

        _db.FamilyInvites.Remove(invite);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // POST /family/invites — parent creates an email-specific invite code
    [HttpPost("invites")]
    public async Task<IActionResult> CreateInvite([FromBody] CreateInviteRequest req)
    {
        var user = CurrentUser;
        var family = await GetParentFamily(user.Id);
        if (family is null) return Forbid();

        // Validate before Trim() to avoid NullReferenceException on null body field
        if (string.IsNullOrWhiteSpace(req.Email))
            return BadRequest(new { error = "Email is required." });
        var email = req.Email.Trim().ToLowerInvariant();

        var role = string.IsNullOrWhiteSpace(req.Role) ? "child" : req.Role.Trim().ToLowerInvariant();
        if (role != "child" && role != "parent")
            return BadRequest(new { error = "Role must be 'child' or 'parent'." });

        // Return existing unused invite for this email if one exists
        var existing = await _db.FamilyInvites
            .FirstOrDefaultAsync(fi => fi.FamilyId == family.Id && fi.Email == email && !fi.IsUsed);
        if (existing is not null)
            return Ok(new InviteResponse(existing.InviteCode, existing.Email, existing.Role));

        // Generate a unique invite code, retrying on the rare concurrent-insert collision.
        // We skip the AnyAsync pre-check and let the unique index be the sole guard — it is
        // race-free and avoids an extra DB round-trip on every attempt.
        // SQL Server error 2627 = primary key violation; 2601 = unique index violation.
        for (int attempt = 0; attempt < 5; attempt++)
        {
            var code = GenerateInviteCode();
            var invite = new FamilyInvite { FamilyId = family.Id, Email = email, InviteCode = code, Role = role };
            _db.FamilyInvites.Add(invite);
            try
            {
                await _db.SaveChangesAsync();
                return Ok(new InviteResponse(code, email, role));
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is SqlException sqlEx && (sqlEx.Number == 2627 || sqlEx.Number == 2601))
            {
                _db.ChangeTracker.Clear();
                // Concurrent insert collision — retry with a new code
            }
        }
        return StatusCode(503, new { error = "Could not generate a unique invite code. Please try again." });
    }

    // POST /family/join — join a family as Child using invite code + email
    [HttpPost("join")]
    public async Task<IActionResult> JoinFamily([FromBody] JoinFamilyRequest req)
    {
        var user = CurrentUser;

        if (await _db.FamilyMembers.AnyAsync(m => m.UserId == user.Id))
            return Conflict(new { error = "You are already in a family." });

        var invite = await _db.FamilyInvites
            .Include(fi => fi.Family).ThenInclude(f => f.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(fi => fi.InviteCode == req.InviteCode.ToUpper() && !fi.IsUsed);

        if (invite is null)
            return NotFound(new { error = "Invalid or already used invite code." });

        if (!string.Equals(invite.Email, req.Email.Trim(), StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Email does not match the invite." });

        invite.IsUsed = true;

        var memberRole = invite.Role == "parent" ? FamilyRole.Parent : FamilyRole.Child;
        var userRole   = invite.Role == "parent" ? UserRole.Parent   : UserRole.Child;

        _db.FamilyMembers.Add(new FamilyMember
        {
            FamilyId = invite.FamilyId,
            UserId = user.Id,
            Role = memberRole
        });

        user.Role = userRole;
        await _db.SaveChangesAsync();

        var family = invite.Family;
        var members = family.Members.Select(m =>
            new FamilyMemberDTO(m.UserId, m.User.Name, m.Role.ToString(), m.User.TotalStars, m.User.DailyStars)
        ).ToList();
        members.Add(new FamilyMemberDTO(user.Id, user.Name, memberRole.ToString(), user.TotalStars, user.DailyStars));

        return Ok(new FamilyResponse(family.Id, family.Name, members));
    }

    // GET /family/children — parent gets list of children with progress
    [HttpGet("children")]
    public async Task<IActionResult> GetChildren()
    {
        var user = CurrentUser;
        var family = await GetParentFamily(user.Id);
        if (family is null) return Forbid();

        var childUserIds = family.Members
            .Where(m => m.Role == FamilyRole.Child)
            .Select(m => m.UserId)
            .ToList();

        var children = await _db.Users
            .Include(u => u.Tasks)
            .Include(u => u.Shield)
            .Where(u => childUserIds.Contains(u.Id))
            .ToListAsync();

        List<MilestoneAward> allAwards;
        try
        {
            allAwards = await _db.MilestoneAwards
                .Where(a => childUserIds.Contains(a.ChildId))
                .ToListAsync();
        }
        catch
        {
            allAwards = new List<MilestoneAward>();
        }

        var result = children.Select(c =>
        {
            var awards = allAwards.Where(a => a.ChildId == c.Id).ToList();
            return BuildChildProgress(c, awards);
        }).ToList();
        return Ok(result);
    }

    // GET /family/children/{childId} — parent gets full state of one child
    [HttpGet("children/{childId:guid}")]
    public async Task<IActionResult> GetChild(Guid childId)
    {
        var user = CurrentUser;
        if (!await IsParentOf(user.Id, childId)) return Forbid();

        var child = await _db.Users
            .Include(u => u.Tasks)
            .Include(u => u.Shield)
            .FirstOrDefaultAsync(u => u.Id == childId);

        if (child is null) return NotFound();

        List<MilestoneAward> awards;
        try { awards = await _db.MilestoneAwards.Where(a => a.ChildId == childId).ToListAsync(); }
        catch { awards = new List<MilestoneAward>(); }

        return Ok(BuildChildProgress(child, awards));
    }

    // POST /family/children/{childId}/tasks — parent adds task to child
    [HttpPost("children/{childId:guid}/tasks")]
    public async Task<IActionResult> AddTaskToChild(Guid childId, [FromBody] CreateTaskRequest req)
    {
        var user = CurrentUser;
        if (!await IsParentOf(user.Id, childId)) return Forbid();

        var task = new TaskItem { UserId = childId, Title = req.Title, AddedByParent = true };
        _db.Tasks.Add(task);
        await _db.SaveChangesAsync();

        return Ok(UsersController.MapTask(task));
    }

    // PATCH /family/children/{childId}/tasks/{taskId} — parent renames child's task
    [HttpPatch("children/{childId:guid}/tasks/{taskId:guid}")]
    public async Task<IActionResult> UpdateChildTask(Guid childId, Guid taskId, [FromBody] UpdateTaskRequest req)
    {
        var user = CurrentUser;
        if (!await IsParentOf(user.Id, childId)) return Forbid();

        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == childId);
        if (task is null) return NotFound();

        if (req.Title is not null) task.Title = req.Title;
        await _db.SaveChangesAsync();

        return Ok(UsersController.MapTask(task));
    }

    // DELETE /family/children/{childId}/tasks/{taskId} — parent deletes child's task
    [HttpDelete("children/{childId:guid}/tasks/{taskId:guid}")]
    public async Task<IActionResult> DeleteChildTask(Guid childId, Guid taskId)
    {
        var user = CurrentUser;
        if (!await IsParentOf(user.Id, childId)) return Forbid();

        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == childId);
        if (task is null) return NotFound();

        _db.Tasks.Remove(task);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // MARK: - Helpers

    private async Task<Family?> GetParentFamily(Guid userId)
    {
        var membership = await _db.FamilyMembers
            .Include(m => m.Family).ThenInclude(f => f.Members)
            .FirstOrDefaultAsync(m => m.UserId == userId && m.Role == FamilyRole.Parent);
        return membership?.Family;
    }

    private async Task<bool> IsParentOf(Guid parentUserId, Guid childUserId)
    {
        var parentMembership = await _db.FamilyMembers
            .FirstOrDefaultAsync(m => m.UserId == parentUserId && m.Role == FamilyRole.Parent);
        if (parentMembership is null) return false;

        return await _db.FamilyMembers.AnyAsync(m =>
            m.UserId == childUserId &&
            m.FamilyId == parentMembership.FamilyId &&
            m.Role == FamilyRole.Child);
    }

    private static ChildProgressDTO BuildChildProgress(User child, List<MilestoneAward> awards)
    {
        var activeTasks = child.Tasks.Where(t => !t.IsSnoozed && !t.IsDismissedToday && !t.IsTomorrowOnly).ToList();
        var completedCount = activeTasks.Count(t => t.IsCompleted);
        var completion = activeTasks.Count > 0 ? (double)completedCount / activeTasks.Count : 0;

        var shield = child.Shield ?? new Shield();
        var tasks = child.Tasks.Where(t => !t.IsTomorrowOnly).Select(UsersController.MapTask).ToList();
        bool hasNewAwardClaim = awards.Any(a => a.ClaimedAt != null && a.ParentViewedAt == null);

        return new ChildProgressDTO(
            UserId: child.Id,
            Name: child.Name,
            TotalStars: child.TotalStars,
            DailyStars: child.DailyStars,
            DailyCompletionPercentage: completion,
            Shield: new ShieldDTO(shield.Fragments, shield.IsActive),
            Tasks: tasks,
            HasNewAwardClaim: hasNewAwardClaim
        );
    }

    // GET /family/children/{childId}/awards — parent views child's award status
    [HttpGet("children/{childId:guid}/awards")]
    public async Task<IActionResult> GetChildAwards(Guid childId)
    {
        var user = CurrentUser;
        if (!await IsParentOf(user.Id, childId)) return Forbid();
        return Ok(await GetOrCreateAwardDTOs(childId, markViewed: true));
    }

    // PUT /family/children/{childId}/awards/{milestoneShards} — parent sets award text
    [HttpPut("children/{childId:guid}/awards/{milestoneShards:int}")]
    public async Task<IActionResult> SetChildAward(Guid childId, int milestoneShards, [FromBody] SetMilestoneAwardRequest req)
    {
        var user = CurrentUser;
        if (!await IsParentOf(user.Id, childId)) return Forbid();

        if (!ValidMilestones.Contains(milestoneShards))
            return BadRequest(new { error = "milestoneShards must be 12, 35, 50, or 100." });

        if (string.IsNullOrWhiteSpace(req.Award1) && string.IsNullOrWhiteSpace(req.Award2) && string.IsNullOrWhiteSpace(req.Award3))
            return BadRequest(new { error = "At least one award must be provided." });

        var existing = await _db.MilestoneAwards
            .FirstOrDefaultAsync(a => a.ChildId == childId && a.MilestoneShards == milestoneShards);

        if (existing is null)
        {
            existing = new MilestoneAward { ChildId = childId, MilestoneShards = milestoneShards };
            _db.MilestoneAwards.Add(existing);
        }

        existing.Award1 = string.IsNullOrWhiteSpace(req.Award1) ? null : req.Award1.Trim();
        existing.Award2 = string.IsNullOrWhiteSpace(req.Award2) ? null : req.Award2.Trim();
        existing.Award3 = string.IsNullOrWhiteSpace(req.Award3) ? null : req.Award3.Trim();
        existing.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(MapAward(existing));
    }

    private static readonly int[] ValidMilestones = [12, 35, 50, 100];

    private async Task<List<MilestoneAwardDTO>> GetOrCreateAwardDTOs(Guid childId, bool markViewed = false)
    {
        var awards = await _db.MilestoneAwards
            .Where(a => a.ChildId == childId)
            .ToListAsync();

        if (markViewed)
        {
            foreach (var a in awards.Where(a => a.ClaimedAt != null && a.ParentViewedAt == null))
                a.ParentViewedAt = DateTime.UtcNow;
            if (awards.Any(a => a.ParentViewedAt.HasValue))
                await _db.SaveChangesAsync();
        }

        return ValidMilestones.Select(shards =>
        {
            var a = awards.FirstOrDefault(x => x.MilestoneShards == shards);
            return a is not null ? MapAward(a) : new MilestoneAwardDTO(shards, null, null, null, null, null, null);
        }).ToList();
    }

    private static MilestoneAwardDTO MapAward(MilestoneAward a) =>
        new(a.MilestoneShards, a.Award1, a.Award2, a.Award3, a.ClaimedAwardIndex, a.ClaimedAt, a.ParentViewedAt);

    private static string GenerateInviteCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(6);
        return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
    }
}
