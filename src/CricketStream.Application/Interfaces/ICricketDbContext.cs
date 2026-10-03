using CricketStream.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace CricketStream.Application.Interfaces;
public interface ICricketDbContext {
    DbSet<Team> Teams { get; }
    DbSet<Player> Players { get; }
    DbSet<Tournament> Tournaments { get; }
    DbSet<Venue> Venues { get; }
    DbSet<MatchRules> MatchRules { get; }
    DbSet<OverlayTheme> OverlayThemes { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Match> Matches { get; }
    DbSet<MatchPlayer> MatchPlayers { get; }
    DbSet<Innings> Innings { get; }
    DbSet<Over> Overs { get; }
    DbSet<Ball> Balls { get; }
    DbSet<Dismissal> Dismissals { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<OverlayGraphicState> OverlayGraphicStates { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
