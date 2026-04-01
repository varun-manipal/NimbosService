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
        // 1. Verify the token with Google's tokeninfo endpoint
        var client = _httpClientFactory.CreateClient();
        var tokenInfoUrl = $"https://oauth2.googleapis.com/tokeninfo?id_token={req.IdToken}";
        var response = await client.GetAsync(tokenInfoUrl);

        if (!response.IsSuccessStatusCode)
            return Unauthorized(new { error = "Invalid Google token" });

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // 2. Validate audience matches our iOS client ID
        var expectedClientId = _config["Google:ClientId"];
        var aud = root.TryGetProperty("aud", out var audProp) ? audProp.GetString() : null;
        Console.WriteLine($"Token aud: {aud}");
        Console.WriteLine($"Expected clientId: {expectedClientId}");
        if (aud != expectedClientId)
            return Unauthorized(new { error = $"Token audience mismatch: got {aud}, expected {expectedClientId}" });

        // 3. Validate email is verified
        var emailVerified = root.TryGetProperty("email_verified", out var evProp) ? evProp.GetString() : null;
        if (emailVerified != "true")
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
                Email: null
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
}
