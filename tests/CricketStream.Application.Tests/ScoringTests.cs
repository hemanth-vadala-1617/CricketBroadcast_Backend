using CricketStream.Application.Common;
using CricketStream.Contracts.Matches;
using CricketStream.Contracts.Scoring;
using CricketStream.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CricketStream.Application.Tests;

public class ScoringTests
{
    // ---------- the replay: exact totals, figures, strike, timeline ----------
    [Fact]
    public async Task Replay_of_a_first_over_gives_exact_totals_figures_and_strike()
    {
        using var f = await MatchFixture.CreateAsync();
        var i1 = f.IndiaXI[0]; var i2 = f.IndiaXI[1]; var bowler = f.AusXI[10];

        var s = await f.Ball(1);                         // single: strike rotates
        Assert.Equal(i2.Id, s.Innings!.Striker!.PlayerId);
        s = await f.Ball(4, boundary: true);             // four by i2
        Assert.Equal(5, s.Innings!.Runs);
        s = await f.Ball(extras: "Wide");                // wide: not a ball, no ball faced
        Assert.Equal(6, s.Innings!.Runs); Assert.Equal(2, s.Innings.LegalBalls);   // single + four are the only legal balls so far
        s = await f.Ball(extras: "NoBall");              // no-ball: ball faced, free hit next
        Assert.True(s.Innings!.FreeHit);
        s = await f.Ball(2);                             // free hit, two runs, free hit consumed
        Assert.False(s.Innings!.FreeHit);
        s = await f.Ball(3);                             // odd => swap
        s = await f.Ball(extras: "LegBye", extra: 1);    // leg bye: legal, strike swaps
        s = await f.Ball(1);                             // 6th legal ball: over ends, strike swaps back
        Assert.Equal("1.0", s.Innings!.Overs);
        Assert.True(s.Actions.AwaitingNewBowler);
        Assert.False(s.Actions.CanScoreBall);

        // runs: 1+4+1(wd)+1(nb)+2+3+1(lb)+1 = 14
        Assert.Equal(14, s.Innings.Runs);
        var card = s.Scorecard[0];
        Assert.Equal(14, card.Runs);
        Assert.Equal("(lb 1, w 1, nb 1)", card.ExtrasText);
        var b = card.Bowling.Single(x => x.PlayerId == bowler.Id);
        Assert.Equal(13, b.Runs);                        // leg-bye (1) is not charged to the bowler
        Assert.Equal("1.0", b.Overs);
        var timeline = s.Timeline.Single();
        Assert.Equal(new[] { "1", "4", "WD", "NB", "2", "3", "LB1", "1" }, timeline.Balls.Select(x => x.Label));
        Assert.Equal(14, timeline.Runs);
        Assert.True(timeline.IsCompleted);
    }

    [Fact]
    public async Task The_scorecard_carries_the_run_rate_and_marks_the_captain_and_keeper()
    {
        using var f = await MatchFixture.CreateAsync();
        await f.Ball(1);                       // I1 (captain) takes a single
        await f.Ball(4, boundary: true);       // I2 hits a four; two legal balls, 5 runs
        var card = f.Last.Scorecard.Single();
        Assert.Equal(15m, card.RunRate);        // 5 runs off 2 balls = 15.00 an over
        var captain = card.Batting.Single(b => b.PlayerId == f.IndiaXI[0].Id);
        Assert.True(captain.IsCaptain);
        Assert.False(captain.IsWicketKeeper);
        Assert.False(card.Batting.Single(b => b.PlayerId == f.IndiaXI[1].Id).IsCaptain);
    }

    [Fact]
    public async Task A_wide_does_not_count_as_a_ball_faced_but_a_no_ball_does()
    {
        using var f = await MatchFixture.CreateAsync();
        var striker = f.IndiaXI[0].Id;
        var s = await f.Ball(extras: "Wide");
        Assert.Equal(0, s.Innings!.Striker!.Balls); Assert.Equal(0, s.Innings.LegalBalls);
        s = await f.Ball(extras: "NoBall");
        Assert.Equal(1, s.Innings!.Striker!.Balls); Assert.Equal(0, s.Innings.LegalBalls);
        Assert.Equal(striker, s.Innings.Striker.PlayerId);
    }

    // ---------- over change ----------
    [Fact]
    public async Task Next_ball_is_refused_until_a_new_bowler_is_chosen_and_the_same_bowler_cannot_repeat()
    {
        using var f = await MatchFixture.CreateAsync();
        await f.Dots(6);
        Assert.False((await f.TryBall()).Succeeded);

        var same = await f.Scoring.NewBowlerAsync(f.MatchId, new NewBowlerRequest { PlayerId = f.AusXI[10].Id });
        Assert.False(same.Succeeded);
        Assert.Equal(ErrorKind.Validation, same.Error!.Kind);

        var s = await f.NewBowler(f.AusXI[9]);
        Assert.True(s.Actions.CanScoreBall);
        Assert.Equal(f.AusXI[9].Id, s.Innings!.Bowler!.PlayerId);
    }

    [Fact]
    public async Task A_bowler_cannot_exceed_the_quota_of_four_overs_in_a_t20()
    {
        using var f = await MatchFixture.CreateAsync();
        var a = f.AusXI[10]; var b = f.AusXI[9]; var c = f.AusXI[8];
        // overs 1..8 alternate a,b,a,b,...: a bowls overs 1,3,5,7 => 4 overs
        for (var over = 1; over <= 7; over++) await f.MaidenOver(over % 2 == 1 ? b : a);
        await f.Dots(6);                                   // over 8 by b
        Assert.DoesNotContain(f.Last.Actions.EligibleBowlers, x => x.Id == a.Id);
        var r = await f.Scoring.NewBowlerAsync(f.MatchId, new NewBowlerRequest { PlayerId = a.Id });
        Assert.False(r.Succeeded);
        Assert.Contains("maximum", r.Error!.Message);
        await f.NewBowler(c);
    }

    [Fact]
    public async Task A_maiden_is_counted_and_a_run_off_the_bowler_breaks_it()
    {
        using var f = await MatchFixture.CreateAsync();
        await f.Dots(6);
        var s = await f.NewBowler(f.AusXI[9]);
        Assert.Equal(1, s.Scorecard[0].Bowling.Single(x => x.PlayerId == f.AusXI[10].Id).Maidens);
        await f.Ball(1); await f.Dots(5);
        s = await f.NewBowler(f.AusXI[10]);
        Assert.Equal(0, s.Scorecard[0].Bowling.Single(x => x.PlayerId == f.AusXI[9].Id).Maidens);
    }

    [Fact]
    public async Task Byes_do_not_break_a_maiden()
    {
        using var f = await MatchFixture.CreateAsync();
        await f.Ball(extras: "Bye", extra: 1); await f.Ball(extras: "LegBye", extra: 1); await f.Dots(4);
        var s = await f.NewBowler(f.AusXI[9]);
        Assert.Equal(1, s.Scorecard[0].Bowling.Single(x => x.PlayerId == f.AusXI[10].Id).Maidens);
        Assert.Equal(0, s.Scorecard[0].Bowling.Single(x => x.PlayerId == f.AusXI[10].Id).Runs);
    }

    // ---------- sixes in a row ----------
    [Fact]
    public async Task Consecutive_sixes_by_the_same_batter_build_a_streak_that_a_wide_does_not_break()
    {
        using var f = await MatchFixture.CreateAsync();
        var s = await f.Ball(bat: 6, boundary: true);
        Assert.Equal(1, s.LastBall!.SixStreak);
        s = await f.Ball(bat: 6, boundary: true);
        Assert.Equal(2, s.LastBall!.SixStreak);
        await f.Ball(extras: "Wide");   // a plain wide: no runs run, same batter still on strike
        s = await f.Ball(bat: 6, boundary: true);
        Assert.Equal(3, s.LastBall!.SixStreak);
        s = await f.Ball(bat: 4, boundary: true);
        Assert.Equal(0, s.LastBall!.SixStreak);
        s = await f.Ball(bat: 6, boundary: true);
        Assert.Equal(1, s.LastBall!.SixStreak);
    }

    [Fact]
    public async Task A_dot_ball_or_a_single_ends_the_streak_and_undo_restores_it()
    {
        using var f = await MatchFixture.CreateAsync();
        await f.Ball(bat: 6, boundary: true);
        await f.Ball(bat: 6, boundary: true);
        var s = await f.Ball(bat: 1);
        Assert.Equal(0, s.LastBall!.SixStreak);
        s = await f.Undo();
        Assert.Equal(2, s.LastBall!.SixStreak);
    }

    // ---------- milestones & six distance ----------
    [Fact]
    public async Task A_fifty_is_announced_once_on_the_ball_that_reaches_it()
    {
        using var f = await MatchFixture.CreateAsync();
        MatchStateDto s = null!;
        for (var i = 0; i < 6; i++) s = await f.Ball(bat: 6, boundary: true);      // over 1: A on 36, strike passes to B
        await f.NewBowler(f.AusXI[9]);
        await f.Dots(6);                                                             // over 2: B faces
        await f.NewBowler(f.AusXI[8]);                                               // over 3: A is back on strike
        s = await f.Ball(bat: 6, boundary: true); Assert.Empty(s.Milestones);       // 42
        s = await f.Ball(bat: 6, boundary: true); Assert.Empty(s.Milestones);       // 48
        s = await f.Ball(bat: 6, boundary: true);                                   // 54
        var m = Assert.Single(s.Milestones);
        Assert.Equal(("Runs", 50), (m.Kind, m.Value));
        Assert.Equal(f.IndiaXI[0].DisplayName, m.Name);
        s = await f.Ball(bat: 1);
        Assert.Empty(s.Milestones);
    }

    [Fact]
    public async Task A_maiden_over_is_announced_on_its_last_ball()
    {
        using var f = await MatchFixture.CreateAsync();
        MatchStateDto s = null!;
        for (var i = 0; i < 5; i++) { s = await f.Ball(); Assert.Empty(s.Milestones); }
        s = await f.Ball();
        var m = Assert.Single(s.Milestones);
        Assert.Equal(("Maiden", 1), (m.Kind, m.Value));
        Assert.Equal(f.AusXI[10].DisplayName, m.Name);
    }

    [Fact]
    public async Task An_over_with_runs_is_not_a_maiden()
    {
        using var f = await MatchFixture.CreateAsync();
        await f.Ball(bat: 1);
        var s = await f.Dots(5);
        Assert.Empty(s.Milestones);
    }

    [Fact]
    public async Task A_fifth_wicket_for_the_bowler_is_a_five_for()
    {
        using var f = await MatchFixture.CreateAsync();
        MatchStateDto s = null!;
        for (var i = 0; i < 4; i++) { s = await f.Ball(wicket: f.Wkt("Bowled")); Assert.DoesNotContain(s.Milestones, x => x.Kind == "FiveWickets"); }
        Assert.Contains(s.Milestones, x => x.Kind == "FourWickets" && x.Value == 4);                // the 4th wicket is a four-for
        s = await f.Ball(wicket: f.Wkt("Bowled"));
        Assert.Contains(s.Milestones, x => x.Kind == "FiveWickets" && x.Value == 5);
    }

    [Fact]
    public async Task Six_distance_attaches_to_the_last_ball_only_when_it_was_a_six()
    {
        using var f = await MatchFixture.CreateAsync();
        var notSix = await f.Scoring.SetSixDistanceAsync(f.MatchId, new SixDistanceRequest { Meters = 90 });
        Assert.False(notSix.Succeeded);
        await f.Ball(bat: 6, boundary: true);
        var ok = await f.Scoring.SetSixDistanceAsync(f.MatchId, new SixDistanceRequest { Meters = 94 });
        Assert.True(ok.Succeeded);
        Assert.Equal(94, ok.Value!.LastBall!.SixDistance);
        Assert.False((await f.Scoring.SetSixDistanceAsync(f.MatchId, new SixDistanceRequest { Meters = 500 })).Succeeded);
        var cleared = await f.Scoring.SetSixDistanceAsync(f.MatchId, new SixDistanceRequest { Meters = null });
        Assert.Null(cleared.Value!.LastBall!.SixDistance);
        var next = await f.Ball(bat: 1);
        Assert.Null(next.LastBall!.SixDistance);
    }

    // ---------- hat-tricks ----------
    [Fact]
    public async Task Three_fours_in_a_row_by_one_batter_is_a_four_streak_of_three()
    {
        using var f = await MatchFixture.CreateAsync();
        var s = await f.Ball(bat: 4, boundary: true); Assert.Equal(1, s.LastBall!.FourStreak);
        s = await f.Ball(bat: 4, boundary: true); Assert.Equal(2, s.LastBall!.FourStreak);
        await f.Ball(extras: "Wide");
        s = await f.Ball(bat: 4, boundary: true); Assert.Equal(3, s.LastBall!.FourStreak);
        s = await f.Ball(bat: 6, boundary: true);
        Assert.Equal(0, s.LastBall!.FourStreak);
    }

    [Fact]
    public async Task Three_wickets_in_three_deliveries_by_one_bowler_is_a_hat_trick()
    {
        using var f = await MatchFixture.CreateAsync();
        var s = await f.Ball(wicket: f.Wkt("Bowled")); Assert.Equal(1, s.LastBall!.WicketStreak);
        s = await f.Ball(wicket: f.Wkt("Bowled")); Assert.Equal(2, s.LastBall!.WicketStreak);
        await f.Ball(extras: "Wide");                                                   // a wide is not a delivery that breaks it
        s = await f.Ball(wicket: f.Wkt("Caught", fielder: f.AusXI[0].Id)); Assert.Equal(3, s.LastBall!.WicketStreak);
        s = await f.Ball(bat: 1);
        Assert.Equal(0, s.LastBall!.WicketStreak);
    }

    [Fact]
    public async Task A_dot_ball_or_a_run_out_breaks_the_wicket_streak()
    {
        using var f = await MatchFixture.CreateAsync();
        await f.Ball(wicket: f.Wkt("Bowled"));
        await f.Ball();
        var s = await f.Ball(wicket: f.Wkt("Bowled"));
        Assert.Equal(1, s.LastBall!.WicketStreak);
        s = await f.Ball(wicket: f.Wkt("RunOut", fielder: f.AusXI[0].Id));
        Assert.Equal(0, s.LastBall!.WicketStreak);                                      // a run out is not the bowler's wicket
    }

    // ---------- BBL Power Surge ----------
    private static MatchRules BblRules() => new() { Name = "BBL", OversPerInnings = 20, MaxOversPerBowler = 0, PowerplayOvers = 6, PowerSurgeEnabled = true, PowerSurgeOvers = 2, PowerSurgeFromOver = 11, PowerSurgeToOver = 16 };

    private static async Task<MatchStateDto> BowlOvers(MatchFixture f, int overs)
    {
        MatchStateDto s = f.Last;
        for (var o = 0; o < overs; o++) { s = await f.Dots(6); s = await f.NewBowler(f.AusXI[o % 2 == 0 ? 9 : 10]); }
        return s;
    }

    [Fact]
    public async Task With_bbl_rules_the_power_surge_cannot_start_before_over_eleven()
    {
        using var f = await MatchFixture.CreateAsync(BblRules());
        var ps = f.Last.Innings!.PowerSurge!;
        Assert.False(ps.CanStart); Assert.Equal(11, ps.EarliestOver); Assert.Equal(16, ps.LatestOver);
        Assert.False((await f.Scoring.PowerSurgeAsync(f.MatchId, new PowerSurgeRequest { Start = true })).Succeeded);
    }

    [Fact]
    public async Task In_a_plain_t20_the_power_surge_can_be_started_any_time_once_for_two_overs()
    {
        using var f = await MatchFixture.CreateAsync();
        var ps = f.Last.Innings!.PowerSurge!;
        Assert.True(ps.CanStart); Assert.Equal(2, ps.Overs);
        var r = await f.Scoring.PowerSurgeAsync(f.MatchId, new PowerSurgeRequest { Start = true });
        Assert.True(r.Succeeded);
        Assert.True(r.Value!.Innings!.PowerSurge!.Active);                   // between overs: on straight away
        Assert.Equal(1, r.Value.Innings.PowerSurge.StartOver);
        Assert.False((await f.Scoring.PowerSurgeAsync(f.MatchId, new PowerSurgeRequest { Start = true })).Succeeded);
    }

    [Fact]
    public async Task A_test_match_has_no_power_surge()
    {
        using var f = await MatchFixture.CreateAsync(MatchFixture.TestRules());
        Assert.Null(f.Last.Innings!.PowerSurge);
        Assert.False((await f.Scoring.PowerSurgeAsync(f.MatchId, new PowerSurgeRequest { Start = true })).Succeeded);
    }

    [Fact]
    public async Task Started_in_the_middle_of_an_over_the_surge_waits_for_the_next_over_and_can_be_cancelled_until_then()
    {
        using var f = await MatchFixture.CreateAsync();
        await f.Dots(2);
        var r = await f.Scoring.PowerSurgeAsync(f.MatchId, new PowerSurgeRequest { Start = true });
        var ps = r.Value!.Innings!.PowerSurge!;
        Assert.True(ps.Pending); Assert.False(ps.Active); Assert.Equal(2, ps.StartOver); Assert.True(ps.CanCancel);
        await f.Dots(3); var s = await f.Dots(1);                            // over 1 is complete (a new bowler is needed)
        s = await f.NewBowler(f.AusXI[9]);
        Assert.True(s.Innings!.PowerSurge!.Active); Assert.False(s.Innings.PowerSurge.Pending); Assert.Equal(2, s.Innings.PowerSurge.OversLeft);
        var cancelled = await f.Scoring.PowerSurgeAsync(f.MatchId, new PowerSurgeRequest { Start = false });
        Assert.True(cancelled.Succeeded);                                    // nothing bowled in it yet
        Assert.True(cancelled.Value!.Innings!.PowerSurge!.CanStart);
    }

    [Fact]
    public async Task Power_surge_runs_for_two_overs_then_ends_and_can_only_be_taken_once()
    {
        using var f = await MatchFixture.CreateAsync(BblRules());
        var s = await BowlOvers(f, 10);                                  // ten overs done, between overs
        Assert.True(s.Innings!.PowerSurge!.CanStart);
        var r = await f.Scoring.PowerSurgeAsync(f.MatchId, new PowerSurgeRequest { Start = true });
        Assert.True(r.Succeeded);
        var ps = r.Value!.Innings!.PowerSurge!;
        Assert.True(ps.Active); Assert.Equal(11, ps.StartOver); Assert.Equal(2, ps.OversLeft);
        Assert.False((await f.Scoring.PowerSurgeAsync(f.MatchId, new PowerSurgeRequest { Start = true })).Succeeded);   // once only

        s = await f.Ball();                                              // a ball into over 11: mid-over, so no cancel
        Assert.False(s.Innings!.PowerSurge!.CanCancel);
        await f.Dots(5); s = await f.NewBowler(f.AusXI[9]);
        Assert.True(s.Innings!.PowerSurge!.Active); Assert.Equal(1, s.Innings.PowerSurge.OversLeft);
        await f.Dots(6); s = await f.NewBowler(f.AusXI[10]);
        Assert.False(s.Innings!.PowerSurge!.Active); Assert.True(s.Innings.PowerSurge.Used);
    }

    [Fact]
    public async Task Power_surge_can_be_cancelled_before_a_ball_is_bowled_in_it()
    {
        using var f = await MatchFixture.CreateAsync(BblRules());
        await BowlOvers(f, 10);
        var started = await f.Scoring.PowerSurgeAsync(f.MatchId, new PowerSurgeRequest { Start = true });
        Assert.True(started.Value!.Innings!.PowerSurge!.CanCancel);
        var r = await f.Scoring.PowerSurgeAsync(f.MatchId, new PowerSurgeRequest { Start = false });
        Assert.True(r.Succeeded);
        Assert.True(r.Value!.Innings!.PowerSurge!.CanStart);
        Assert.False(r.Value.Innings.PowerSurge.Used);
    }

    // ---------- ducks ----------
    [Fact]
    public async Task Out_to_the_first_ball_faced_for_nought_is_a_golden_duck()
    {
        using var f = await MatchFixture.CreateAsync();
        var s = await f.Ball(wicket: f.Wkt("Bowled"));
        Assert.Equal("Golden", s.LastBall!.DuckType);
        Assert.Equal(f.IndiaXI[0].DisplayName, s.LastBall.DuckName);
    }

    [Fact]
    public async Task Out_for_nought_after_facing_more_balls_is_a_plain_duck_and_runs_make_it_none()
    {
        using var f = await MatchFixture.CreateAsync();
        await f.Dots(2);
        var s = await f.Ball(wicket: f.Wkt("Bowled"));
        Assert.Equal("Duck", s.LastBall!.DuckType);                          // the striker faced 3 balls, no runs

        using var g = await MatchFixture.CreateAsync();
        await g.Ball(bat: 1); await g.Ball(bat: 1);                          // striker scores, then they swap back
        var t = await g.Ball(wicket: g.Wkt("Bowled"));
        Assert.Null(t.LastBall!.DuckType);                                   // he had runs
    }

    [Fact]
    public async Task A_non_striker_run_out_before_facing_a_ball_is_a_diamond_duck()
    {
        using var f = await MatchFixture.CreateAsync();
        var nonStriker = f.IndiaXI[1].Id;
        var s = await f.Ball(wicket: f.Wkt("RunOut", dismissed: nonStriker, fielder: f.AusXI[0].Id));
        Assert.Equal("Diamond", s.LastBall!.DuckType);
    }

    [Fact]
    public async Task A_ball_with_no_wicket_has_no_duck()
    {
        using var f = await MatchFixture.CreateAsync();
        var s = await f.Ball();
        Assert.Null(s.LastBall!.DuckType);
    }

    // ---------- breaks ----------
    [Theory]
    [InlineData("StrategicTimeout", "Strategic timeout", 180)]
    [InlineData("DrinksBreak", "Drinks break", 300)]
    [InlineData("InjuryBreak", "Injury break", 0)]
    [InlineData("BadLight", "Bad light", 0)]
    public async Task A_break_pauses_play_with_its_label_and_usual_length_and_resume_ends_it(string kind, string label, int seconds)
    {
        using var f = await MatchFixture.CreateAsync();
        var r = await f.Matches.StartBreakAsync(f.MatchId, new BreakRequest { Kind = kind });
        Assert.True(r.Succeeded, r.Error?.Message);
        Assert.Equal(label, r.Value!.Break!.Label);
        Assert.Equal(seconds, r.Value.Break.PlannedSeconds);
        Assert.False(r.Value.Actions.CanScoreBall);                          // scoring is locked during a break
        var back = await f.Matches.ResumeAsync(f.MatchId);
        Assert.Null(back.Value!.Break);
        Assert.Equal("Live", back.Value.Status);
        Assert.True(back.Value.Actions.CanScoreBall);
    }

    [Fact]
    public async Task Rain_delay_keeps_its_status_and_a_custom_length_is_used()
    {
        using var f = await MatchFixture.CreateAsync();
        var r = await f.Matches.StartBreakAsync(f.MatchId, new BreakRequest { Kind = "RainDelay", Minutes = 45 });
        Assert.Equal("RainDelay", r.Value!.Status);
        Assert.Equal(45 * 60, r.Value.Break!.PlannedSeconds);
        Assert.Equal("RainDelay", r.Value.Break.Kind);
    }

    [Fact]
    public async Task Lunch_and_tea_are_test_breaks_and_the_strategic_timeout_is_not()
    {
        using var t20 = await MatchFixture.CreateAsync();
        Assert.False((await t20.Matches.StartBreakAsync(t20.MatchId, new BreakRequest { Kind = "Lunch" })).Succeeded);
        Assert.False((await t20.Matches.StartBreakAsync(t20.MatchId, new BreakRequest { Kind = "Stumps" })).Succeeded);
        using var test = await MatchFixture.CreateAsync(MatchFixture.TestRules());
        Assert.True((await test.Matches.StartBreakAsync(test.MatchId, new BreakRequest { Kind = "Lunch" })).Succeeded);
        await test.Matches.ResumeAsync(test.MatchId);
        Assert.False((await test.Matches.StartBreakAsync(test.MatchId, new BreakRequest { Kind = "StrategicTimeout" })).Succeeded);
    }

    [Fact]
    public async Task An_unknown_break_or_a_silly_length_is_rejected()
    {
        using var f = await MatchFixture.CreateAsync();
        Assert.False((await f.Matches.StartBreakAsync(f.MatchId, new BreakRequest { Kind = "Nap" })).Succeeded);
        Assert.False((await f.Matches.StartBreakAsync(f.MatchId, new BreakRequest { Kind = "Tea", Minutes = 9999 })).Succeeded);
        Assert.Null(f.Last.Break);
    }

    // ---------- wickets ----------
    [Fact]
    public async Task Bowled_credits_the_bowler_and_brings_in_the_next_batter_on_strike()
    {
        using var f = await MatchFixture.CreateAsync();
        var s = await f.Ball(wicket: f.Wkt("Bowled"));
        Assert.Equal(1, s.Innings!.Wickets);
        Assert.Equal(f.IndiaXI[2].Id, s.Innings.Striker!.PlayerId);
        Assert.Equal(1, s.Scorecard[0].Bowling[0].Wickets);
        Assert.Equal("b Aus 11", s.Scorecard[0].Batting.Single(x => x.PlayerId == f.IndiaXI[0].Id).DismissalText);
        Assert.Equal("1-0", $"{s.Scorecard[0].FallOfWickets[0].WicketNumber}-{s.Scorecard[0].FallOfWickets[0].Score}");
        Assert.Equal(0, s.Innings.Partnership.Runs);
    }

    [Fact]
    public async Task Run_out_does_not_credit_the_bowler_and_may_dismiss_the_non_striker()
    {
        using var f = await MatchFixture.CreateAsync();
        var nonStriker = f.IndiaXI[1].Id;
        var s = await f.Ball(1, wicket: f.Wkt("RunOut", dismissed: nonStriker));
        Assert.Equal(1, s.Innings!.Wickets);
        Assert.Equal(0, s.Scorecard[0].Bowling[0].Wickets);
        Assert.Equal("out", s.Scorecard[0].Batting.Single(x => x.PlayerId == nonStriker).Status);
        // one run was completed so the batters crossed: the original striker is now at the non-striker's end
        Assert.Equal(f.IndiaXI[0].Id, s.Innings.NonStriker!.PlayerId);
        Assert.Equal(f.IndiaXI[2].Id, s.Innings.Striker!.PlayerId);
    }

    [Fact]
    public async Task Caught_needs_a_fielder_and_a_non_striker_cannot_be_bowled()
    {
        using var f = await MatchFixture.CreateAsync();
        var r = await f.TryBall(wicket: f.Wkt("Caught"));
        Assert.False(r.Succeeded);
        r = await f.TryBall(wicket: f.Wkt("Bowled", dismissed: f.IndiaXI[1].Id));
        Assert.False(r.Succeeded);
        var s = await f.Ball(wicket: f.Wkt("Caught", fielder: f.AusXI[3].Id));
        Assert.Equal("c Aus 4 b Aus 11", s.Scorecard[0].Batting.Single(x => x.PlayerId == f.IndiaXI[0].Id).DismissalText);
    }

    [Fact]
    public async Task An_incoming_batter_must_be_unused_and_in_the_batting_xi()
    {
        using var f = await MatchFixture.CreateAsync();
        var atCrease = new WicketRequest { DismissalType = "Bowled", NewBatterId = f.IndiaXI[1].Id };
        Assert.False((await f.TryBall(wicket: atCrease)).Succeeded);
        var wrongTeam = new WicketRequest { DismissalType = "Bowled", NewBatterId = f.AusXI[0].Id };
        Assert.False((await f.TryBall(wicket: wrongTeam)).Succeeded);
    }

    [Fact]
    public async Task A_wicket_without_a_named_batter_waits_for_new_batter_and_scoring_is_blocked()
    {
        using var f = await MatchFixture.CreateAsync();
        var s = await f.Ball(wicket: f.Wkt("Bowled", newBatter: false));
        Assert.True(s.Actions.AwaitingNewBatter);
        Assert.False((await f.TryBall()).Succeeded);
        s = f.Ok(await f.Scoring.NewBatterAsync(f.MatchId, new NewBatterRequest { PlayerId = f.IndiaXI[2].Id }));
        Assert.False(s.Actions.AwaitingNewBatter);
        Assert.True(s.Actions.CanScoreBall);
    }

    // ---------- free hit ----------
    [Fact]
    public async Task Free_hit_rejects_bowled_survives_a_wide_and_ends_on_the_next_legal_ball()
    {
        using var f = await MatchFixture.CreateAsync();
        var s = await f.Ball(extras: "NoBall");
        Assert.True(s.Innings!.FreeHit);
        Assert.False((await f.TryBall(wicket: f.Wkt("Bowled"))).Succeeded);
        s = await f.Ball(extras: "Wide");
        Assert.True(s.Innings!.FreeHit);                  // a wide does not use up the free hit
        s = await f.Ball(wicket: f.Wkt("RunOut", dismissed: f.IndiaXI[0].Id));   // run-out is allowed on a free hit
        Assert.Equal(1, s.Innings!.Wickets);
        Assert.False(s.Innings.FreeHit);
    }

    // ---------- idempotency ----------
    [Fact]
    public async Task A_retried_request_does_not_create_a_second_ball()
    {
        using var f = await MatchFixture.CreateAsync();
        var id = Guid.NewGuid();
        var first = await f.Ball(4, boundary: true, id: id);
        var retry = await f.Ball(4, boundary: true, id: id);
        Assert.Equal(first.Innings!.Runs, retry.Innings!.Runs);
        Assert.Equal(4, retry.Innings.Runs);
        Assert.Equal(1, await f.Db.Balls.CountAsync());
    }

    // ---------- undo / edit ----------
    [Fact]
    public async Task Undo_restores_runs_strike_bowler_and_the_wicket()
    {
        using var f = await MatchFixture.CreateAsync();
        await f.Ball(1);
        var before = f.Last.Innings!;
        await f.Ball(wicket: f.Wkt("Bowled"));
        var s = await f.Undo();
        Assert.Equal(0, s.Innings!.Wickets);
        Assert.Equal(before.Runs, s.Innings.Runs);
        Assert.Equal(before.Striker!.PlayerId, s.Innings.Striker!.PlayerId);
        Assert.Equal(before.NonStriker!.PlayerId, s.Innings.NonStriker!.PlayerId);
        Assert.Equal(before.Striker.Runs, s.Innings.Striker.Runs);
        Assert.Single(await f.Db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task Undo_can_walk_back_through_an_over_boundary()
    {
        using var f = await MatchFixture.CreateAsync();
        await f.Dots(5); await f.Ball(1);                         // over 1 ends on a single
        await f.NewBowler(f.AusXI[9]);
        await f.Ball(2);
        var s = await f.Undo();                                   // undo the ball of over 2
        Assert.Equal("1.0", s.Innings!.Overs);
        s = await f.Undo();                                       // undo the last ball of over 1: over 1 is open again
        Assert.Equal("0.5", s.Innings!.Overs);
        Assert.Equal(f.AusXI[10].Id, s.Innings.Bowler!.PlayerId);
        Assert.False(s.Actions.AwaitingNewBowler);
        Assert.Single(s.Timeline);                                // the empty second over is not shown
    }

    [Fact]
    public async Task Editing_the_last_ball_after_an_undo_stays_in_the_right_over()
    {
        // Regression: after undoing the over-ending ball, an empty next over still exists. The replacement
        // ball must go back into over 1, not into the empty over 2.
        using var f = await MatchFixture.CreateAsync();
        await f.Dots(5); await f.Ball(1);
        await f.NewBowler(f.AusXI[9]);
        await f.Ball(1);
        await f.Undo();                                           // over 2's only ball
        var s = f.Ok(await f.Revisions.EditLastBallAsync(f.MatchId, new Contracts.Revisions.EditBallRequest
        {
            Reason = "it was a dot", Ball = MatchFixture.Req(0),
        }, MatchFixture.User));
        Assert.Equal("1.0", s.Innings!.Overs);
        Assert.Equal(6, s.Timeline.Single(o => o.OverNumber == 1).Balls.Count);
        Assert.DoesNotContain(s.Timeline, o => o.OverNumber == 2 && o.Balls.Count > 0);
        Assert.Equal(f.AusXI[10].Id, s.Innings.Bowler!.PlayerId);
        Assert.Equal(0, s.Innings.Runs);
    }

    [Fact]
    public async Task Editing_the_last_ball_replaces_it_and_audits_before_and_after()
    {
        using var f = await MatchFixture.CreateAsync();
        await f.Ball(1);
        var s = f.Ok(await f.Revisions.EditLastBallAsync(f.MatchId, new Contracts.Revisions.EditBallRequest
        {
            Reason = "was a four", Ball = MatchFixture.Req(4, boundary: true),
        }, MatchFixture.User));
        Assert.Equal(4, s.Innings!.Runs);
        Assert.Equal(f.IndiaXI[0].Id, s.Innings.Striker!.PlayerId);     // 4 does not rotate strike (the single did)
        var audit = await f.Db.AuditLogs.SingleAsync();
        Assert.Equal("Edit", audit.ActionType);
        Assert.NotNull(audit.AfterStateJson);
        Assert.Equal(MatchFixture.User, audit.UserId);
    }

    [Fact]
    public async Task A_rejected_edit_leaves_the_ball_untouched()
    {
        using var f = await MatchFixture.CreateAsync();
        await f.Ball(2);
        var r = await f.Revisions.EditLastBallAsync(f.MatchId, new Contracts.Revisions.EditBallRequest
        {
            Reason = "bad edit", Ball = MatchFixture.Req(bat: 1, extras: "Wide"),   // runs off the bat on a wide is invalid
        }, MatchFixture.User);
        Assert.False(r.Succeeded);
        var s = (await f.Matches.GetStateAsync(f.MatchId))!;
        Assert.Equal(2, s.Innings!.Runs);
    }

    [Fact]
    public async Task Undo_needs_a_reason_and_refuses_when_nothing_was_bowled()
    {
        using var f = await MatchFixture.CreateAsync();
        Assert.False((await f.Revisions.UndoLastBallAsync(f.MatchId, new Contracts.Revisions.UndoBallRequest { Reason = "" }, MatchFixture.User)).Succeeded);
        Assert.False((await f.Revisions.UndoLastBallAsync(f.MatchId, new Contracts.Revisions.UndoBallRequest { Reason = "nothing yet" }, MatchFixture.User)).Succeeded);
    }

    [Fact]
    public async Task The_start_of_an_innings_can_be_cancelled_before_the_first_ball()
    {
        using var f = await MatchFixture.CreateAsync();
        f.Ok(await f.Revisions.CancelInningsStartAsync(f.MatchId, MatchFixture.User));
        var s = (await f.Matches.GetStateAsync(f.MatchId))!;
        Assert.Null(s.Innings);
        Assert.True(s.Actions.CanStartInnings);
        await f.StartInningsAsync();   // restart with (possibly different) openers
        await f.Ball(1);
        Assert.False((await f.Revisions.CancelInningsStartAsync(f.MatchId, MatchFixture.User)).Succeeded);
    }

    // ---------- retire / penalty / swap ----------
    [Fact]
    public async Task A_penalty_is_announced_with_a_new_id_each_time_so_the_overlay_can_show_it()
    {
        using var f = await MatchFixture.CreateAsync();
        Assert.Null(f.Last.LastPenalty);                                   // nothing yet

        var s = f.Ok(await f.Scoring.PenaltyAsync(f.MatchId, new PenaltyRequest { Runs = 5, Reason = "ball tampering" }, MatchFixture.User));
        Assert.Equal(5, s.LastPenalty!.Runs);
        var first = s.LastPenalty.Id;

        await f.Ball(1);                                                   // a ball afterwards does not change or clear it
        Assert.Equal(first, (await f.Matches.GetStateAsync(f.MatchId))!.LastPenalty!.Id);

        s = f.Ok(await f.Scoring.PenaltyAsync(f.MatchId, new PenaltyRequest { Runs = 5, Reason = "again" }, MatchFixture.User));
        Assert.Equal(5, s.LastPenalty!.Runs);                              // the amount awarded this time, not the running total (10)
        Assert.NotEqual(first, s.LastPenalty.Id);                          // a NEW award, so clients show it again
    }

    [Fact]
    public async Task A_penalty_correction_shows_nothing()
    {
        using var f = await MatchFixture.CreateAsync();
        f.Ok(await f.Scoring.PenaltyAsync(f.MatchId, new PenaltyRequest { Runs = 5 }, MatchFixture.User));
        var s = f.Ok(await f.Scoring.PenaltyAsync(f.MatchId, new PenaltyRequest { Runs = -5, Reason = "entered by mistake" }, MatchFixture.User));
        Assert.Null(s.LastPenalty);                                        // taking runs back must not flash a celebration
        Assert.Equal(0, s.Innings!.Extras);
    }
    [Fact]
    public async Task Swap_strike_retire_and_penalty_work_and_are_audited()
    {
        using var f = await MatchFixture.CreateAsync();
        await f.Ball(1);                                             // I1 takes a single, I2 now on strike
        var s = f.Ok(await f.Scoring.SwapStrikeAsync(f.MatchId));
        Assert.Equal(f.IndiaXI[0].Id, s.Innings!.Striker!.PlayerId);  // swapped back by hand

        s = f.Ok(await f.Scoring.PenaltyAsync(f.MatchId, new PenaltyRequest { Runs = 5, Reason = "ball tampering" }, MatchFixture.User));
        Assert.Equal(6, s.Innings!.Runs); Assert.Equal(5, s.Innings.Extras);
        Assert.False((await f.Scoring.PenaltyAsync(f.MatchId, new PenaltyRequest { Runs = 0 }, MatchFixture.User)).Succeeded);

        var retiring = s.Innings.Striker!.PlayerId;
        s = f.Ok(await f.Scoring.RetireAsync(f.MatchId, new RetireRequest { PlayerId = retiring, NewBatterId = f.IndiaXI[2].Id, Reason = "injury" }, MatchFixture.User));
        Assert.Equal(0, s.Innings!.Wickets);                     // retired hurt is not a wicket
        Assert.Equal("retired hurt", s.Scorecard[0].Batting.Single(x => x.PlayerId == retiring).Status);
        Assert.Contains(s.Actions.AvailableBatters, p => p.Id == retiring);   // he may return later
        Assert.Equal(2, await f.Db.AuditLogs.CountAsync());
    }

    // ---------- innings & match end ----------
    [Fact]
    public async Task All_out_ends_the_innings_and_a_chase_wins_the_match_by_wickets()
    {
        using var f = await MatchFixture.CreateAsync();
        // India collapse: 10 balls, 10 wickets (bowled). Bowler changes after 6 legal balls.
        for (var i = 0; i < 10; i++)
        {
            if (f.Last.Actions.AwaitingNewBowler) await f.NewBowler(f.AusXI[9]);
            await f.Ball(wicket: f.Wkt("Bowled"));
        }
        Assert.Equal("Completed", f.Last.Innings!.Status);
        Assert.Equal(10, f.Last.Innings.Wickets);
        Assert.Equal("InningsBreak", f.Last.Status);
        Assert.True(f.Last.Actions.CanStartInnings);
        Assert.False((await f.TryBall()).Succeeded);

        // Australia chase 1 (India scored 0): target 1
        await f.StartInningsAsync();
        Assert.Equal(1, f.Last.Innings!.Target);
        var s = await f.Ball(1);
        Assert.Equal("Completed", s.Status);
        Assert.Equal("Win", s.ResultType);
        Assert.Equal("Australia won by 10 wickets", s.ResultText);
        Assert.Contains(f.Last.PreviousInnings, p => p.Text == "1st Inng.: 0-10");
    }

    [Fact]
    public async Task A_second_innings_that_falls_short_loses_by_runs_and_one_short_is_a_tie()
    {
        using var f = await MatchFixture.CreateAsync();
        await f.Ball(6);                                                    // India 6
        f.Ok(await f.Matches.EndInningsAsync(f.MatchId, new EndInningsRequest()));
        await f.StartInningsAsync();
        Assert.Equal(7, f.Last.Innings!.Target);
        Assert.Equal(5, (await f.Ball(2)).Innings!.RunsRequired);
        f.Ok(await f.Matches.EndInningsAsync(f.MatchId, new EndInningsRequest()));   // Australia stop on 2
        Assert.Equal("India won by 4 runs", f.Last.ResultText);

        using var g = await MatchFixture.CreateAsync();
        await g.Ball(6);
        g.Ok(await g.Matches.EndInningsAsync(g.MatchId, new EndInningsRequest()));
        await g.StartInningsAsync();
        await g.Ball(6, boundary: true);                                    // 6 = target-1 -> tie when the innings ends
        g.Ok(await g.Matches.EndInningsAsync(g.MatchId, new EndInningsRequest()));
        Assert.Equal("Tie", g.Last.ResultType);
    }

    [Fact]
    public async Task The_innings_ends_when_the_overs_are_bowled()
    {
        var rules = MatchFixture.T20Rules(); rules.OversPerInnings = 2; rules.MaxOversPerBowler = 2;
        using var f = await MatchFixture.CreateAsync(rules);
        await f.MaidenOver(f.AusXI[9]);
        await f.Dots(6);
        Assert.Equal("Completed", f.Last.Innings!.Status);
        Assert.Equal("2.0", f.Last.Innings.Overs);
    }

    [Fact]
    public async Task Only_a_live_match_can_be_scored()
    {
        using var f = await MatchFixture.CreateAsync();
        f.Ok(await f.Matches.PauseAsync(f.MatchId, rain: true));
        Assert.False((await f.TryBall()).Succeeded);
        f.Ok(await f.Matches.ResumeAsync(f.MatchId));
        Assert.True((await f.TryBall()).Succeeded);
    }

    [Fact]
    public async Task Undoing_the_winning_ball_reopens_the_match()
    {
        using var f = await MatchFixture.CreateAsync();
        f.Ok(await f.Matches.EndInningsAsync(f.MatchId, new EndInningsRequest()));      // India 0 all out (ended)
        await f.StartInningsAsync();
        await f.Ball(1);                                                              // target 1 reached
        Assert.Equal("Completed", f.Last.Status);
        var s = await f.Undo();
        Assert.Equal("Live", s.Status);
        Assert.Null(s.ResultText);
        Assert.Equal("InProgress", s.Innings!.Status);
    }

    // ---------- realtime ----------
    [Fact]
    public async Task Every_command_broadcasts_a_newer_version_than_the_last()
    {
        using var f = await MatchFixture.CreateAsync();
        var start = f.Broadcaster.Sent.Count;
        await f.Ball(1); await f.Ball(2); await f.Undo();
        var versions = f.Broadcaster.Sent.Skip(start).Select(x => x.Version).ToList();
        Assert.Equal(3, versions.Count);
        Assert.True(versions.SequenceEqual(versions.OrderBy(v => v)) && versions.Distinct().Count() == 3);
    }
}


