using CricketStream.Domain.Entities;
using CricketStream.Domain.Enums;
using CricketStream.Domain.Rules;
using Xunit;

namespace CricketStream.Domain.Tests;

public class CricketRulesTests
{
    private static readonly MatchRules T20 = new() { OversPerInnings = 20, MaxOversPerBowler = 4, PowerplayOvers = 6 };
    private static readonly MatchRules Test = new()
    {
        IsTest = true, OversPerInnings = 0, MaxOversPerBowler = 0, PowerplayOvers = 0, InningsPerSide = 2,
        OversPerDay = 90, SessionsPerDay = 3, NewBallAfterOvers = 80, FollowOnEnabled = true, FollowOnMargin = 200, FreeHitEnabled = false,
    };

    private static DeliveryInput D(int bat = 0, int extra = 0, ExtrasType p = ExtrasType.None, ExtrasType s = ExtrasType.None,
        bool boundary = false, bool wicket = false, DismissalType? dis = null, bool freeHit = false) =>
        new(bat, extra, p, s, boundary, wicket, dis, freeHit);

    // ---------- legal ball / extras maths ----------
    [Fact] public void Plain_runs_are_legal_and_charged_to_bowler()
    {
        var o = CricketRules.Evaluate(D(bat: 2), T20);
        Assert.True(o.IsLegal); Assert.Equal(2, o.RunsBat); Assert.Equal(0, o.ExtrasTotal);
        Assert.Equal(2, o.BowlerRuns); Assert.Equal(2, o.RunsCompleted); Assert.True(o.BatterFacesBall);
    }

    [Fact] public void Wide_is_not_legal_batter_does_not_face_and_bowler_is_charged_everything()
    {
        var o = CricketRules.Evaluate(D(extra: 2, p: ExtrasType.Wide), T20);   // wide + 2 run
        Assert.False(o.IsLegal); Assert.False(o.BatterFacesBall);
        Assert.Equal(3, o.ExtrasTotal); Assert.Equal(3, o.BowlerRuns); Assert.Equal(2, o.RunsCompleted);
    }

    [Fact] public void Wide_that_goes_for_four_is_five_and_does_not_swap_strike()
    {
        var o = CricketRules.Evaluate(D(extra: 4, p: ExtrasType.Wide, boundary: true), T20);
        Assert.Equal(5, o.ExtrasTotal); Assert.Equal(0, o.RunsCompleted);
        Assert.False(CricketRules.SwapsStrike(o.RunsCompleted));
    }

    [Fact] public void No_ball_with_bat_runs_credits_batter_and_charges_bowler_for_both()
    {
        var o = CricketRules.Evaluate(D(bat: 4, p: ExtrasType.NoBall, boundary: true), T20);
        Assert.False(o.IsLegal); Assert.True(o.BatterFacesBall); Assert.True(o.IsFour);
        Assert.Equal(4, o.RunsBat); Assert.Equal(1, o.ExtrasTotal); Assert.Equal(5, o.BowlerRuns);
    }

    [Fact] public void No_ball_plus_byes_does_not_charge_the_byes_to_the_bowler()
    {
        var o = CricketRules.Evaluate(D(extra: 2, p: ExtrasType.NoBall, s: ExtrasType.Bye), T20);
        Assert.Equal(3, o.ExtrasTotal); Assert.Equal(1, o.BowlerRuns); Assert.Equal(2, o.RunsCompleted);
    }

    [Fact] public void Bye_and_legbye_are_legal_team_extras_not_bowler_runs()
    {
        foreach (var t in new[] { ExtrasType.Bye, ExtrasType.LegBye })
        {
            var o = CricketRules.Evaluate(D(extra: 3, p: t), T20);
            Assert.True(o.IsLegal); Assert.True(o.BatterFacesBall);
            Assert.Equal(3, o.ExtrasTotal); Assert.Equal(0, o.BowlerRuns); Assert.Equal(0, o.RunsBat);
        }
    }

    [Fact] public void Six_is_flagged_and_boundary_runs_never_rotate_strike()
    {
        var o = CricketRules.Evaluate(D(bat: 6), T20);
        Assert.True(o.IsSix); Assert.True(o.IsBoundary); Assert.Equal(0, o.RunsCompleted);
    }

    [Fact] public void Overthrow_five_off_the_bat_without_boundary_rotates_strike()
    {
        var o = CricketRules.Evaluate(D(bat: 5), T20);
        Assert.Equal(5, o.RunsCompleted); Assert.True(CricketRules.SwapsStrike(o.RunsCompleted));
    }

    [Theory] [InlineData(0, false)] [InlineData(1, true)] [InlineData(2, false)] [InlineData(3, true)]
    public void Odd_completed_runs_swap_strike(int runs, bool swap) => Assert.Equal(swap, CricketRules.SwapsStrike(runs));

    // ---------- validation ----------
    [Fact] public void Runs_off_the_bat_on_a_wide_are_rejected() =>
        Assert.NotNull(CricketRules.Validate(D(bat: 1, p: ExtrasType.Wide)));

    [Fact] public void Bye_with_a_wicket_of_bowled_is_rejected() =>
        Assert.NotNull(CricketRules.Validate(D(extra: 1, p: ExtrasType.Bye, wicket: true, dis: DismissalType.Bowled)));

    [Fact] public void Secondary_extras_need_a_no_ball() =>
        Assert.NotNull(CricketRules.Validate(D(extra: 1, p: ExtrasType.Wide, s: ExtrasType.Bye)));

    [Fact] public void Boundary_needs_four_or_more_runs() =>
        Assert.NotNull(CricketRules.Validate(D(bat: 2, boundary: true)));

    [Fact] public void Wicket_needs_a_dismissal_type() =>
        Assert.NotNull(CricketRules.Validate(D(wicket: true)));

    [Fact] public void Retired_hurt_cannot_be_scored_on_a_ball() =>
        Assert.NotNull(CricketRules.Validate(D(wicket: true, dis: DismissalType.RetiredHurt)));

    [Theory]
    [InlineData(DismissalType.Bowled)] [InlineData(DismissalType.Caught)] [InlineData(DismissalType.LBW)]
    [InlineData(DismissalType.Stumped)] [InlineData(DismissalType.HitWicket)]
    public void Only_run_out_handled_and_obstructing_are_allowed_on_a_free_hit(DismissalType t) =>
        Assert.NotNull(CricketRules.Validate(D(wicket: true, dis: t, freeHit: true)));

    [Theory] [InlineData(DismissalType.RunOut)] [InlineData(DismissalType.HandledBall)] [InlineData(DismissalType.ObstructingField)]
    public void Free_hit_allows_run_out_and_handled(DismissalType t) =>
        Assert.Null(CricketRules.Validate(D(wicket: true, dis: t, freeHit: true)));

    [Fact] public void Stumped_is_allowed_on_a_wide_but_bowled_is_not()
    {
        Assert.Null(CricketRules.Validate(D(p: ExtrasType.Wide, wicket: true, dis: DismissalType.Stumped)));
        Assert.NotNull(CricketRules.Validate(D(p: ExtrasType.Wide, wicket: true, dis: DismissalType.Bowled)));
    }

    [Fact] public void Only_five_dismissals_credit_the_bowler()
    {
        var credited = Enum.GetValues<DismissalType>().Where(CricketRules.CreditsBowler).ToList();
        Assert.Equal(5, credited.Count);
        Assert.DoesNotContain(DismissalType.RunOut, credited);
    }

    // ---------- numbers ----------
    [Fact] public void Overs_format_as_overs_dot_balls() { Assert.Equal("114.1", CricketRules.FormatOvers(114 * 6 + 1, 6)); Assert.Equal("0.0", CricketRules.FormatOvers(0, 6)); }
    [Fact] public void Run_rate_matches_the_screenshot() => Assert.Equal(3.11m, CricketRules.RunRate(355, 114 * 6 + 1, 6));
    [Fact] public void Strike_rate_matches_the_screenshot() { Assert.Equal(42.50m, CricketRules.StrikeRate(34, 80)); Assert.Equal(50.00m, CricketRules.StrikeRate(1, 2)); }
    [Fact] public void Economy_matches_the_screenshot() => Assert.Equal(2.35m, CricketRules.Economy(52, 22 * 6 + 1, 6));
    [Fact] public void Zero_balls_gives_zero_rates() { Assert.Equal(0m, CricketRules.RunRate(5, 0, 6)); Assert.Equal(0m, CricketRules.StrikeRate(5, 0)); }

    [Fact] public void Overs_left_today_matches_the_screenshot()
    {
        // 90 overs a day, 4.1 overs bowled => 85.5 left
        var left = CricketRules.BallsLeftToday(4 * 6 + 1, 0, Test);
        Assert.Equal("85.5", CricketRules.FormatOvers(left, 6));
    }
    [Fact] public void Lost_overs_reduce_the_day() => Assert.Equal(60 * 6, CricketRules.BallsLeftToday(0, 30, Test));
    [Fact] public void Overs_left_is_never_negative() => Assert.Equal(0, CricketRules.BallsLeftToday(999, 0, Test));

    [Fact] public void Bowler_quota_is_enforced_in_limited_overs_only()
    {
        Assert.True(CricketRules.BowlerHasQuotaLeft(3 * 6 + 5, T20));
        Assert.False(CricketRules.BowlerHasQuotaLeft(4 * 6, T20));
        Assert.True(CricketRules.BowlerHasQuotaLeft(500, Test));
    }

    [Fact] public void Innings_overs_complete_only_at_the_limit()
    {
        Assert.False(CricketRules.OversComplete(119, T20)); Assert.True(CricketRules.OversComplete(120, T20));
        Assert.False(CricketRules.OversComplete(5000, Test));
    }

    [Fact] public void Powerplay_covers_the_first_six_overs() { Assert.True(CricketRules.IsPowerplay(5, T20)); Assert.False(CricketRules.IsPowerplay(6, T20)); }

    // ---------- targets & follow-on ----------
    [Fact] public void Limited_overs_target_is_first_innings_plus_one()
    {
        Assert.Equal(181, CricketRules.ComputeTarget(T20, 2, 0, 0, 180, 1, 180));
        Assert.Null(CricketRules.ComputeTarget(T20, 1, 0, 0, 0, 0, 0));
    }

    [Fact] public void Test_has_no_target_before_the_fourth_innings()
    {
        Assert.Null(CricketRules.ComputeTarget(Test, 1, 0, 0, 0, 0, 0));
        Assert.Null(CricketRules.ComputeTarget(Test, 2, 0, 0, 300, 1, 300));
        Assert.Null(CricketRules.ComputeTarget(Test, 3, 300, 1, 250, 1, 300));   // third innings: opposition has batted once
    }

    [Fact] public void Test_fourth_innings_target_uses_both_sides_totals()
    {
        // A 300 & 250 = 550, B 200 in innings 2, B now chasing: needs 550 - 200 + 1 = 351
        Assert.Equal(351, CricketRules.ComputeTarget(Test, 4, 200, 1, 550, 2, 300));
    }

    [Fact] public void Follow_on_needs_the_margin_and_the_rule_enabled()
    {
        Assert.True(CricketRules.FollowOnAvailable(Test, 400, 200));
        Assert.False(CricketRules.FollowOnAvailable(Test, 400, 201));
        Assert.False(CricketRules.FollowOnAvailable(new MatchRules { IsTest = true, FollowOnEnabled = false, FollowOnMargin = 200 }, 500, 100));
    }
}
