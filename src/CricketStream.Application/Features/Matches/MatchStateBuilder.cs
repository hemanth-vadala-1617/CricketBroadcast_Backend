using System.Text.Json;
using CricketStream.Contracts.Matches;
using CricketStream.Domain.Entities;
using CricketStream.Domain.Enums;
using CricketStream.Domain.Rules;

namespace CricketStream.Application.Features.Matches;

/// <summary>
/// Pure projection: Match (with players, innings, overs, balls, dismissals loaded) -> MatchStateDto.
/// Everything is recomputed from the non-voided ball log, so undo can never leave totals wrong.
/// </summary>
public static class MatchStateBuilder
{
    public static readonly string[] GraphicKeys =
        { "Scorebug", "BatterCards", "BowlerCard", "Timeline", "WinProbability", "FullScorecard", "Banner", "TeamLineup" };

    private static readonly Dictionary<string, bool> GraphicDefaults = new()
    {
        ["Scorebug"] = true, ["BatterCards"] = true, ["BowlerCard"] = true, ["Timeline"] = true,
        ["WinProbability"] = false, ["FullScorecard"] = false, ["Banner"] = false, ["TeamLineup"] = false,
    };

    // ---------- per-innings working data ----------
    private sealed class BatAcc { public int Runs, Balls, Fours, Sixes; public Dismissal? Out; public Ball? OutBall; }
    private sealed class BowlAcc { public int LegalBalls, Runs, Wickets, Wides, NoBalls, Maidens; }

    private sealed class Calc
    {
        public Innings Innings = null!;
        public List<Ball> Balls = new();
        public int Runs, Wickets, Extras, LegalBalls;
        public Dictionary<Guid, BatAcc> Bat = new();
        public List<Guid> BatOrder = new();
        public Dictionary<Guid, BowlAcc> Bowl = new();
        public List<Guid> BowlOrder = new();
        public List<FallOfWicketDto> Fow = new();
        public int PartnerRuns, PartnerBalls;
        public HashSet<Guid> Dismissed = new();
        public string ExtrasText = "";
    }

    public static Guid? NextBattingTeamId(Match m, IReadOnlyList<Innings> ordered)
    {
        if (ordered.Count == 0)
        {
            if (m.TossWinnerTeamId is null || m.TossDecision is null) return null;
            var other = m.TossWinnerTeamId == m.HomeTeamId ? m.AwayTeamId : m.HomeTeamId;
            return m.TossDecision == TossDecision.Bat ? m.TossWinnerTeamId : other;
        }
        var last = ordered[^1].BattingTeamId;
        return last == m.HomeTeamId ? m.AwayTeamId : m.HomeTeamId;
    }

    /// <summary>Playing XI in batting order. Names come from First/Last name, else the display name is split.</summary>
    private static LineupDto BuildLineup(Match m, Team team)
    {
        var players = m.MatchPlayers.Where(p => p.TeamId == team.Id && p.IsPlayingXI)
            .OrderBy(p => p.BattingOrder).ThenBy(p => p.Player.DisplayName)
            .Select(p =>
            {
                var first = p.Player.FirstName?.Trim() ?? "";
                var last = p.Player.LastName?.Trim() ?? "";
                if (first.Length == 0 && last.Length == 0)
                {
                    var parts = p.Player.DisplayName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    first = parts.Length > 1 ? parts[0] : "";
                    last = parts.Length > 1 ? parts[1] : parts.FirstOrDefault() ?? "";
                }
                return new LineupPlayerDto
                {
                    PlayerId = p.PlayerId, FirstName = first, LastName = last, DisplayName = p.Player.DisplayName,
                    PhotoUrl = p.Player.PhotoUrl, IsCaptain = p.IsCaptain, IsWicketKeeper = p.IsWicketKeeper, BattingOrder = p.BattingOrder,
                };
            }).ToList();
        return new LineupDto
        {
            Players = players,
            Team = new TeamLiteDto { Id = team.Id, Name = team.Name, ShortName = team.ShortName, LogoUrl = team.LogoUrl, PrimaryColor = team.PrimaryColor, SecondaryColor = team.SecondaryColor },
        };
    }

    public static string Ordinal(int n) => n switch { 1 => "1st", 2 => "2nd", 3 => "3rd", _ => $"{n}th" };

    public static string DismissalText(Dismissal d, Ball b, Func<Guid, string> name)
    {
        var bowler = name(b.BowlerId);
        var fielder = d.FielderId is { } f ? name(f) : null;
        return d.DismissalType switch
        {
            DismissalType.Bowled => $"b {bowler}",
            DismissalType.Caught => fielder is null || d.FielderId == b.BowlerId ? $"c & b {bowler}" : $"c {fielder} b {bowler}",
            DismissalType.LBW => $"lbw b {bowler}",
            DismissalType.Stumped => $"st {fielder ?? "wk"} b {bowler}",
            DismissalType.HitWicket => $"hit wicket b {bowler}",
            DismissalType.RunOut => fielder is null ? "run out" : $"run out ({fielder})",
            DismissalType.HandledBall => "handled the ball",
            DismissalType.ObstructingField => "obstructing the field",
            DismissalType.TimedOut => "timed out",
            _ => d.DismissalType.ToString(),
        };
    }

    public static (string Label, string Kind) Chip(Ball b, MatchRules rules)
    {
        var total = b.RunsBat + b.RunsExtra;
        if (b.IsWicket) return (total > 0 ? $"{total}W" : "W", "Wicket");
        switch (b.ExtrasType)
        {
            case ExtrasType.Wide:
            {
                var more = b.RunsExtra - rules.WideRuns;
                return (more > 0 ? $"WD+{more}" : "WD", "Wide");
            }
            case ExtrasType.NoBall:
            {
                var more = total - rules.NoBallRuns;
                return (more > 0 ? $"NB+{more}" : "NB", "NoBall");
            }
            case ExtrasType.Bye: return ($"B{b.RunsExtra}", "Bye");
            case ExtrasType.LegBye: return ($"LB{b.RunsExtra}", "LegBye");
        }
        if (b.IsSix) return ("6", "Six");
        if (b.IsFour) return ("4", "Four");
        if (b.RunsBat == 0) return ("0", "Dot");
        return (b.RunsBat.ToString(), "Runs");
    }

    private static Calc Calculate(Innings inn, MatchRules rules, Func<Guid, string> name)
    {
        var c = new Calc { Innings = inn };
        c.Balls = inn.Overs.SelectMany(o => o.Balls).Where(b => !b.IsVoided).OrderBy(b => b.Sequence).ToList();
        int byes = 0, lbs = 0, wides = 0, nbs = 0;

        void Touch(Guid id, List<Guid> order) { if (!order.Contains(id)) order.Add(id); }

        foreach (var b in c.Balls)
        {
            Touch(b.StrikerId, c.BatOrder);
            Touch(b.NonStrikerId, c.BatOrder);
            if (b.Dismissal?.NewBatterId is { } nb) Touch(nb, c.BatOrder);
            Touch(b.BowlerId, c.BowlOrder);

            if (!c.Bat.TryGetValue(b.StrikerId, out var bat)) c.Bat[b.StrikerId] = bat = new BatAcc();
            if (!c.Bowl.TryGetValue(b.BowlerId, out var bowl)) c.Bowl[b.BowlerId] = bowl = new BowlAcc();

            bat.Runs += b.RunsBat;
            if (b.ExtrasType != ExtrasType.Wide) bat.Balls++;
            if (b.IsFour) bat.Fours++;
            if (b.IsSix) bat.Sixes++;

            bowl.Runs += b.BowlerRuns;
            if (b.IsLegal) bowl.LegalBalls++;
            if (b.ExtrasType == ExtrasType.Wide) bowl.Wides += b.RunsExtra;
            if (b.ExtrasType == ExtrasType.NoBall) bowl.NoBalls++;

            c.Runs += b.RunsBat + b.RunsExtra;
            c.Extras += b.RunsExtra;
            if (b.IsLegal) c.LegalBalls++;

            switch (b.ExtrasType)
            {
                case ExtrasType.Wide: wides += b.RunsExtra; break;
                case ExtrasType.NoBall:
                    nbs += rules.NoBallRuns;
                    if (b.SecondaryExtrasType == ExtrasType.Bye) byes += b.RunsExtra - rules.NoBallRuns;
                    if (b.SecondaryExtrasType == ExtrasType.LegBye) lbs += b.RunsExtra - rules.NoBallRuns;
                    break;
                case ExtrasType.Bye: byes += b.RunsExtra; break;
                case ExtrasType.LegBye: lbs += b.RunsExtra; break;
            }

            c.PartnerRuns += b.RunsBat + b.RunsExtra;
            if (b.IsLegal) c.PartnerBalls++;

            if (b.IsWicket && b.Dismissal is { } d)
            {
                c.Wickets++;
                c.Dismissed.Add(d.DismissedPlayerId);
                if (!c.Bat.TryGetValue(d.DismissedPlayerId, out var outBat)) c.Bat[d.DismissedPlayerId] = outBat = new BatAcc();
                Touch(d.DismissedPlayerId, c.BatOrder);
                outBat.Out = d; outBat.OutBall = b;
                if (CricketRules.CreditsBowler(d.DismissalType)) bowl.Wickets++;
                c.Fow.Add(new FallOfWicketDto
                {
                    WicketNumber = c.Wickets,
                    Score = c.Runs,
                    Overs = CricketRules.FormatOvers(c.LegalBalls, rules.BallsPerOver),
                    BatterName = name(d.DismissedPlayerId),
                });
                c.PartnerRuns = 0; c.PartnerBalls = 0;
            }
        }

        // The batters at the crease may not have faced a ball yet.
        if (inn.CurrentStrikerId is { } s) { Touch(s, c.BatOrder); c.Bat.TryAdd(s, new BatAcc()); }
        if (inn.CurrentNonStrikerId is { } n) { Touch(n, c.BatOrder); c.Bat.TryAdd(n, new BatAcc()); }
        if (inn.CurrentBowlerId is { } cb) { Touch(cb, c.BowlOrder); c.Bowl.TryAdd(cb, new BowlAcc()); }

        // Maidens: a completed over, one bowler, nothing charged to him.
        foreach (var over in inn.Overs)
        {
            var live = over.Balls.Where(b => !b.IsVoided).ToList();
            if (live.Count == 0 || live.Count(b => b.IsLegal) < rules.BallsPerOver) continue;
            if (live.Select(b => b.BowlerId).Distinct().Count() != 1) continue;
            if (live.Sum(b => b.BowlerRuns) == 0) c.Bowl[live[0].BowlerId].Maidens++;
        }

        c.Runs += inn.PenaltyRuns;
        c.Extras += inn.PenaltyRuns;

        var parts = new List<string>();
        if (byes > 0) parts.Add($"b {byes}");
        if (lbs > 0) parts.Add($"lb {lbs}");
        if (wides > 0) parts.Add($"w {wides}");
        if (nbs > 0) parts.Add($"nb {nbs}");
        if (inn.PenaltyRuns > 0) parts.Add($"p {inn.PenaltyRuns}");
        c.ExtrasText = parts.Count == 0 ? "" : $"({string.Join(", ", parts)})";
        return c;
    }

    public static MatchStateDto Build(Match m, IEnumerable<OverlayGraphicState> graphics)
    {
        var rules = m.MatchRules;
        var bpo = rules.BallsPerOver;
        var roster = m.MatchPlayers.ToDictionary(mp => mp.PlayerId);
        string Name(Guid id) => roster.TryGetValue(id, out var mp) ? mp.Player.DisplayName : "?";
        string Short(Guid id) => roster.TryGetValue(id, out var mp) ? mp.Player.ShortName : "?";
        string? Photo(Guid id) => roster.TryGetValue(id, out var mp) ? mp.Player.PhotoUrl : null;
        PlayerLiteDto Lite(MatchPlayer mp) => new()
        {
            Id = mp.PlayerId, Name = mp.Player.DisplayName, ShortName = mp.Player.ShortName,
            PhotoUrl = mp.Player.PhotoUrl, IsWicketKeeper = mp.IsWicketKeeper,
        };
        TeamLiteDto TeamLite(Team t) => new()
        {
            Id = t.Id, Name = t.Name, ShortName = t.ShortName, LogoUrl = t.LogoUrl,
            PrimaryColor = t.PrimaryColor, SecondaryColor = t.SecondaryColor,
        };
        List<MatchPlayer> Xi(Guid teamId) => m.MatchPlayers.Where(p => p.TeamId == teamId && p.IsPlayingXI)
            .OrderBy(p => p.BattingOrder).ThenBy(p => p.Player.DisplayName).ToList();

        var innings = m.Innings.OrderBy(i => i.InningsNumber).ToList();
        var calcs = innings.ToDictionary(i => i.Id, i => Calculate(i, rules, Name));
        var latest = innings.LastOrDefault();

        var dto = new MatchStateDto
        {
            MatchId = m.Id, Version = m.Version, Title = m.Title, Format = m.MatchFormat.ToString(),
            IsTest = rules.IsTest, Status = m.Status.ToString(), ResultText = m.ResultText,
            ResultType = m.ResultType == MatchResultType.None ? null : m.ResultType.ToString(),
            TournamentName = m.Tournament.Name, VenueName = m.Venue.Name,
            BackgroundUrl = m.OverlayTheme?.BackgroundUrl ?? m.Venue.GroundImageUrl,
            HomeTeam = TeamLite(m.HomeTeam), AwayTeam = TeamLite(m.AwayTeam),
            BallsPerOver = bpo, OversPerInnings = rules.OversPerInnings,
        };
        if (m.OverlayTheme is { } th)
            dto.Theme = new ThemeDto
            {
                PrimaryColor = th.PrimaryColor, SecondaryColor = th.SecondaryColor, AccentColor = th.AccentColor,
                LogoUrl = th.LogoUrl, WatermarkUrl = th.WatermarkUrl, WatermarkText = th.WatermarkText,
            };
        if (m.TossWinnerTeamId is { } tw && m.TossDecision is { } td)
        {
            var winner = tw == m.HomeTeamId ? m.HomeTeam : m.AwayTeam;
            dto.TossText = $"{winner.Name} won the toss and chose to {(td == TossDecision.Bat ? "bat" : "bowl")}";
        }

        // ---- scorecards & previous innings ----
        foreach (var i in innings)
        {
            var c = calcs[i.Id];
            var team = i.BattingTeamId == m.HomeTeamId ? m.HomeTeam : m.AwayTeam;
            var card = new InningsScorecardDto
            {
                InningsNumber = i.InningsNumber, BattingTeam = TeamLite(team), Runs = c.Runs, Wickets = c.Wickets,
                Overs = CricketRules.FormatOvers(c.LegalBalls, bpo), Extras = c.Extras, ExtrasText = c.ExtrasText,
                IsCompleted = i.Status == InningsStatus.Completed, FallOfWickets = c.Fow,
            };
            foreach (var id in c.BatOrder)
            {
                var a = c.Bat.GetValueOrDefault(id) ?? new BatAcc();
                var atCrease = id == i.CurrentStrikerId || id == i.CurrentNonStrikerId;
                card.Batting.Add(new BatterLineDto
                {
                    PlayerId = id, Name = Name(id), Runs = a.Runs, Balls = a.Balls, Fours = a.Fours, Sixes = a.Sixes,
                    StrikeRate = CricketRules.StrikeRate(a.Runs, a.Balls),
                    Status = a.Out is not null ? "out" : atCrease && i.Status == InningsStatus.InProgress ? "batting"
                           : i.Status == InningsStatus.Completed ? "not out" : "retired hurt",
                    DismissalText = a.Out is not null && a.OutBall is not null ? DismissalText(a.Out, a.OutBall, Name) : "",
                });
            }
            foreach (var id in c.BowlOrder)
            {
                var a = c.Bowl[id];
                card.Bowling.Add(new BowlerLineDto
                {
                    PlayerId = id, Name = Name(id), Overs = CricketRules.FormatOvers(a.LegalBalls, bpo), Maidens = a.Maidens,
                    Runs = a.Runs, Wickets = a.Wickets, Wides = a.Wides, NoBalls = a.NoBalls,
                    Economy = CricketRules.Economy(a.Runs, a.LegalBalls, bpo),
                });
            }
            card.YetToBat = Xi(i.BattingTeamId).Where(p => !c.BatOrder.Contains(p.PlayerId))
                .Select(p => p.Player.DisplayName).ToList();
            dto.Scorecard.Add(card);

            if (latest is not null && i.Id != latest.Id)
                dto.PreviousInnings.Add(new InningsSummaryDto
                {
                    InningsNumber = i.InningsNumber, TeamShortName = team.ShortName, Runs = c.Runs, Wickets = c.Wickets,
                    Overs = card.Overs, IsDeclared = i.IsDeclared,
                    Text = $"{Ordinal(i.InningsNumber)} Inng.: {c.Runs}-{c.Wickets}{(i.IsDeclared ? "d" : "")}",
                });
        }

        // ---- current innings ----
        var dismissedAll = latest is null ? new HashSet<Guid>() : calcs[latest.Id].Dismissed;
        if (latest is not null)
        {
            var c = calcs[latest.Id];
            var batTeam = latest.BattingTeamId == m.HomeTeamId ? m.HomeTeam : m.AwayTeam;
            var bowlTeam = latest.BattingTeamId == m.HomeTeamId ? m.AwayTeam : m.HomeTeam;
            var inProgress = latest.Status == InningsStatus.InProgress;
            var completedOvers = c.LegalBalls / bpo;
            var s = new InningsStateDto
            {
                InningsId = latest.Id, InningsNumber = latest.InningsNumber, Status = latest.Status.ToString(),
                IsFollowOn = latest.IsFollowOn, BattingTeam = TeamLite(batTeam), BowlingTeam = TeamLite(bowlTeam),
                Runs = c.Runs, Wickets = c.Wickets, Overs = CricketRules.FormatOvers(c.LegalBalls, bpo),
                LegalBalls = c.LegalBalls, CurrentRunRate = CricketRules.RunRate(c.Runs, c.LegalBalls, bpo),
                Extras = c.Extras, Target = latest.Target, FreeHit = inProgress && latest.FreeHitPending && rules.FreeHitEnabled,
                Partnership = new PartnershipDto { Runs = c.PartnerRuns, Balls = c.PartnerBalls },
                Powerplay = inProgress && !rules.IsTest && CricketRules.IsPowerplay(completedOvers, rules),
                NewBallDue = inProgress && rules.NewBallAfterOvers > 0 && completedOvers == rules.NewBallAfterOvers,
            };

            if (rules.OversPerInnings > 0 && !rules.IsTest)
            {
                var total = rules.OversPerInnings * bpo;
                s.BallsRemaining = Math.Max(0, total - c.LegalBalls);
                if (latest.Target is null && c.LegalBalls > 0)
                    s.ProjectedScore = Math.Round(c.Runs * (decimal)total / c.LegalBalls, 0);
            }
            if (latest.Target is { } target)
            {
                s.RunsRequired = Math.Max(0, target - c.Runs);
                if (s.BallsRemaining is > 0)
                    s.RequiredRunRate = Math.Round(s.RunsRequired.Value * (decimal)bpo / s.BallsRemaining.Value, 2);
                s.LeadText = inProgress
                    ? (rules.IsTest
                        ? $"{batTeam.ShortName} need {s.RunsRequired} runs to win"
                        : $"{batTeam.ShortName} need {s.RunsRequired} runs in {s.BallsRemaining} balls")
                    : null;
            }
            else if (rules.IsTest && latest.InningsNumber >= 2)
            {
                var own = innings.Where(i => i.BattingTeamId == latest.BattingTeamId).Sum(i => calcs[i.Id].Runs);
                var opp = innings.Where(i => i.BattingTeamId != latest.BattingTeamId).Sum(i => calcs[i.Id].Runs);
                s.LeadRuns = own - opp;
                s.LeadText = s.LeadRuns > 0 ? $"{batTeam.ShortName} lead by {s.LeadRuns} runs"
                           : s.LeadRuns < 0 ? $"{batTeam.ShortName} trail by {-s.LeadRuns} runs"
                           : "Scores level";
            }

            BatterDto? Bat(Guid? id, bool onStrike)
            {
                if (id is not { } pid) return null;
                var a = c.Bat.GetValueOrDefault(pid) ?? new BatAcc();
                return new BatterDto
                {
                    PlayerId = pid, Name = Name(pid), ShortName = Short(pid), PhotoUrl = Photo(pid), Runs = a.Runs,
                    Balls = a.Balls, Fours = a.Fours, Sixes = a.Sixes, StrikeRate = CricketRules.StrikeRate(a.Runs, a.Balls),
                    OnStrike = onStrike,
                };
            }
            s.Striker = Bat(latest.CurrentStrikerId, true);
            s.NonStriker = Bat(latest.CurrentNonStrikerId, false);
            var bid = latest.CurrentBowlerId ?? latest.LastBowlerId;
            if (bid is { } bp)
            {
                var a = c.Bowl.GetValueOrDefault(bp) ?? new BowlAcc();
                s.Bowler = new BowlerDto
                {
                    PlayerId = bp, Name = Name(bp), ShortName = Short(bp), PhotoUrl = Photo(bp),
                    Overs = CricketRules.FormatOvers(a.LegalBalls, bpo), Maidens = a.Maidens, Runs = a.Runs, Wickets = a.Wickets,
                    Economy = CricketRules.Economy(a.Runs, a.LegalBalls, bpo),
                };
            }
            dto.Innings = s;

            // last ball + timeline (last two overs of this innings that actually have balls)
            var last = c.Balls.LastOrDefault();
            if (last is not null)
            {
                var (label, kind) = Chip(last, rules);
                dto.LastBall = new LastBallDto { BallId = last.Id, Label = label, Kind = kind, Runs = last.RunsBat + last.RunsExtra };
            }
            foreach (var o in latest.Overs.Where(o => o.Balls.Any(b => !b.IsVoided)).OrderByDescending(o => o.OverNumber).Take(2).Reverse())
            {
                var live = o.Balls.Where(b => !b.IsVoided).OrderBy(b => b.Sequence).ToList();
                dto.Timeline.Add(new OverTimelineDto
                {
                    OverNumber = o.OverNumber, BowlerName = Name(o.BowlerId), IsCompleted = o.IsCompleted,
                    Runs = live.Sum(b => b.RunsBat + b.RunsExtra), Wickets = live.Count(b => b.IsWicket), LegalBalls = live.Count(b => b.IsLegal),
                    Balls = live.Select(b => { var (l, k) = Chip(b, rules); return new BallChipDto { Label = l, Kind = k }; }).ToList(),
                });
            }
        }

        // ---- Test clock ----
        if (rules.IsTest)
        {
            var today = innings.SelectMany(i => calcs[i.Id].Balls).Count(b => b.IsLegal && b.Day == m.CurrentDay);
            dto.TestClock = new TestClockDto
            {
                Day = m.CurrentDay, Session = m.CurrentSession, OversLostToday = m.OversLostToday,
                OversLeftToday = CricketRules.FormatOvers(CricketRules.BallsLeftToday(today, m.OversLostToday, rules), bpo),
            };
        }

        dto.Lineups = new LineupsDto { Home = BuildLineup(m, m.HomeTeam), Away = BuildLineup(m, m.AwayTeam) };
        dto.WinProbability = new WinProbabilityDto { Home = m.WinProbHome, Draw = m.WinProbDraw, Away = m.WinProbAway };

        // ---- graphics ----
        var saved = graphics.ToDictionary(g => g.GraphicKey);
        foreach (var key in GraphicKeys)
        {
            saved.TryGetValue(key, out var g);
            JsonElement? payload = null;
            if (g?.PayloadJson is { Length: > 0 } pj) payload = JsonDocument.Parse(pj).RootElement.Clone();
            dto.Graphics[key] = new GraphicVisibilityDto { IsVisible = g?.IsVisible ?? GraphicDefaults[key], Payload = payload };
        }

        // ---- what the scorer may do next ----
        var a2 = dto.Actions;
        var homeXi = Xi(m.HomeTeamId).Count; var awayXi = Xi(m.AwayTeamId).Count;
        var finished = m.Status is MatchStatus.Completed or MatchStatus.Abandoned or MatchStatus.Cancelled;
        var liveish = m.Status is MatchStatus.Live or MatchStatus.InningsBreak or MatchStatus.RainDelay or MatchStatus.Paused;
        a2.CanRecordToss = m.Status == MatchStatus.Scheduled && homeXi >= 2 && awayXi >= 2;
        a2.CanStartMatch = m.Status == MatchStatus.TossCompleted;
        var maxInnings = Math.Max(1, rules.InningsPerSide) * 2;
        a2.CanStartInnings = liveish && (latest is null || latest.Status == InningsStatus.Completed) && innings.Count < maxInnings;
        a2.CanComplete = liveish;
        a2.CanUndo = (liveish || m.Status == MatchStatus.Completed) && latest is not null && calcs[latest.Id].Balls.Count > 0;
        a2.InningsOver = latest?.Status == InningsStatus.Completed;
        var inProg = latest is { Status: InningsStatus.InProgress };
        a2.CanDeclare = inProg && rules.IsTest;
        if (rules.IsTest && innings.Count == 2 && latest!.Status == InningsStatus.Completed)
            a2.FollowOnAvailable = CricketRules.FollowOnAvailable(rules, calcs[innings[0].Id].Runs, calcs[innings[1].Id].Runs);

        Guid? activeBatTeam = inProg ? latest!.BattingTeamId : NextBattingTeamId(m, innings);
        a2.NextBattingTeamId = a2.CanStartInnings ? NextBattingTeamId(m, innings) : null;
        if (activeBatTeam is { } bt)
        {
            var ft = bt == m.HomeTeamId ? m.AwayTeamId : m.HomeTeamId;
            a2.BattingXI = Xi(bt).Select(Lite).ToList();
            a2.FieldingXI = Xi(ft).Select(Lite).ToList();
        }
        if (inProg)
        {
            var l = latest!; var c = calcs[l.Id];
            a2.AwaitingNewBatter = l.CurrentStrikerId is null || l.CurrentNonStrikerId is null;
            a2.AwaitingNewBowler = l.CurrentBowlerId is null;
            a2.CanScoreBall = m.Status == MatchStatus.Live && !a2.AwaitingNewBatter && !a2.AwaitingNewBowler;
            a2.AvailableBatters = a2.BattingXI
                .Where(p => !dismissedAll.Contains(p.Id) && p.Id != l.CurrentStrikerId && p.Id != l.CurrentNonStrikerId).ToList();
            a2.EligibleBowlers = a2.FieldingXI.Where(p =>
                p.Id != l.LastBowlerId && p.Id != l.CurrentBowlerId &&
                CricketRules.BowlerHasQuotaLeft(c.Bowl.GetValueOrDefault(p.Id)?.LegalBalls ?? 0, rules)).ToList();
        }
        return dto;
    }
}
