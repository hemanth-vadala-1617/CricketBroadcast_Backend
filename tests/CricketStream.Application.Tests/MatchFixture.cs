using CricketStream.Application;
using CricketStream.Application.Common;
using CricketStream.Application.Features.Graphics;
using CricketStream.Application.Features.Matches;
using CricketStream.Application.Features.Revisions;
using CricketStream.Application.Features.Scoring;
using CricketStream.Application.Interfaces;
using CricketStream.Contracts.Matches;
using CricketStream.Contracts.Revisions;
using CricketStream.Contracts.Scoring;
using CricketStream.Domain.Entities;
using CricketStream.Domain.Enums;
using CricketStream.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CricketStream.Application.Tests;

public sealed class FakeBroadcaster : IMatchBroadcaster
{
    public List<MatchStateDto> Sent { get; } = new();
    public Task BroadcastStateAsync(Guid matchId, MatchStateDto state) { Sent.Add(state); return Task.CompletedTask; }
}

/// <summary>A real match driven through the real services (in-memory EF), India batting first, Australia bowling.
/// One DI scope per fixture = one "request", so tracked entities behave simply.</summary>
public sealed class MatchFixture : IDisposable
{
    private ServiceProvider _root = null!;
    private IServiceScope _scope = null!;
    private IServiceProvider Sp => _scope.ServiceProvider;

    public FakeBroadcaster Broadcaster { get; } = new();
    public Guid MatchId { get; private set; }
    public Team India { get; private set; } = null!;
    public Team Australia { get; private set; } = null!;
    public List<Player> IndiaXI { get; } = new();
    public List<Player> AusXI { get; } = new();
    public MatchStateDto Last { get; private set; } = null!;
    public const string User = "scorer-1";

    public IMatchService Matches => Sp.GetRequiredService<IMatchService>();
    public IScoringService Scoring => Sp.GetRequiredService<IScoringService>();
    public IRevisionService Revisions => Sp.GetRequiredService<IRevisionService>();
    public IGraphicsService Graphics => Sp.GetRequiredService<IGraphicsService>();
    public CricketDbContext Db => Sp.GetRequiredService<CricketDbContext>();

    public static MatchRules T20Rules() => new() { Name = "T20", OversPerInnings = 20, MaxOversPerBowler = 4, PowerplayOvers = 6 };
    public static MatchRules TestRules(int followOnMargin = 200) => new()
    {
        Name = "Test", IsTest = true, OversPerInnings = 0, MaxOversPerBowler = 0, PowerplayOvers = 0, FreeHitEnabled = false,
        FollowOnEnabled = true, FollowOnMargin = followOnMargin, InningsPerSide = 2, Days = 5, OversPerDay = 90, SessionsPerDay = 3, NewBallAfterOvers = 80,
    };

    public static async Task<MatchFixture> CreateAsync(MatchRules? rules = null, bool startInnings = true)
    {
        var f = new MatchFixture();
        var sc = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        sc.AddDbContext<CricketDbContext>(o => o.UseInMemoryDatabase(dbName));
        sc.AddScoped<ICricketDbContext>(sp => sp.GetRequiredService<CricketDbContext>());
        sc.AddSingleton<IMatchBroadcaster>(f.Broadcaster);
        sc.AddApplication();
        f._root = sc.BuildServiceProvider(validateScopes: true);
        f._scope = f._root.CreateScope();
        await f.SetupAsync(rules ?? T20Rules(), startInnings);
        return f;
    }

    private async Task SetupAsync(MatchRules rules, bool startInnings)
    {
        var db = Db;
        India = new Team { Name = "India", ShortName = "IND" };
        Australia = new Team { Name = "Australia", ShortName = "AUS" };
        db.Teams.AddRange(India, Australia);
        for (var i = 1; i <= 11; i++)
        {
            IndiaXI.Add(new Player { DisplayName = $"Ind {i}", ShortName = $"I{i}", CurrentTeam = India, PlayerRole = PlayerRole.Batter });
            AusXI.Add(new Player { DisplayName = $"Aus {i}", ShortName = $"A{i}", CurrentTeam = Australia, PlayerRole = i >= 7 ? PlayerRole.Bowler : PlayerRole.Batter });
        }
        db.Players.AddRange(IndiaXI); db.Players.AddRange(AusXI);
        db.MatchRules.Add(rules);
        var tour = new Tournament { Name = "T", ShortName = "T" };
        var venue = new Venue { Name = "V" };
        db.Tournaments.Add(tour); db.Venues.Add(venue);
        await db.SaveChangesAsync();

        var created = await Matches.CreateAsync(new CreateMatchRequest
        {
            TournamentId = tour.Id, HomeTeamId = India.Id, AwayTeamId = Australia.Id, VenueId = venue.Id, MatchRulesId = rules.Id,
            MatchFormat = rules.IsTest ? "TEST" : "T20",
        });
        Assert.True(created.Succeeded, created.Error?.Message);
        MatchId = created.Value!.Id;

        Ok(await Matches.SetSquadAsync(MatchId, new SetSquadRequest { TeamId = India.Id, Players = Squad(IndiaXI) }));
        Ok(await Matches.SetSquadAsync(MatchId, new SetSquadRequest { TeamId = Australia.Id, Players = Squad(AusXI) }));
        Ok(await Matches.RecordTossAsync(MatchId, new TossRequest { TossWinnerTeamId = Australia.Id, TossDecision = "Bowl" }));
        Ok(await Matches.StartMatchAsync(MatchId));
        if (startInnings) await StartInningsAsync();
    }

    private static List<SquadPlayerRequest> Squad(List<Player> xi) =>
        xi.Select((p, i) => new SquadPlayerRequest { PlayerId = p.Id, IsPlayingXI = true, BattingOrder = i + 1, IsCaptain = i == 0, IsWicketKeeper = i == 4 }).ToList();

    // ---- helpers -----------------------------------------------------------------------------------------------
    public MatchStateDto Ok(Result<MatchStateDto> r)
    {
        Assert.True(r.Succeeded, r.Error?.Message);
        Last = r.Value!;
        return Last;
    }

    public async Task<MatchStateDto> StartInningsAsync(Guid? battingTeam = null, int strikerIdx = 0, int nonStrikerIdx = 1, int bowlerIdx = 10)
    {
        var st = (await Matches.GetStateAsync(MatchId))!;
        var nextBat = battingTeam ?? st.Actions.NextBattingTeamId!.Value;
        var batting = nextBat == India.Id ? IndiaXI : AusXI;
        var bowling = nextBat == India.Id ? AusXI : IndiaXI;
        return Ok(await Matches.StartInningsAsync(MatchId, new StartInningsRequest
        {
            BattingTeamId = nextBat, StrikerId = batting[strikerIdx].Id, NonStrikerId = batting[nonStrikerIdx].Id, BowlerId = bowling[bowlerIdx].Id,
        }));
    }

    public static ScoreBallRequest Req(int bat = 0, string extras = "None", int extra = 0, bool boundary = false,
        WicketRequest? wicket = null, string secondary = "None", Guid? id = null) => new()
    {
        ClientRequestId = id ?? Guid.NewGuid(), RunsBat = bat, ExtrasType = extras, ExtraRuns = extra, IsBoundary = boundary,
        Wicket = wicket, SecondaryExtrasType = secondary,
    };

    public Task<Result<MatchStateDto>> TryBall(int bat = 0, string extras = "None", int extra = 0, bool boundary = false,
        WicketRequest? wicket = null, string secondary = "None", Guid? id = null) =>
        Scoring.ScoreBallAsync(MatchId, Req(bat, extras, extra, boundary, wicket, secondary, id), User);

    public async Task<MatchStateDto> Ball(int bat = 0, string extras = "None", int extra = 0, bool boundary = false,
        WicketRequest? wicket = null, string secondary = "None", Guid? id = null) =>
        Ok(await TryBall(bat, extras, extra, boundary, wicket, secondary, id));

    /// <summary>Wicket whose incoming batter is the next one in the XI.</summary>
    public WicketRequest Wkt(string type, Guid? dismissed = null, Guid? fielder = null, bool newBatter = true) => new()
    {
        DismissalType = type, DismissedPlayerId = dismissed, FielderId = fielder,
        NewBatterId = newBatter ? Last.Actions.AvailableBatters.FirstOrDefault()?.Id : null,
    };

    public async Task<MatchStateDto> Dots(int count) { for (var i = 0; i < count; i++) await Ball(); return Last; }

    public async Task<MatchStateDto> NewBowler(Player p, bool replace = false) =>
        Ok(await Scoring.NewBowlerAsync(MatchId, new NewBowlerRequest { PlayerId = p.Id, ReplaceMidOver = replace }));

    /// <summary>Bowls a full maiden over and (unless the innings ended) selects the next bowler.</summary>
    public async Task<MatchStateDto> MaidenOver(Player nextBowler)
    {
        await Dots(6);
        return Last.Actions.AwaitingNewBowler ? await NewBowler(nextBowler) : Last;
    }

    public async Task<MatchStateDto> Undo(string reason = "test") => Ok(await Revisions.UndoLastBallAsync(MatchId, new UndoBallRequest { Reason = reason }, User));

    public void Dispose() { _scope.Dispose(); _root.Dispose(); }
}
