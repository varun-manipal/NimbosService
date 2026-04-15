using System.ComponentModel.DataAnnotations;

namespace NimbosService.Models;

public class FamilyInvite
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FamilyId { get; set; }
    public Family Family { get; set; } = null!;

    [Required, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(8)]
    public string InviteCode { get; set; } = string.Empty;

    [Required, MaxLength(10)]
    public string Role { get; set; } = "child";  // "child" | "parent"

    public bool IsUsed { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
