using CricketStream.Domain.Entities;
using CricketStream.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CricketStream.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static readonly string[] Roles = { "SuperAdmin", "Admin", "Scorer", "Producer", "Viewer" };

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var config = sp.GetRequiredService<IConfiguration>();
        var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");
        var db = sp.GetRequiredService<CricketDbContext>();

        await SeedIdentityAsync(sp, config, log);
        await SeedRulesAsync(db);
        await SeedThemeAsync(db);
        if (config.GetValue<bool>("Seed:SampleData")) await SeedSampleAsync(db, log);
    }

    private static async Task SeedIdentityAsync(IServiceProvider sp, IConfiguration config, ILogger log)
    {
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = sp.GetRequiredService<UserManager<IdentityUser>>();
        foreach (var role in Roles)
            if (!await roleManager.RoleExistsAsync(role)) await roleManager.CreateAsync(new IdentityRole(role));

        var email = config["Seed:AdminEmail"];
        var password = config["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            log.LogWarning("Seed:AdminEmail / Seed:AdminPassword are not configured; no admin account was created.");
            return;
        }
        if (await userManager.FindByEmailAsync(email) is not null) return;
        var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded) { log.LogError("Could not create the admin user: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description))); return; }
        await userManager.AddToRoleAsync(user, "SuperAdmin");
    }

    private static async Task SeedRulesAsync(CricketDbContext db)
    {
        var presets = new[]
        {
            new MatchRules { Name = "T20", OversPerInnings = 20, MaxOversPerBowler = 4, PowerplayOvers = 6 },
            new MatchRules { Name = "T10", OversPerInnings = 10, MaxOversPerBowler = 2, PowerplayOvers = 3 },
            new MatchRules { Name = "ODI", OversPerInnings = 50, MaxOversPerBowler = 10, PowerplayOvers = 10 },
            new MatchRules
            {
                Name = "Test (5 days)", IsTest = true, OversPerInnings = 0, MaxOversPerBowler = 0, PowerplayOvers = 0,
                FreeHitEnabled = false, FollowOnEnabled = true, FollowOnMargin = 200, InningsPerSide = 2, Days = 5,
                OversPerDay = 90, SessionsPerDay = 3, NewBallAfterOvers = 80,
            },
        };
        foreach (var p in presets)
            if (!await db.MatchRules.AnyAsync(r => r.Name == p.Name)) db.MatchRules.Add(p);
        await db.SaveChangesAsync();
    }

    private static async Task SeedThemeAsync(CricketDbContext db)
    {
        if (await db.OverlayThemes.AnyAsync()) return;
        db.OverlayThemes.Add(new OverlayTheme
        {
            Name = "Broadcast Green", PrimaryColor = "#0f6b4f", SecondaryColor = "#7a1fa2", AccentColor = "#ffd400", WatermarkText = "LIVE",
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedSampleAsync(CricketDbContext db, ILogger log)
    {
        if (await db.Teams.AnyAsync()) return;
        var ind = new Team { Name = "India", ShortName = "IND", Country = "India", PrimaryColor = "#1d4ed8", SecondaryColor = "#f97316" };
        var aus = new Team { Name = "Australia", ShortName = "AUS", Country = "Australia", PrimaryColor = "#facc15", SecondaryColor = "#166534" };
        db.Teams.AddRange(ind, aus);

        void Squad(Team t, params (string Name, PlayerRole Role)[] ps)
        {
            var n = 1;
            foreach (var (name, role) in ps)
            {
                var parts = name.Split(' ', 2);
                db.Players.Add(new Player
                {
                    CurrentTeam = t, FirstName = parts[0], LastName = parts.Length > 1 ? parts[1] : "", DisplayName = name,
                    ShortName = parts.Length > 1 ? $"{parts[0][0]} {parts[1]}" : name, PlayerRole = role, JerseyNumber = n++,
                });
            }
        }
        Squad(ind, ("Rohit Sharma", PlayerRole.Batter), ("Shubman Gill", PlayerRole.Batter), ("Virat Kohli", PlayerRole.Batter),
            ("KL Rahul", PlayerRole.WicketKeeper), ("Rishabh Pant", PlayerRole.WicketKeeper), ("Hardik Pandya", PlayerRole.AllRounder),
            ("Ravindra Jadeja", PlayerRole.AllRounder), ("Axar Patel", PlayerRole.AllRounder), ("Kuldeep Yadav", PlayerRole.Bowler),
            ("Jasprit Bumrah", PlayerRole.Bowler), ("Mohammed Siraj", PlayerRole.Bowler), ("Arshdeep Singh", PlayerRole.Bowler),
            ("Washington Sundar", PlayerRole.AllRounder));
        Squad(aus, ("Travis Head", PlayerRole.Batter), ("Mitchell Marsh", PlayerRole.AllRounder), ("Steve Smith", PlayerRole.Batter),
            ("Marnus Labuschagne", PlayerRole.Batter), ("Alex Carey", PlayerRole.WicketKeeper), ("Glenn Maxwell", PlayerRole.AllRounder),
            ("Cameron Green", PlayerRole.AllRounder), ("Pat Cummins", PlayerRole.Bowler), ("Mitchell Starc", PlayerRole.Bowler),
            ("Adam Zampa", PlayerRole.Bowler), ("Josh Hazlewood", PlayerRole.Bowler), ("Marcus Stoinis", PlayerRole.AllRounder),
            ("Nathan Lyon", PlayerRole.Bowler));

        var theme = await db.OverlayThemes.FirstAsync();
        var t20 = await db.MatchRules.FirstAsync(r => r.Name == "T20");
        db.Tournaments.Add(new Tournament
        {
            Name = "Sample Series", ShortName = "SAMPLE", Season = DateTime.UtcNow.Year.ToString(), Format = MatchFormat.T20,
            StartDate = DateTimeOffset.UtcNow.Date, EndDate = DateTimeOffset.UtcNow.Date.AddDays(30), Status = "Live",
            DefaultMatchRulesId = t20.Id, OverlayThemeId = theme.Id,
        });
        db.Venues.Add(new Venue { Name = "Sample Stadium", City = "Mumbai", Country = "India", Capacity = 33000, Timezone = "Asia/Kolkata" });
        await db.SaveChangesAsync();
        log.LogInformation("Seeded sample data (India vs Australia squads, tournament, venue).");
    }
}
