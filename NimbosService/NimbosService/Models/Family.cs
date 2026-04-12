using System.ComponentModel.DataAnnotations;

namespace NimbosService.Models;

public class Family
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(8)]
    public string InviteCode { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<FamilyMember> Members { get; set; } = new List<FamilyMember>();
    public ICollection<FamilyInvite> Invites { get; set; } = new List<FamilyInvite>();
}
