namespace NimbosService.DTOs;

public record RegisterRequest(
    string DeviceId,
    string Name,
    string Vibe,
    string? Pin,
    List<string> Tasks,
    string? GoogleId,
    string? AppleId,
    string? Email
);

public record RegisterResponse(Guid UserId, string Token, UserDTO User);

public record UpdateUserRequest(string? Name, string? Vibe, string? Pin);

public record UserDTO(
    string Name,
    string Vibe,
    string Role,
    int TotalStars,
    int DailyStars,
    ShieldDTO Shield,
    List<TaskDTO> Tasks,
    List<TaskDTO> TomorrowExtras
);

public record ShieldDTO(int Fragments, bool IsActive);
