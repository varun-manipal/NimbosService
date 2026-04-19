using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NimbosService.Data;
using NimbosService.Models;
using NimbosService.Services;

namespace NimbosService.Controllers;

[ApiController]
[Route("debug")]
public class DebugController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;

    public DebugController(AppDbContext db, IServiceProvider services, IConfiguration config)
    {
        _db = db;
        _services = services;
        _config = config;
    }

    private PushNotificationService? Push => _services.GetService<PushNotificationService>();

    // GET /debug/version — confirms which build is deployed (no auth required in middleware, but middleware will reject unauthenticated)
    [HttpGet("version")]
    public IActionResult Version() => Ok(new { version = "2026-04-18-v4", pushServiceAvailable = Push is not null });

    // GET /debug/push-test
    [HttpGet("push-test")]
    public Task<IActionResult> PushTest() => PushTestForUser(((User)HttpContext.Items["CurrentUser"]!).Id);

    // GET /debug/push-test-children — parent calls this to test each child's push pipeline.
    [HttpGet("push-test-children")]
    public async Task<IActionResult> PushTestChildren()
    {
        var caller = (User)HttpContext.Items["CurrentUser"]!;

        var childIds = await _db.FamilyMembers
            .Where(m => m.Family.Members.Any(p => p.UserId == caller.Id && p.Role == FamilyRole.Parent)
                     && m.Role == FamilyRole.Child)
            .Select(m => m.UserId)
            .ToListAsync();

        if (childIds.Count == 0)
            return Ok(new { message = "No children found in your family.", callerId = caller.Id });

        var results = new List<object>();
        foreach (var id in childIds)
            results.Add(await RunPushTest(id));

        return Ok(results);
    }

    private async Task<IActionResult> PushTestForUser(Guid userId)
    {
        return Ok(await RunPushTest(userId));
    }

    private async Task<object> RunPushTest(Guid userId)
    {
        // ApnsToken/ApnsSandbox are [NotMapped] — use raw ADO.NET to avoid EF Core query pipeline.
        string? apnsToken = null;
        bool apnsSandbox = false;
        string? userName = null;
        var conn = _db.Database.GetDbConnection();
        var shouldClose = conn.State != System.Data.ConnectionState.Open;
        if (shouldClose) await conn.OpenAsync();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT ApnsToken, ApnsSandbox, Name FROM Users WHERE Id = @id";
            var p = cmd.CreateParameter();
            p.ParameterName = "@id";
            p.Value = userId;
            cmd.Parameters.Add(p);
            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                if (shouldClose) await conn.CloseAsync();
                return new { userId, status = "user_not_found" };
            }
            apnsToken  = reader.IsDBNull(0) ? null : reader.GetString(0);
            apnsSandbox = !reader.IsDBNull(1) && reader.GetBoolean(1);
            userName   = reader.IsDBNull(2) ? null : reader.GetString(2);
        }
        finally
        {
            if (shouldClose) await conn.CloseAsync();
        }

        var tokenSnippet = apnsToken is not null
            ? apnsToken[..Math.Min(12, apnsToken.Length)] + "…"
            : null;

        if (apnsToken is null)
            return new { userId, name = userName, status = "no_token", deviceSandbox = (object?)null };

        var configSandbox = _config["Apns:UseSandbox"];
        var resolvedSandbox = configSandbox is not null ? configSandbox == "true" : apnsSandbox;

        string? apnsError = null;
        string? exceptionMessage = null;
        var push = Push;
        if (push is null)
        {
            exceptionMessage = "PushNotificationService not registered";
        }
        else
        {
            try
            {
                apnsError = await push.SendAsync(
                    apnsToken,
                    "Nimbos test ☁️",
                    "Push pipeline is working!",
                    "daily_summary",
                    resolvedSandbox);
            }
            catch (Exception ex)
            {
                exceptionMessage = ex.ToString();
            }
        }

        return new
        {
            userId,
            name = userName,
            status = apnsError is null && exceptionMessage is null ? "sent" : "failed",
            apnsError,
            exceptionMessage,
            tokenPrefix = tokenSnippet,
            deviceSandbox = apnsSandbox,
            configSandbox,
            resolvedSandbox
        };
    }
}

