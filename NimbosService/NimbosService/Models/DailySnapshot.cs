namespace NimbosService.Models;

public class DailySnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    public DateOnly SnapshotDate { get; set; }
    public double CompletionPercentage { get; set; }
    public int StarsLit { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
