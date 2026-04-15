using System.ComponentModel.DataAnnotations;

namespace NimbosService.Models;

public class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public bool IsCompleted { get; set; } = false;
    public bool IsSnoozed { get; set; } = false;
    public bool IsDismissedToday { get; set; } = false;
    public bool IsSkippedTomorrow { get; set; } = false;
    public bool IsTomorrowOnly { get; set; } = false;
    public bool AddedByParent { get; set; } = false;

    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
