using Microsoft.EntityFrameworkCore;
using NimbosService.Data;
using NimbosService.Middleware;
using NimbosService.Services;
using NimbosService.Utilities;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(opts =>
{
    // EF Core reads DateTime from SQL Server with DateTimeKind.Unspecified, causing System.Text.Json
    // to omit the 'Z' timezone designator. Force all DateTimes to be treated as UTC on both
    // read and write so clients always receive a well-formed ISO-8601 string ending in 'Z'.
    opts.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient("apns").ConfigurePrimaryHttpMessageHandler(() =>
    new SocketsHttpHandler { EnableMultipleHttp2Connections = true });
builder.Services.AddSingleton<PushNotificationService>();
builder.Services.AddSingleton<EmailService>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    // Ensure APNs columns exist regardless of migration state.
    // Safe to run on every startup — IF NOT EXISTS makes each statement a no-op if column is already present.
    db.Database.ExecuteSqlRaw(@"
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Users') AND name = N'ApnsToken')
            ALTER TABLE [Users] ADD [ApnsToken] nvarchar(100) NULL;
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Users') AND name = N'Timezone')
            ALTER TABLE [Users] ADD [Timezone] nvarchar(64) NULL;
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Users') AND name = N'ApnsSandbox')
            ALTER TABLE [Users] ADD [ApnsSandbox] bit NOT NULL DEFAULT 1;
    ");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<DeviceTokenAuthMiddleware>();
app.MapControllers();

app.Run();
