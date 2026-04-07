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

public record AppleAuthRequest(string UserIdentifier, string DeviceId, string? Email, string? FullName);

public record AppleAuthResponse(
    Guid? UserId,
    string? Token,
    bool IsNewUser,
    UserDTO? User,
    string? AppleId,
    string? Email,
    string? FullName
);
