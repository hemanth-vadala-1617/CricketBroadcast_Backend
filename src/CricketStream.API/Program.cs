using System.Threading.RateLimiting;
using CricketStream.API.Services;
using CricketStream.Application;
using CricketStream.Application.Interfaces;
using CricketStream.Infrastructure;
using CricketStream.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// SQLite file + JWT secret in App_Data, created on first start (nothing to install or configure)
LocalDataSetup.Apply(builder.Configuration, builder.Environment.ContentRootPath);

builder.Host.UseSerilog((context, configuration) => configuration.ReadFrom.Configuration(context.Configuration).WriteTo.Console());

// FluentValidation owns validation; MVC's implicit [Required] on every non-nullable string would reject legitimate empty optional fields ("").
builder.Services.AddControllers(o => o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddScoped<IMatchBroadcaster, MatchBroadcaster>();

// CORS: the SPA origins come from configuration; an empty list must not silently block everything in development.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
if (allowedOrigins.Length == 0 && builder.Environment.IsDevelopment())
    allowedOrigins = new[] { "http://localhost:5173", "http://localhost:4173" };
// Development: any localhost / 127.0.0.1 / private-LAN origin on any port (laptop, tablet on the same Wi-Fi, a second Vite port).
// Production: only the configured origins.
static bool IsLocalOrPrivate(string origin)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var u)) return false;
    if (u.IsLoopback || u.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)) return true;
    if (!System.Net.IPAddress.TryParse(u.Host, out var ip)) return false;
    var b = ip.GetAddressBytes();
    return b.Length == 4 && (b[0] == 10 || (b[0] == 192 && b[1] == 168) || (b[0] == 172 && b[1] is >= 16 and <= 31));
}
var isDev = builder.Environment.IsDevelopment();
// Cors:AllowAnyOrigin = true lets a page from ANY address call the API (a phone, tablet or another laptop, whatever its IP).
// The origin is echoed back rather than "*", so SignalR (which sends credentials) keeps working. Set it to false to go back to the allow-list above.
var allowAnyOrigin = builder.Configuration.GetValue("Cors:AllowAnyOrigin", false);
builder.Services.AddCors(o => o.AddPolicy("AllowFrontend", p => p
    .SetIsOriginAllowed(origin => allowAnyOrigin || allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase) || (isDev && IsLocalOrPrivate(origin)))
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

builder.Services.AddRateLimiter(o => {
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "anon", _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy("public", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "anon", _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddHealthChecks().AddDbContextCheck<CricketDbContext>();

var uploadPath = Path.GetFullPath(builder.Configuration["Uploads:Path"] ?? Path.Combine(builder.Environment.ContentRootPath, "uploads"));
Directory.CreateDirectory(uploadPath);
builder.Services.AddSingleton(new CricketStream.API.Services.UploadOptions(uploadPath));

var app = builder.Build();

app.UseExceptionHandler(errorApp => errorApp.Run(async ctx => {
    var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    Log.Error(ex, "Unhandled exception for {Path}", ctx.Request.Path);
    ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await ctx.Response.WriteAsJsonAsync(new { title = "Server error", status = 500, detail = "Something went wrong. It has been logged." });
}));
app.UseStatusCodePages();

if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.UseCors("AllowFrontend");
app.UseStaticFiles(new StaticFileOptions {
    FileProvider = new PhysicalFileProvider(uploadPath), RequestPath = "/uploads",
    OnPrepareResponse = c => {
        c.Context.Response.Headers.CacheControl = "public,max-age=86400";
        c.Context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    },
});
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<CricketStream.API.Hubs.MatchHub>("/hubs/match");
app.MapHealthChecks("/health");

// The database is created and brought up to date on every start (set Database:AutoMigrate to false to turn that off).
if (builder.Configuration.GetValue("Database:AutoMigrate", true)) {
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CricketDbContext>();
    await db.Database.MigrateAsync();
    // WAL lets the overlay and public pages read while the scorer writes
    await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
}
await DatabaseSeeder.SeedAsync(app.Services);
app.Run();

public partial class Program { }


