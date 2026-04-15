namespace NimbosService.DTOs;

public record CreateFamilyRequest(string FamilyName);

public record CreateInviteRequest(string Email);

public record InviteResponse(string InviteCode, string Email);

public record JoinFamilyRequest(string InviteCode, string Email);

public record FamilyMemberDTO(
    Guid UserId,
    string Name,
    string Role,
    int TotalStars,
    int DailyStars
);

public record FamilyResponse(
    Guid FamilyId,
    string Name,
    List<FamilyMemberDTO> Members
);

public record ChildProgressDTO(
    Guid UserId,
    string Name,
    int TotalStars,
    int DailyStars,
    double DailyCompletionPercentage,
    ShieldDTO Shield,
    List<TaskDTO> Tasks,
    bool HasNewAwardClaim
);
