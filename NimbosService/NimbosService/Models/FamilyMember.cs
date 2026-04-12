namespace NimbosService.Models;

public class FamilyMember
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FamilyId { get; set; }
    public Family Family { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public FamilyRole Role { get; set; }

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

public enum FamilyRole { Parent, Child }
