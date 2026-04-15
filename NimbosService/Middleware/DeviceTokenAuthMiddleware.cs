using NimbosService.Data;
using NimbosService.Models;

namespace NimbosService.Middleware;

public class DeviceTokenAuthMiddleware
{
    private readonly RequestDelegate _next;

    public DeviceTokenAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        var path = context.Request.Path.Value ?? "";
        var method = context.Request.Method;

        // Skip auth for registration, Google/Apple auth, and Swagger
        if (path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) ||
            (method == "POST" && path.Equals("/users", StringComparison.OrdinalIgnoreCase)) ||
            (method == "POST" && path.Equals("/auth/google", StringComparison.OrdinalIgnoreCase)) ||
            (method == "POST" && path.Equals("/auth/apple", StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        var authHeader = context.Request.Headers.Authorization.ToString();
        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Missing or invalid Authorization header" });
            return;
        }

        var token = authHeader["Bearer ".Length..].Trim();
        if (!Guid.TryParse(token, out var userId))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid token format" });
            return;
        }

        var user = await db.Users.FindAsync(userId);
        if (user is null)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "User not found" });
            return;
        }

        context.Items["CurrentUser"] = user;
        await _next(context);
    }
}
