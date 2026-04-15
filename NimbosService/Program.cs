using Microsoft.EntityFrameworkCore;
using NimbosService.Data;
using NimbosService.Middleware;
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
builder.Services.AddHttpClient();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<DeviceTokenAuthMiddleware>();
app.MapControllers();

app.Run();
