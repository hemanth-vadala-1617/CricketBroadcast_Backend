using CricketStream.Application.Interfaces;
using CricketStream.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CricketStream.Infrastructure.Persistence;

public class CricketDbContext : IdentityDbContext<IdentityUser>, ICricketDbContext
{
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TournamentTeam> TournamentTeams => Set<TournamentTeam>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<MatchRules> MatchRules => Set<MatchRules>();
    public DbSet<OverlayTheme> OverlayThemes => Set<OverlayTheme>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchPlayer> MatchPlayers => Set<MatchPlayer>();
    public DbSet<MatchStaff> MatchStaff => Set<MatchStaff>();
    public DbSet<Innings> Innings => Set<Innings>();
    public DbSet<Over> Overs => Set<Over>();
    public DbSet<Ball> Balls => Set<Ball>();
    public DbSet<Dismissal> Dismissals => Set<Dismissal>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OverlayGraphicState> OverlayGraphicStates => Set<OverlayGraphicState>();

    public CricketDbContext(DbContextOptions<CricketDbContext> options) : base(options) { }

    /// <summary>Npgsql only accepts UTC offsets. Clients send +05:30 etc., so every DateTimeOffset is normalised to UTC on the way in.</summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>()
            .HaveConversion<UtcDateTimeOffsetConverter>();
        configurationBuilder.Properties<DateTimeOffset?>()
            .HaveConversion<NullableUtcDateTimeOffsetConverter>();
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // ---- master data ----
        b.Entity<Team>(e => {
            e.HasIndex(t => t.Name).IsUnique();
            e.Property(t => t.Name).HasMaxLength(100);
            e.Property(t => t.ShortName).HasMaxLength(10);
        });
        b.Entity<TournamentTeam>(e => {
            e.HasOne(x => x.Tournament).WithMany().HasForeignKey(x => x.TournamentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Team).WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.TournamentId, x.TeamId }).IsUnique();
        });
        b.Entity<Player>(e => {
            e.HasOne(p => p.CurrentTeam).WithMany(t => t.Players).HasForeignKey(p => p.CurrentTeamId).OnDelete(DeleteBehavior.SetNull);
            e.Property(p => p.DisplayName).HasMaxLength(100);
            e.Property(p => p.ShortName).HasMaxLength(20);
            e.HasIndex(p => p.DisplayName);
        });
        b.Entity<Venue>().Property(v => v.Name).HasMaxLength(150);
        b.Entity<Tournament>(e => {
            e.Property(t => t.Name).HasMaxLength(150);
            e.HasOne<MatchRules>().WithMany().HasForeignKey(t => t.DefaultMatchRulesId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<OverlayTheme>().WithMany().HasForeignKey(t => t.OverlayThemeId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<MatchRules>().HasIndex(r => r.Name).IsUnique();
        b.Entity<RefreshToken>().HasIndex(rt => rt.Token).IsUnique();

        // ---- match ----
        b.Entity<Match>(e => {
            e.HasOne(m => m.HomeTeam).WithMany().HasForeignKey(m => m.HomeTeamId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.AwayTeam).WithMany().HasForeignKey(m => m.AwayTeamId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.TossWinnerTeam).WithMany().HasForeignKey(m => m.TossWinnerTeamId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.WinnerTeam).WithMany().HasForeignKey(m => m.WinnerTeamId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.Tournament).WithMany().HasForeignKey(m => m.TournamentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.Venue).WithMany().HasForeignKey(m => m.VenueId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.MatchRules).WithMany().HasForeignKey(m => m.MatchRulesId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.OverlayTheme).WithMany().HasForeignKey(m => m.OverlayThemeId).OnDelete(DeleteBehavior.SetNull);
            e.Property(m => m.Version).IsConcurrencyToken();   // two scorers on one match => 409, not corruption
            e.Property(m => m.Title).HasMaxLength(200);
            e.HasIndex(m => new { m.Status, m.ScheduledStart });
        });
        b.Entity<MatchPlayer>(e => {
            e.HasOne(mp => mp.Match).WithMany(m => m.MatchPlayers).HasForeignKey(mp => mp.MatchId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(mp => mp.Team).WithMany().HasForeignKey(mp => mp.TeamId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(mp => mp.Player).WithMany().HasForeignKey(mp => mp.PlayerId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(mp => new { mp.MatchId, mp.PlayerId }).IsUnique();
        });

        b.Entity<MatchStaff>(e => {
            e.HasOne(s => s.Match).WithMany(m => m.MatchStaff).HasForeignKey(s => s.MatchId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.Team).WithMany().HasForeignKey(s => s.TeamId).OnDelete(DeleteBehavior.Restrict);
            e.Property(s => s.Name).HasMaxLength(100);
            e.Property(s => s.Role).HasMaxLength(60);
            e.HasIndex(s => new { s.MatchId, s.TeamId });
        });

        // ---- scoring ----
        b.Entity<Innings>(e => {
            e.HasOne(i => i.Match).WithMany(m => m.Innings).HasForeignKey(i => i.MatchId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.BattingTeam).WithMany().HasForeignKey(i => i.BattingTeamId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(i => i.BowlingTeam).WithMany().HasForeignKey(i => i.BowlingTeamId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(i => new { i.MatchId, i.InningsNumber }).IsUnique();
        });
        b.Entity<Over>(e => {
            e.HasOne(o => o.Innings).WithMany(i => i.Overs).HasForeignKey(o => o.InningsId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(o => o.Bowler).WithMany().HasForeignKey(o => o.BowlerId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(o => new { o.InningsId, o.OverNumber }).IsUnique();
        });
        b.Entity<Ball>(e => {
            e.HasOne(x => x.Over).WithMany(o => o.Balls).HasForeignKey(x => x.OverId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Innings).WithMany().HasForeignKey(x => x.InningsId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Striker).WithMany().HasForeignKey(x => x.StrikerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.NonStriker).WithMany().HasForeignKey(x => x.NonStrikerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Bowler).WithMany().HasForeignKey(x => x.BowlerId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.InningsId, x.Sequence }).IsUnique();
            e.HasIndex(x => x.ClientRequestId).IsUnique();     // idempotency
        });
        b.Entity<Dismissal>(e => {
            e.HasOne(d => d.Ball).WithOne(x => x.Dismissal).HasForeignKey<Dismissal>(d => d.BallId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(d => d.DismissedPlayer).WithMany().HasForeignKey(d => d.DismissedPlayerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(d => d.Fielder).WithMany().HasForeignKey(d => d.FielderId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---- presentation / audit ----
        b.Entity<OverlayGraphicState>(e => {
            e.HasOne(g => g.Match).WithMany().HasForeignKey(g => g.MatchId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(g => new { g.MatchId, g.GraphicKey }).IsUnique();
            e.Property(g => g.GraphicKey).HasMaxLength(50);
        });
        b.Entity<AuditLog>().HasIndex(a => new { a.MatchId, a.CreatedAt });
    }
}


