using CricketStream.Contracts.Matches;
using CricketStream.Contracts.Scoring;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CricketStream.Application.Tests;

/// <summary>Retired / absent hurt, Super Over, rain-reduced overs and DLS targets, short runs, dead ball, penalty runs to the fielding side, and the "play must be on" guard.</summary>
public class LawsTests
{
    private static Task<CricketStream.Application.Common.Result<MatchStateDto>> Short(MatchFixture f, int bat, int shortRuns, bool boundary = false) =>
        f.Scoring.ScoreBallAsync(f.MatchId, new ScoreBallRequest { ClientRequestId = Guid.NewGuid(), RunsBat = bat, ShortRuns = shortRuns, IsBoundary = boundary }, MatchFixture.User);

    // ---------- play must be on ----------
    [Fact]
    public async Task The_strike_cannot_be_swapped_or_a_surge_started_during_a_break_but_the_next_bowler_can_be_chosen()
    {
        using var f = await MatchFixture.CreateAsync();
        await f.Dots(6);                                                                  // over done: waiting for the next bowler
        f.Ok(await f.Matches.StartBreakAsync(f.MatchId, new BreakRequest { Kind = "DrinksBreak" }));
        var swap = await f.Scoring.SwapStrikeAsync(f.MatchId);
        Assert.False(swap.Succeeded);
        Assert.False((await f.Scoring.PowerSurgeAsync(f.MatchId, new PowerSurgeRequest { Start = true })).Succeeded);
        var bowler = await f.Scoring.NewBowlerAsync(f.MatchId, new NewBowlerRequest { PlayerId = f.AusXI[9].Id });
        Assert.True(bowler.Succeeded, bowler.Error?.Message);                            // the commentators' time: selecting is allowed
        f.Ok(await f.Matches.ResumeAsync(f.MatchId));
        Assert.True((await f.Scoring.SwapStrikeAsync(f.MatchId)).Succeeded);
    }

    // ---------- retired hurt ----------
    [Fact]
    public async Task A_retired_batter_is_recorded_as_retired_hurt_even_after_the_innings_and_not_as_not_out()
    {
        using var f = await MatchFixture.CreateAsync();
        await f.Ball(1);                                                                  // Ind 1 takes a single: Ind 2 is on strike
        var r = await f.Scoring.RetireAsync(f.MatchId, new RetireRequest { PlayerId = f.IndiaXI[0].Id, NewBatterId = f.IndiaXI[2].Id, Reason = "Cramp" }, MatchFixture.User);
        Assert.True(r.Succeeded, r.Error?.Message);
        Assert.Contains(f.IndiaXI[0].Id, r.Value!.Actions.RetiredHurtIds);
        Assert.Contains(f.IndiaXI[0].Id, r.Value.Actions.AvailableBatters.Select(p => p.Id));   // not out: he may come back
        f.Ok(await f.Matches.EndInningsAsync(f.MatchId, new EndInningsRequest()));
        var card = f.Last.Scorecard[0].Batting;
        Assert.Equal("retired hurt", card.First(b => b.PlayerId == f.IndiaXI[0].Id).Status);
        Assert.Equal("not out", card.First(b => b.PlayerId == f.IndiaXI[1].Id).Status);
    }

    [Fact]
    public async Task A_retired_batter_who_never_faced_a_ball_still_appears_and_can_walk_back_in()
    {
        using var f = await MatchFixture.CreateAsync();
        f.Ok(await f.Scoring.RetireAsync(f.MatchId, new RetireRequest { PlayerId = f.IndiaXI[1].Id, NewBatterId = null }, MatchFixture.User));
        Assert.True(f.Last.Actions.AwaitingNewBatter);
        Assert.Contains(f.Last.Scorecard[0].Batting, b => b.PlayerId == f.IndiaXI[1].Id && b.Status == "retired hurt");
        f.Ok(await f.Scoring.NewBatterAsync(f.MatchId, new NewBatterRequest { PlayerId = f.IndiaXI[1].Id }));   // he returns
        Assert.Contains(f.Last.Scorecard[0].Batting, b => b.PlayerId == f.IndiaXI[1].Id && b.Status == "batting");
        Assert.Empty(f.Last.Actions.RetiredHurtIds);
    }

    [Fact]
    public async Task Undoing_the_ball_before_a_retirement_takes_the_retirement_back_too()
    {
        using var f = await MatchFixture.CreateAsync();
        await f.Ball(1);
        f.Ok(await f.Scoring.RetireAsync(f.MatchId, new RetireRequest { PlayerId = f.IndiaXI[1].Id, NewBatterId = f.IndiaXI[2].Id }, MatchFixture.User));
        Assert.Equal(1, await f.Db.InningsBatterStatuses.CountAsync());
        var s = await f.Undo();
        Assert.Equal(0, await f.Db.InningsBatterStatuses.CountAsync());
        Assert.Empty(s.Actions.RetiredHurtIds);
        Assert.Equal(f.IndiaXI[0].Id, s.Innings!.Striker!.PlayerId);                     // the crease is back as it was before the ball
        Assert.Equal(f.IndiaXI[1].Id, s.Innings.NonStriker!.PlayerId);
    }

    // ---------- absent hurt ----------
    [Fact]
    public async Task An_absent_hurt_player_cannot_bat_and_the_side_is_all_out_one_wicket_earlier()
    {
        using var f = await MatchFixture.CreateAsync();
        f.Ok(await f.Scoring.AbsentHurtAsync(f.MatchId, new AbsentHurtRequest { PlayerId = f.IndiaXI[10].Id }, MatchFixture.User));
        Assert.DoesNotContain(f.IndiaXI[10].Id, f.Last.Actions.AvailableBatters.Select(p => p.Id));
        Assert.Contains("Ind 11", f.Last.Scorecard[0].AbsentHurt);
        Assert.DoesNotContain("Ind 11", f.Last.Scorecard[0].YetToBat);
        Assert.False((await f.Scoring.NewBatterAsync(f.MatchId, new NewBatterRequest { PlayerId = f.IndiaXI[10].Id })).Succeeded);

        var nextBowler = 9;
        for (var wk = 1; wk <= 9; wk++)
        {
            if (f.Last.Actions.AwaitingNewBowler) { await f.NewBowler(f.AusXI[nextBowler]); nextBowler = nextBowler == 9 ? 10 : 9; }
            await f.Ball(wicket: f.Wkt("Bowled"));
            if (wk < 9) Assert.Equal("InProgress", f.Last.Innings!.Status);
        }
        Assert.Equal("InningsBreak", f.Last.Status);                                      // 9 down: only one batter left, so all out
        Assert.Equal(9, f.Last.Innings!.Wickets);
    }

    [Fact]
    public async Task Absent_hurt_is_refused_for_a_batter_at_the_crease_and_can_be_cleared()
    {
        using var f = await MatchFixture.CreateAsync();
        Assert.False((await f.Scoring.AbsentHurtAsync(f.MatchId, new AbsentHurtRequest { PlayerId = f.IndiaXI[0].Id }, MatchFixture.User)).Succeeded);
        f.Ok(await f.Scoring.AbsentHurtAsync(f.MatchId, new AbsentHurtRequest { PlayerId = f.IndiaXI[5].Id }, MatchFixture.User));
        f.Ok(await f.Scoring.AbsentHurtAsync(f.MatchId, new AbsentHurtRequest { PlayerId = f.IndiaXI[5].Id, Absent = false }, MatchFixture.User));
        Assert.Empty(f.Last.Actions.AbsentHurtIds);
        Assert.False((await f.Scoring.AbsentHurtAsync(f.MatchId, new AbsentHurtRequest { PlayerId = f.AusXI[0].Id }, MatchFixture.User)).Succeeded);   // not in the batting XI
    }

    // ---------- short run ----------
    [Fact]
    public async Task A_short_run_takes_runs_off_the_score_but_the_batters_still_crossed()
    {
        using var f = await MatchFixture.CreateAsync();
        var s = f.Ok(await Short(f, bat: 2, shortRuns: 1));                              // ran two, one short: 1 run credited
        Assert.Equal(1, s.Innings!.Runs);
        Assert.Equal(f.IndiaXI[0].Id, s.Innings.Striker!.PlayerId);                      // two crossings = back where they started
        s = f.Ok(await Short(f, bat: 1, shortRuns: 1));                                  // ran one, short: nothing credited, but they crossed
        Assert.Equal(1, s.Innings!.Runs);
        Assert.Equal(f.IndiaXI[1].Id, s.Innings.Striker!.PlayerId);
        Assert.Equal(2, s.Innings.LegalBalls);
    }

    [Fact]
    public async Task A_short_run_is_refused_when_it_is_more_than_was_run_or_on_a_boundary()
    {
        using var f = await MatchFixture.CreateAsync();
        Assert.False((await Short(f, bat: 1, shortRuns: 2)).Succeeded);
        Assert.False((await Short(f, bat: 4, shortRuns: 1, boundary: true)).Succeeded);
        Assert.False((await Short(f, bat: 1, shortRuns: -1)).Succeeded);
        Assert.Equal(0, f.Last.Innings!.LegalBalls);
    }

    // ---------- dead ball ----------
    [Fact]
    public async Task A_dead_ball_scores_nothing_but_is_counted_for_the_overlay()
    {
        using var f = await MatchFixture.CreateAsync();
        await f.Ball(2);
        var s = f.Ok(await f.Scoring.DeadBallAsync(f.MatchId, MatchFixture.User));
        Assert.Equal(1, s.Innings!.DeadBalls);
        Assert.Equal(2, s.Innings.Runs);
        Assert.Equal(1, s.Innings.LegalBalls);
        Assert.Equal("2", s.Timeline.Last().Balls.Last().Label);                         // no chip for the dead ball
    }

    // ---------- rain-reduced overs and DLS ----------
    private static async Task<MatchFixture> SecondInningsAsync()
    {
        var f = await MatchFixture.CreateAsync();
        await f.Ball(1);
        f.Ok(await f.Matches.EndInningsAsync(f.MatchId, new EndInningsRequest()));        // India 1
        await f.StartInningsAsync();                                                       // Australia chase 2
        return f;
    }

    [Fact]
    public async Task A_revised_target_and_overs_replace_the_original_and_the_win_says_dls()
    {
        using var f = await SecondInningsAsync();
        var s = f.Ok(await f.Scoring.ReviseInningsAsync(f.MatchId, new ReviseInningsRequest { Overs = 10, Target = 5 }, MatchFixture.User));
        Assert.Equal(10, s.Innings!.OversLimit);
        Assert.Equal(10, s.OversPerInnings);
        Assert.Equal(5, s.Innings.Target);
        Assert.True(s.Innings.IsDls);
        Assert.Equal(60, s.Innings.BallsRemaining);
        await f.Ball(4, boundary: true);
        s = await f.Ball(1);
        Assert.Equal("Completed", s.Status);
        Assert.Contains("won by 10 wickets (DLS method)", s.ResultText);
    }

    [Fact]
    public async Task Revised_overs_must_be_sensible()
    {
        using var f = await SecondInningsAsync();
        Assert.False((await f.Scoring.ReviseInningsAsync(f.MatchId, new ReviseInningsRequest { Overs = 25 }, MatchFixture.User)).Succeeded);   // more than the rules
        Assert.False((await f.Scoring.ReviseInningsAsync(f.MatchId, new ReviseInningsRequest { Overs = 0 }, MatchFixture.User)).Succeeded);
        Assert.False((await f.Scoring.ReviseInningsAsync(f.MatchId, new ReviseInningsRequest(), MatchFixture.User)).Succeeded);               // nothing to revise
        await f.Dots(6);
        await f.NewBowler(f.IndiaXI[9]);
        await f.Dots(6);                                                                                                                       // 2 overs bowled
        Assert.False((await f.Scoring.ReviseInningsAsync(f.MatchId, new ReviseInningsRequest { Overs = 1 }, MatchFixture.User)).Succeeded);    // fewer than already bowled
    }

    [Fact]
    public async Task Overs_cut_by_rain_in_the_first_innings_carry_over_to_the_chase()
    {
        using var f = await MatchFixture.CreateAsync();
        f.Ok(await f.Scoring.ReviseInningsAsync(f.MatchId, new ReviseInningsRequest { Overs = 10 }, MatchFixture.User));
        await f.Ball(1);
        f.Ok(await f.Matches.EndInningsAsync(f.MatchId, new EndInningsRequest()));
        await f.StartInningsAsync();
        Assert.Equal(10, f.Last.Innings!.OversLimit);
        Assert.True(f.Last.Innings.IsDls);
    }

    [Fact]
    public async Task A_target_cannot_be_revised_in_the_first_innings_and_bowlers_get_a_scaled_quota()
    {
        using var f = await MatchFixture.CreateAsync();
        Assert.False((await f.Scoring.ReviseInningsAsync(f.MatchId, new ReviseInningsRequest { Target = 120 }, MatchFixture.User)).Succeeded);
        f.Ok(await f.Scoring.ReviseInningsAsync(f.MatchId, new ReviseInningsRequest { Overs = 5 }, MatchFixture.User));   // 5 overs: 1 each (a fifth of the innings)
        var s = await f.Dots(6);
        s = await f.NewBowler(f.AusXI[9]);
        s = await f.Dots(6);
        Assert.DoesNotContain(f.AusXI[9].Id, s.Actions.EligibleBowlers.Select(p => p.Id));     // already bowled his one over
    }

    // ---------- Super Over ----------
    private static async Task<MatchFixture> TiedMatchAsync()
    {
        var f = await MatchFixture.CreateAsync();
        await f.Ball(1);
        f.Ok(await f.Matches.EndInningsAsync(f.MatchId, new EndInningsRequest()));        // India 1
        await f.StartInningsAsync();                                                       // Australia need 2
        await f.Ball(1);                                                                   // 1 run: level with 1 -> tied
        f.Ok(await f.Matches.EndInningsAsync(f.MatchId, new EndInningsRequest()));
        return f;
    }

    private static Task<CricketStream.Application.Common.Result<MatchStateDto>> StartSuper(MatchFixture f, bool australiaBat)
    {
        var bat = australiaBat ? f.AusXI : f.IndiaXI; var bowl = australiaBat ? f.IndiaXI : f.AusXI;
        return f.Matches.StartInningsAsync(f.MatchId, new StartInningsRequest
        {
            SuperOver = true, BattingTeamId = (australiaBat ? f.Australia : f.India).Id,
            StrikerId = bat[0].Id, NonStrikerId = bat[1].Id, BowlerId = bowl[10].Id,
        });
    }

    [Fact]
    public async Task A_tied_limited_overs_match_offers_a_super_over_started_by_the_side_that_batted_second()
    {
        using var f = await TiedMatchAsync();
        Assert.Equal("Tie", f.Last.ResultType);
        Assert.True(f.Last.Actions.CanStartSuperOver);
        Assert.Equal(f.Australia.Id, f.Last.Actions.NextBattingTeamId);
        Assert.False((await StartSuper(f, australiaBat: false)).Succeeded);               // India batted first before: Australia go first now
        var s = f.Ok(await StartSuper(f, australiaBat: true));
        Assert.Equal("Live", s.Status);
        Assert.Null(s.ResultType);
        Assert.True(s.Innings!.IsSuperOver);
        Assert.Equal(1, s.Innings.OversLimit);
        Assert.Null(s.Innings.Target);
        Assert.Null(s.Innings.PowerSurge);
    }

    [Fact]
    public async Task A_super_over_lasts_one_over_then_the_other_side_chases_and_wins_it()
    {
        using var f = await TiedMatchAsync();
        f.Ok(await StartSuper(f, australiaBat: true));
        await f.Ball(4, boundary: true);
        var s = await f.Dots(5);
        Assert.Equal("InningsBreak", s.Status);                                           // 6 balls bowled
        Assert.True(s.Actions.CanStartInnings);
        await f.StartInningsAsync();                                                      // India chase
        Assert.Equal(5, f.Last.Innings!.Target);
        Assert.True(f.Last.Innings.IsSuperOver);
        s = await f.Ball(6, boundary: true);
        Assert.Equal("Completed", s.Status);
        Assert.Equal("Win", s.ResultType);
        Assert.Contains("India won the Super Over", s.ResultText);
    }

    [Fact]
    public async Task A_super_over_innings_ends_at_two_wickets()
    {
        using var f = await TiedMatchAsync();
        f.Ok(await StartSuper(f, australiaBat: true));
        await f.Ball(wicket: f.Wkt("Bowled"));
        var s = await f.Ball(wicket: f.Wkt("Bowled"));
        Assert.Equal("InningsBreak", s.Status);
        Assert.Equal(2, s.Innings!.Wickets);
    }

    [Fact]
    public async Task A_super_over_that_ends_level_can_be_followed_by_another_with_the_batting_order_reversed()
    {
        using var f = await TiedMatchAsync();
        f.Ok(await StartSuper(f, australiaBat: true));
        await f.Ball(4, boundary: true); await f.Dots(5);                                  // Australia 4
        await f.StartInningsAsync();                                                       // India chase 5
        await f.Ball(4, boundary: true);
        var s = await f.Dots(5);                                                           // India 4: tied again
        Assert.Equal("Completed", s.Status);
        Assert.Equal("Tie", s.ResultType);
        Assert.True(s.Actions.CanStartSuperOver);
        Assert.Equal(f.India.Id, s.Actions.NextBattingTeamId);                            // India batted second, so they go first
        s = f.Ok(await StartSuper(f, australiaBat: false));
        Assert.Equal(5, s.Innings!.InningsNumber);
        Assert.Null(s.Innings.Target);
    }

    [Fact]
    public async Task Backing_out_of_the_first_super_over_innings_leaves_the_match_tied()
    {
        using var f = await TiedMatchAsync();
        f.Ok(await StartSuper(f, australiaBat: true));
        var s = f.Ok(await f.Revisions.CancelInningsStartAsync(f.MatchId, MatchFixture.User));
        Assert.Equal("Completed", s.Status);
        Assert.Equal("Tie", s.ResultType);
        Assert.True(s.Actions.CanStartSuperOver);
    }

    [Fact]
    public async Task A_super_over_is_refused_unless_the_match_is_tied_and_never_in_a_test()
    {
        using var f = await MatchFixture.CreateAsync();
        f.Ok(await f.Matches.EndInningsAsync(f.MatchId, new EndInningsRequest()));
        Assert.False((await StartSuper(f, australiaBat: true)).Succeeded);
        using var t = await MatchFixture.CreateAsync(MatchFixture.TestRules());
        Assert.False((await StartSuper(t, australiaBat: true)).Succeeded);
    }

    // ---------- penalty runs to the fielding side ----------
    [Fact]
    public async Task Penalty_runs_to_a_side_that_has_not_batted_wait_and_are_added_when_it_starts()
    {
        using var f = await MatchFixture.CreateAsync();
        await f.Ball(1);
        f.Ok(await f.Scoring.PenaltyAsync(f.MatchId, new PenaltyRequest { Runs = 5, ToFieldingSide = true }, MatchFixture.User));
        Assert.Equal(1, f.Last.Innings!.Runs);                                             // India's score is unchanged
        Assert.Equal(5, (await f.Db.Matches.AsNoTracking().FirstAsync(m => m.Id == f.MatchId)).PendingPenaltyAway);
        f.Ok(await f.Matches.EndInningsAsync(f.MatchId, new EndInningsRequest()));
        await f.StartInningsAsync();
        Assert.Equal(5, f.Last.Innings!.Runs);                                             // Australia start on 5
        Assert.Equal(2, f.Last.Innings.Target);                                            // and still chase India's 1 + 1
        Assert.Equal(0, (await f.Db.Matches.AsNoTracking().FirstAsync(m => m.Id == f.MatchId)).PendingPenaltyAway);
    }

    [Fact]
    public async Task Penalty_runs_to_the_fielding_side_after_it_has_batted_raise_the_target_of_the_chasing_side()
    {
        using var f = await SecondInningsAsync();                                          // India 1, Australia chase 2
        var s = f.Ok(await f.Scoring.PenaltyAsync(f.MatchId, new PenaltyRequest { Runs = 5, ToFieldingSide = true }, MatchFixture.User));
        Assert.Equal(7, s.Innings!.Target);
        Assert.Equal(6, s.Scorecard[0].Runs);                                              // India's innings now 1 + 5
    }
}
