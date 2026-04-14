using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NimbosService.Data;
using NimbosService.DTOs;
using NimbosService.Models;
using System.Text.Json;

namespace NimbosService.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;

    public AuthController(AppDbContext db, IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _config = config;
    }

    // POST /auth/google — verify Google ID token, return existing user or signal new user
    [HttpPost("google")]
    public async Task<IActionResult> GoogleAuth([FromBody] GoogleAuthRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.IdToken))
            return BadRequest(new { error = "ID token is required" });

        // 1. Verify the token with Google's tokeninfo endpoint
        JsonElement root;
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            var tokenInfoUrl = $"https://oauth2.googleapis.com/tokeninfo?id_token={req.IdToken}";
            var response = await client.GetAsync(tokenInfoUrl);

            if (!response.IsSuccessStatusCode)
                return Unauthorized(new { error = "Invalid Google token" });

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            root = doc.RootElement.Clone();
        }
        catch (Exception)
        {
            return StatusCode(500, new { error = "Failed to verify token with Google" });
        }

        // 2. Validate audience matches our iOS client ID
        var expectedClientId = _config["Google:ClientId"];
        var aud = root.TryGetProperty("aud", out var audProp) ? audProp.GetString() : null;
        Console.WriteLine($"Token aud: {aud}");
        Console.WriteLine($"Expected clientId: {expectedClientId}");
        if (string.IsNullOrEmpty(aud) || aud != expectedClientId)
            return Unauthorized(new { error = $"Token audience mismatch: got {aud}, expected {expectedClientId}" });

        // 3. Validate email is verified
        var emailVerified = root.TryGetProperty("email_verified", out var evProp) &&
            evProp.ValueKind switch {
                System.Text.Json.JsonValueKind.True   => true,
                System.Text.Json.JsonValueKind.String => evProp.GetString() == "true",
                _                                     => false
            };
        if (!emailVerified)
            return Unauthorized(new { error = "Email not verified" });

        var googleId = root.TryGetProperty("sub", out var subProp) ? subProp.GetString() : null;
        var email = root.TryGetProperty("email", out var emailProp) ? emailProp.GetString() : null;

        if (string.IsNullOrEmpty(googleId))
            return Unauthorized(new { error = "Missing sub claim" });

        // 4. Look up existing user by GoogleId
        var user = await _db.Users
            .Include(u => u.Tasks)
            .Include(u => u.Shield)
            .FirstOrDefaultAsync(u => u.GoogleId == googleId);

        if (user is not null)
        {
            // Returning user — update their DeviceId to the current device
            user.DeviceId = req.DeviceId;
            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            var shield = user.Shield ?? new Shield();
            var tasks = user.Tasks.Where(t => !t.IsTomorrowOnly).ToList();
            var tomorrowExtras = user.Tasks.Where(t => t.IsTomorrowOnly).ToList();

            var userDto = new UserDTO(
                Name: user.Name,
                Vibe: user.Vibe,
                Role: user.Role.ToString().ToLowerInvariant(),
                TotalStars: user.TotalStars,
                DailyStars: user.DailyStars,
                Shield: new ShieldDTO(shield.Fragments, shield.IsActive),
                Tasks: tasks.Select(UsersController.MapTask).ToList(),
                TomorrowExtras: tomorrowExtras.Select(UsersController.MapTask).ToList()
            );

            return Ok(new GoogleAuthResponse(
                UserId: user.Id,
                Token: user.Id.ToString(),
                IsNewUser: false,
                User: userDto,
                GoogleId: null,
                Email: user.Email
            ));
        }

        // 5. New user — signal the app to run onboarding
        return Ok(new GoogleAuthResponse(
            UserId: null,
            Token: null,
            IsNewUser: true,
            User: null,
            GoogleId: googleId,
            Email: email
        ));
    }

    // POST /auth/apple — verify Apple user identifier, return existing user or signal new user
    [HttpPost("apple")]
    public async Task<IActionResult> AppleAuth([FromBody] AppleAuthRequest req)
    {
        if (string.IsNullOrEmpty(req.UserIdentifier))
            return BadRequest(new { error = "Missing Apple user identifier" });

        var user = await _db.Users
            .Include(u => u.Tasks)
            .Include(u => u.Shield)
            .FirstOrDefaultAsync(u => u.AppleId == req.UserIdentifier);

        if (user is not null)
        {
            // Returning user — update DeviceId
            user.DeviceId = req.DeviceId;
            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            var shield = user.Shield ?? new Shield();
            var tasks = user.Tasks.Where(t => !t.IsTomorrowOnly).ToList();
            var tomorrowExtras = user.Tasks.Where(t => t.IsTomorrowOnly).ToList();

            var userDto = new UserDTO(
                Name: user.Name,
                Vibe: user.Vibe,
                Role: user.Role.ToString().ToLowerInvariant(),
                TotalStars: user.TotalStars,
                DailyStars: user.DailyStars,
                Shield: new ShieldDTO(shield.Fragments, shield.IsActive),
                Tasks: tasks.Select(UsersController.MapTask).ToList(),
                TomorrowExtras: tomorrowExtras.Select(UsersController.MapTask).ToList()
            );

            return Ok(new AppleAuthResponse(
                UserId: user.Id,
                Token: user.Id.ToString(),
                IsNewUser: false,
                User: userDto,
                AppleId: null,
                Email: user.Email,
                FullName: user.Name
            ));
        }

        // New user — signal the app to run onboarding
        return Ok(new AppleAuthResponse(
            UserId: null,
            Token: null,
            IsNewUser: true,
            User: null,
            AppleId: req.UserIdentifier,
            Email: req.Email,
            FullName: req.FullName
        ));
    }
}
