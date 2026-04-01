namespace NimbosService.DTOs;

public record GoogleAuthRequest(string IdToken, string DeviceId);

public record GoogleAuthResponse(
    Guid? UserId,
    string? Token,
    bool IsNewUser,
    UserDTO? User,
    string? GoogleId,
    string? Email
);
