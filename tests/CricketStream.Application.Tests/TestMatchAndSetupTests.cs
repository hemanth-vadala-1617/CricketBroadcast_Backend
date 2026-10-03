using System.Text.Json;
using CricketStream.Contracts.Graphics;
using CricketStream.Contracts.Matches;
using Xunit;

namespace CricketStream.Application.Tests;

public class TestMatchTests
{
    private static async Task Sixes(MatchFixture f, int count)
    {
        for (var i = 0; i < count; i++)
        {
            if (f.Last.Actions.AwaitingNewBowler) await f.NewBowler(f.AusXI[9 - (i / 6) % 2]);
            await f.Ball(6);
        }
    }

    [Fact]
    public async Task Overs_left_today_lead_text_follow_on_and_an_innings_victory()
    {
        using var f = await MatchFixture.CreateAsync(MatchFixture.TestRules(followOnMargin: 50));
        Assert.True(f.Last.IsTest);
        Assert.Equal("90.0", f.Last.TestClock!.OversLeftToday);

        await Sixes(f, 12);                                                       // India 72 in 2 overs
        Assert.Equal("88.0", f.Last.TestClock!.OversLeftToday);
        f.Ok(await f.Matches.EndInningsAsync(f.MatchId, new EndInningsRequest { Declared = true }));
        await f.StartInningsAsync();
        Assert.Equal("1st Inng.: 72-0d", f.Last.PreviousInnings.Single().Text);
        Assert.Equal(f.Australia.Id, f.Last.Innings!.BattingTeam.Id);
        Assert.Null(f.Last.Innings.Target);
        await f.Ball(1);
        Assert.Equal("AUS trail by 71 runs", f.Last.Innings!.LeadText);
        Assert.Equal(-71, f.Last.Innings.LeadRuns);
        f.Ok(await f.Matches.EndInningsAsync(f.MatchId, new EndInningsRequest()));
        Assert.True(f.Last.Actions.FollowOnAvailable);

        await f.StartInningsAsync(battingTeam: f.Australia.Id);                  // follow-on: Australia bat again
        Assert.True(f.Last.Innings!.IsFollowOn);
        Assert.Null(f.Last.Innings.Target);                                      // opposition has batted only once
        await f.Ball(0);
        f.Ok(await f.Matches.EndInningsAsync(f.MatchId, new EndInningsRequest()));

        Assert.Equal("Completed", f.Last.Status);
        Assert.Equal("India won by an innings and 71 runs", f.Last.ResultText);
        Assert.Equal(f.India.Id, f.Db.Matches.Single().WinnerTeamId);
    }

    [Fact]
    public async Task Follow_on_is_refused_when_the_lead_is_too_small()
    {
        using var f = await MatchFixture.CreateAsync(MatchFixture.TestRules(followOnMargin: 200));
        await f.Ball(6);
        f.Ok(await f.Matches.EndInningsAsync(f.MatchId, new EndInningsRequest()));
        await f.StartInningsAsync();
        f.Ok(await f.Matches.EndInningsAsync(f.MatchId, new EndInningsRequest()));
        Assert.False(f.Last.Actions.FollowOnAvailable);
        var r = await f.Matches.StartInningsAsync(f.MatchId, new StartInningsRequest
        {
            BattingTeamId = f.Australia.Id, StrikerId = f.AusXI[0].Id, NonStrikerId = f.AusXI[1].Id, BowlerId = f.IndiaXI[10].Id,
        });
        Assert.False(r.Succeeded);
    }

    [Fact]
    public async Task Fourth_innings_target_comes_from_both_sides_totals_and_reaching_it_wins()
    {
        using var f = await MatchFixture.CreateAsync(MatchFixture.TestRules());
        await f.Ball(6);                                                          // IND 6
        f.Ok(await f.Matches.EndInningsAsync(f.MatchId, new EndInningsRequest()));
        await f.StartInningsAsync(); await f.Ball(2);                             // AUS 2
        f.Ok(await f.Matches.EndInningsAsync(f.MatchId, new EndInningsRequest()));
        await f.StartInningsAsync(); await f.Ball(6, boundary: true);             // IND 6 more (12 total)
        f.Ok(await f.Matches.EndInningsAsync(f.MatchId, new EndInningsRequest()));

        await f.StartInningsAsync();                                              // AUS chase
        Assert.Equal(11, f.Last.Innings!.Target);                                 // 12 - 2 + 1
        Assert.Equal("AUS need 11 runs to win", f.Last.Innings.LeadText);
        await f.Ball(6, boundary: true);
        Assert.Equal("Live", f.Last.Status);
        await f.Ball(6, boundary: true);
        Assert.Equal("Completed", f.Last.Status);
        Assert.Equal("Australia won by 10 wickets", f.Last.ResultText);
    }

    [Fact]
    public async Task Sessions_advance_through_the_day_and_a_new_day_resets_the_lost_overs()
    {
        using var f = await MatchFixture.CreateAsync(MatchFixture.TestRules());
        Assert.Equal((1, 1), (f.Last.TestClock!.Day, f.Last.TestClock.Session));
        f.Ok(await f.Matches.SetOversLostAsync(f.MatchId, new OversLostRequest { Overs = 10 }));
        Assert.Equal("80.0", f.Last.TestClock!.OversLeftToday);
        f.Ok(await f.Matches.AdvanceSessionAsync(f.MatchId)); f.Ok(await f.Matches.AdvanceSessionAsync(f.MatchId));
        Assert.Equal((1, 3), (f.Last.TestClock!.Day, f.Last.TestClock.Session));
        f.Ok(await f.Matches.AdvanceSessionAsync(f.MatchId));
        Assert.Equal((2, 1), (f.Last.TestClock!.Day, f.Last.TestClock.Session));
        Assert.Equal(0, f.Last.TestClock.OversLostToday);
        Assert.Equal("90.0", f.Last.TestClock.OversLeftToday);
    }

    [Fact]
    public async Task Overs_left_today_counts_only_balls_bowled_on_the_current_day()
    {
        using var f = await MatchFixture.CreateAsync(MatchFixture.TestRules());
        await f.Dots(6);                                                         // 1 over on day 1
        f.Ok(await f.Matches.AdvanceSessionAsync(f.MatchId));
        f.Ok(await f.Matches.AdvanceSessionAsync(f.MatchId)); f.Ok(await f.Matches.AdvanceSessionAsync(f.MatchId));
        Assert.Equal("90.0", f.Last.TestClock!.OversLeftToday);                  // day 2 starts fresh
    }

    [Fact]
    public async Task Days_and_sessions_are_refused_for_limited_overs()
    {
        using var f = await MatchFixture.CreateAsync();
        Assert.False((await f.Matches.AdvanceSessionAsync(f.MatchId)).Succeeded);
        Assert.False((await f.Matches.EndInningsAsync(f.MatchId, new EndInningsRequest { Declared = true })).Succeeded);
        Assert.Null(f.Last.TestClock);
    }

    [Fact]
    public async Task A_draw_can_be_entered_by_hand_and_closes_the_match()
    {
        using var f = await MatchFixture.CreateAsync(MatchFixture.TestRules());
        var s = f.Ok(await f.Matches.CompleteAsync(f.MatchId, new CompleteMatchRequest { ResultType = "Draw" }));
        Assert.Equal("Completed", s.Status); Assert.Equal("Match drawn", s.ResultText); Assert.Equal("Completed", s.Innings!.Status);
    }
}

public class SetupAndGraphicsTests
{
    [Fact]
    public async Task A_player_cannot_be_in_both_squads_and_an_xi_has_at_most_eleven()
    {
        using var f = await MatchFixture.CreateAsync(startInnings: false);
        var second = await CreateSecondMatch(f);
        var ok = await f.Matches.SetSquadAsync(second, new SetSquadRequest
        {
            TeamId = f.India.Id, Players = f.IndiaXI.Select((p, i) => new SquadPlayerRequest { PlayerId = p.Id, IsPlayingXI = true, BattingOrder = i + 1 }).ToList(),
        });
        Assert.True(ok.Succeeded);
        var clash = await f.Matches.SetSquadAsync(second, new SetSquadRequest
        {
            TeamId = f.Australia.Id, Players = new() { new SquadPlayerRequest { PlayerId = f.IndiaXI[0].Id } },
        });
        Assert.False(clash.Succeeded);

        var twelve = f.AusXI.Concat(new[] { new Domain.Entities.Player { DisplayName = "Extra", ShortName = "X" } }).ToList();
        f.Db.Players.Add(twelve[^1]); await f.Db.SaveChangesAsync();
        var tooMany = await f.Matches.SetSquadAsync(second, new SetSquadRequest
        {
            TeamId = f.Australia.Id, Players = twelve.Select(p => new SquadPlayerRequest { PlayerId = p.Id, IsPlayingXI = true }).ToList(),
        });
        Assert.False(tooMany.Succeeded);
    }

    [Fact]
    public async Task Toss_needs_both_squads_and_the_squads_lock_once_the_match_starts()
    {
        using var f = await MatchFixture.CreateAsync(startInnings: false);
        var second = await CreateSecondMatch(f);
        Assert.False((await f.Matches.RecordTossAsync(second, new TossRequest { TossWinnerTeamId = f.India.Id, TossDecision = "Bat" })).Succeeded);

        var locked = await f.Matches.SetSquadAsync(f.MatchId, new SetSquadRequest
        {
            TeamId = f.India.Id, Players = new() { new SquadPlayerRequest { PlayerId = f.IndiaXI[0].Id } },
        });
        Assert.False(locked.Succeeded);   // match 1 is Live
    }

    [Fact]
    public async Task Home_and_away_must_differ_and_an_unstarted_match_can_be_deleted()
    {
        using var f = await MatchFixture.CreateAsync(startInnings: false);
        var t = f.Db.Tournaments.First(); var v = f.Db.Venues.First(); var r = f.Db.MatchRules.First();
        var same = await f.Matches.CreateAsync(new CreateMatchRequest { TournamentId = t.Id, HomeTeamId = f.India.Id, AwayTeamId = f.India.Id, VenueId = v.Id, MatchRulesId = r.Id });
        Assert.False(same.Succeeded);
        var second = await CreateSecondMatch(f);
        Assert.True((await f.Matches.DeleteAsync(second)).Succeeded);
        Assert.False((await f.Matches.DeleteAsync(f.MatchId)).Succeeded);   // started
    }

    [Fact]
    public async Task Win_probability_must_add_up_to_one_hundred()
    {
        using var f = await MatchFixture.CreateAsync();
        Assert.False((await f.Matches.SetWinProbabilityAsync(f.MatchId, new WinProbabilityRequest { Home = 50, Draw = 10, Away = 10 })).Succeeded);
        var s = f.Ok(await f.Matches.SetWinProbabilityAsync(f.MatchId, new WinProbabilityRequest { Home = 52, Draw = 7, Away = 41 }));
        Assert.Equal((52, 7, 41), (s.WinProbability.Home, s.WinProbability.Draw, s.WinProbability.Away));
    }

    [Fact]
    public async Task Graphics_state_is_persisted_in_the_match_state_and_banners_need_content()
    {
        using var f = await MatchFixture.CreateAsync();
        Assert.True(f.Last.Graphics["Scorebug"].IsVisible);
        Assert.False(f.Last.Graphics["Banner"].IsVisible);

        Assert.False((await f.Graphics.SetAsync(f.MatchId, "Banner", new SetGraphicRequest { IsVisible = true })).Succeeded);
        Assert.False((await f.Graphics.SetAsync(f.MatchId, "NoSuchGraphic", new SetGraphicRequest { IsVisible = true })).Succeeded);

        var payload = JsonDocument.Parse("{\"text\":\"Happy Independence Day\"}").RootElement;
        var s = f.Ok(await f.Graphics.SetAsync(f.MatchId, "banner", new SetGraphicRequest { IsVisible = true, Payload = payload }));
        Assert.True(s.Graphics["Banner"].IsVisible);
        Assert.Equal("Happy Independence Day", s.Graphics["Banner"].Payload!.Value.GetProperty("text").GetString());

        s = f.Ok(await f.Graphics.SetAsync(f.MatchId, "Banner", new SetGraphicRequest { IsVisible = false }));
        Assert.False(s.Graphics["Banner"].IsVisible);

        // a fresh read (what an OBS page does after a refresh) restores the same state
        var again = (await f.Matches.GetStateAsync(f.MatchId))!;
        Assert.False(again.Graphics["Banner"].IsVisible);
        Assert.True(again.Graphics["Scorebug"].IsVisible);
    }

    [Fact]
    public async Task Both_playing_xis_are_in_the_state_in_batting_order_with_split_names_and_roles()
    {
        using var f = await MatchFixture.CreateAsync();
        var l = f.Last.Lineups;
        Assert.Equal(f.India.Id, l.Home.Team.Id);
        Assert.Equal(f.Australia.Id, l.Away.Team.Id);
        Assert.Equal(11, l.Home.Players.Count);
        Assert.Equal(Enumerable.Range(1, 11), l.Home.Players.Select(p => p.BattingOrder));
        var first = l.Home.Players[0];
        Assert.Equal(("Ind", "1"), (first.FirstName, first.LastName));       // "Ind 1" has no first/last name stored: split from DisplayName
        Assert.True(first.IsCaptain);
        Assert.True(l.Home.Players[4].IsWicketKeeper);
        Assert.Equal(1, l.Home.Players.Count(p => p.IsWicketKeeper));
    }

    [Fact]
    public async Task The_team_lineup_graphic_needs_one_of_the_two_teams_and_a_playing_xi()
    {
        using var f = await MatchFixture.CreateAsync();
        Assert.False(f.Last.Graphics["TeamLineup"].IsVisible);

        Assert.False((await f.Graphics.SetAsync(f.MatchId, "TeamLineup", new SetGraphicRequest { IsVisible = true })).Succeeded);               // no team
        var stranger = JsonDocument.Parse($"{{\"teamId\":\"{Guid.NewGuid()}\"}}").RootElement;
        Assert.False((await f.Graphics.SetAsync(f.MatchId, "TeamLineup", new SetGraphicRequest { IsVisible = true, Payload = stranger })).Succeeded); // not playing
        var junk = JsonDocument.Parse("{\"teamId\":\"nope\"}").RootElement;
        Assert.False((await f.Graphics.SetAsync(f.MatchId, "TeamLineup", new SetGraphicRequest { IsVisible = true, Payload = junk })).Succeeded);

        var ind = JsonDocument.Parse($"{{\"teamId\":\"{f.India.Id}\"}}").RootElement;
        var s = f.Ok(await f.Graphics.SetAsync(f.MatchId, "TeamLineup", new SetGraphicRequest { IsVisible = true, Payload = ind }));
        Assert.True(s.Graphics["TeamLineup"].IsVisible);
        Assert.Equal(f.India.Id.ToString(), s.Graphics["TeamLineup"].Payload!.Value.GetProperty("teamId").GetString());

        s = f.Ok(await f.Graphics.SetAsync(f.MatchId, "TeamLineup", new SetGraphicRequest { IsVisible = false }));    // hiding needs no payload
        Assert.False(s.Graphics["TeamLineup"].IsVisible);
    }

    private static CricketStream.Contracts.Master.OverlayThemeDto Theme(string primary, bool teamColours = false) => new()
    {
        Name = "Test theme " + Guid.NewGuid().ToString("N")[..6], PrimaryColor = primary, SecondaryColor = "#7a1fa2", AccentColor = "#ffd400", UseTeamColors = teamColours,
    };

    [Fact]
    public async Task Editing_a_theme_pushes_the_new_colours_to_the_overlay_already_on_air()
    {
        using var f = await MatchFixture.CreateAsync();
        var theme = (await f.MasterData.SaveThemeAsync(null, Theme("#0f6b4f"))).Value!;
        var match = await f.Db.Matches.FindAsync(f.MatchId);
        match!.OverlayThemeId = theme.Id;
        await f.Db.SaveChangesAsync();
        var versionBefore = (await f.Matches.GetStateAsync(f.MatchId))!.Version;
        var sentBefore = f.Broadcaster.Sent.Count;

        var edited = Theme("#112233"); edited.Name = theme.Name;
        Assert.True((await f.MasterData.SaveThemeAsync(theme.Id, edited)).Succeeded);

        Assert.True(f.Broadcaster.Sent.Count > sentBefore, "the open overlay was never told about the edit");
        var pushed = f.Broadcaster.Sent.Last();
        Assert.Equal("#112233", pushed.Theme.PrimaryColor);
        Assert.True(pushed.Version > versionBefore);                         // clients ignore versions that are not newer
        Assert.Equal("#112233", (await f.Matches.GetStateAsync(f.MatchId))!.Theme.PrimaryColor);
    }

    [Fact]
    public async Task Creating_a_theme_does_not_broadcast_to_matches_that_do_not_use_it()
    {
        using var f = await MatchFixture.CreateAsync();
        var sentBefore = f.Broadcaster.Sent.Count;
        Assert.True((await f.MasterData.SaveThemeAsync(null, Theme("#445566"))).Succeeded);
        Assert.Equal(sentBefore, f.Broadcaster.Sent.Count);
    }

    [Fact]
    public async Task The_use_team_colours_option_reaches_the_overlay_state()
    {
        using var f = await MatchFixture.CreateAsync();
        var theme = (await f.MasterData.SaveThemeAsync(null, Theme("#0f6b4f", teamColours: true))).Value!;
        Assert.True(theme.UseTeamColors);
        (await f.Db.Matches.FindAsync(f.MatchId))!.OverlayThemeId = theme.Id;
        await f.Db.SaveChangesAsync();
        Assert.True((await f.Matches.GetStateAsync(f.MatchId))!.Theme.UseTeamColors);

        var off = Theme("#0f6b4f", teamColours: false); off.Name = theme.Name;
        await f.MasterData.SaveThemeAsync(theme.Id, off);
        Assert.False((await f.Matches.GetStateAsync(f.MatchId))!.Theme.UseTeamColors);
    }

    [Fact]
    public async Task A_match_without_its_own_theme_uses_its_tournaments_theme()
    {
        using var f = await MatchFixture.CreateAsync();
        Assert.Equal("#0f6b4f", (await f.Matches.GetStateAsync(f.MatchId))!.Theme.PrimaryColor);   // built-in default while neither has one
        var theme = (await f.MasterData.SaveThemeAsync(null, Theme("#abcdef"))).Value!;
        f.Db.Tournaments.First().OverlayThemeId = theme.Id;
        await f.Db.SaveChangesAsync();
        Assert.Equal("#abcdef", (await f.Matches.GetStateAsync(f.MatchId))!.Theme.PrimaryColor);
    }

    [Fact]
    public async Task Both_teams_playing_xis_can_be_shown_together_but_only_when_both_have_one()
    {
        using var f = await MatchFixture.CreateAsync(startInnings: false);
        var both = JsonDocument.Parse("{\"teamId\":\"both\"}").RootElement;
        var s = f.Ok(await f.Graphics.SetAsync(f.MatchId, "TeamLineup", new SetGraphicRequest { IsVisible = true, Payload = both }));
        Assert.True(s.Graphics["TeamLineup"].IsVisible);
        Assert.Equal("both", s.Graphics["TeamLineup"].Payload!.Value.GetProperty("teamId").GetString());

        // a match where only India has picked its XI cannot show both
        var second = await CreateSecondMatch(f);
        Assert.True((await f.Matches.SetSquadAsync(second, new SetSquadRequest
        {
            TeamId = f.India.Id, Players = f.IndiaXI.Select((p, i) => new SquadPlayerRequest { PlayerId = p.Id, IsPlayingXI = true, BattingOrder = i + 1 }).ToList(),
        })).Succeeded);
        var refused = await f.Graphics.SetAsync(second, "TeamLineup", new SetGraphicRequest { IsVisible = true, Payload = both });
        Assert.False(refused.Succeeded);
        Assert.Equal(Application.Common.ErrorKind.Unprocessable, refused.Error!.Kind);

        // a single team is still fine there, and junk is still rejected
        var india = JsonDocument.Parse($"{{\"teamId\":\"{f.India.Id}\"}}").RootElement;
        Assert.True((await f.Graphics.SetAsync(second, "TeamLineup", new SetGraphicRequest { IsVisible = true, Payload = india })).Succeeded);
        var junk = JsonDocument.Parse("{\"teamId\":\"everyone\"}").RootElement;
        Assert.False((await f.Graphics.SetAsync(second, "TeamLineup", new SetGraphicRequest { IsVisible = true, Payload = junk })).Succeeded);
    }
    [Fact]
    public async Task Match_state_exposes_what_the_scorer_may_do_next()
    {
        using var f = await MatchFixture.CreateAsync();
        var a = f.Last.Actions;
        Assert.True(a.CanScoreBall); Assert.False(a.CanStartInnings); Assert.False(a.AwaitingNewBatter); Assert.False(a.AwaitingNewBowler);
        Assert.Equal(11, a.BattingXI.Count); Assert.Equal(11, a.FieldingXI.Count);
        Assert.Equal(9, a.AvailableBatters.Count);                        // XI minus the two at the crease
        Assert.Equal(10, a.EligibleBowlers.Count);                        // XI minus the current bowler
        Assert.StartsWith("Australia won the toss", f.Last.TossText);
        Assert.Contains("chose to bowl", f.Last.TossText);
    }

    private static async Task<Guid> CreateSecondMatch(MatchFixture f)
    {
        var t = f.Db.Tournaments.First(); var v = f.Db.Venues.First(); var r = f.Db.MatchRules.First();
        var created = await f.Matches.CreateAsync(new CreateMatchRequest
        {
            TournamentId = t.Id, HomeTeamId = f.India.Id, AwayTeamId = f.Australia.Id, VenueId = v.Id, MatchRulesId = r.Id, MatchFormat = "T20",
        });
        Assert.True(created.Succeeded, created.Error?.Message);
        return created.Value!.Id;
    }
}

