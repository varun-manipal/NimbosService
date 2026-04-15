using System.ComponentModel.DataAnnotations;

namespace NimbosService.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(64)]
    public string DeviceId { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(10)]
    public string Vibe { get; set; } = string.Empty;

    [MaxLength(10)]
    public string? ListPin { get; set; }

    [MaxLength(128)]
    public string? GoogleId { get; set; }

    [MaxLength(128)]
    public string? AppleId { get; set; }

    [MaxLength(254)]
    public string? Email { get; set; }

    public UserRole Role { get; set; } = UserRole.Solo;

    public int TotalStars { get; set; } = 0;
    public int DailyStars { get; set; } = 0;

    public DateTime? LastOpened { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    public ICollection<DailySnapshot> DailySnapshots { get; set; } = new List<DailySnapshot>();
    public Shield? Shield { get; set; }
}
