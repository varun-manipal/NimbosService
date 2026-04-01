namespace NimbosService.Models;

public class Shield
{
    public Guid UserId { get; set; }
    public int Fragments { get; set; } = 0;
    public bool IsActive { get; set; } = false;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
