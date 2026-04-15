namespace NimbosService.Models;

public class MilestoneAward
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChildId { get; set; }
    public int MilestoneShards { get; set; }  // 12, 35, 50, or 100
    public string? Award1 { get; set; }
    public string? Award2 { get; set; }
    public string? Award3 { get; set; }
    public int? ClaimedAwardIndex { get; set; }  // null = unclaimed, 1–3 = claimed
    public DateTime? ClaimedAt { get; set; }
    public DateTime? ParentViewedAt { get; set; }  // set when parent fetches; clears badge
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public User Child { get; set; } = null!;
}
