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
        public HashSet<Guid> Retired = new();      // retired hurt in this innings (may be back at the crease now)
        public HashSet<Guid> Absent = new();       // absent hurt: cannot bat in this innings
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
        // The first innings of a Super Over (and of each further one): the side that batted last bats first again.
        var mainInnings = Math.Max(1, m.MatchRules.InningsPerSide) * 2;
        if (!m.MatchRules.IsTest && ordered.Count >= mainInnings && (ordered.Count - mainInnings) % 2 == 0) return last;
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

    // The audit entry of a penalty stores the innings' penalty total before and after; the difference is what was awarded.
    // A correction (negative) or an unreadable entry shows nothing.
    private static LastPenaltyDto? ParsePenalty(AuditLog? log)
    {
        if (log is null || string.IsNullOrWhiteSpace(log.AfterStateJson)) return null;
        try
        {
            static int Total(string json) => System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("penaltyRuns").GetInt32();
            var awarded = Total(log.AfterStateJson) - Total(log.PreviousStateJson);
            return awarded > 0 ? new LastPenaltyDto { Id = log.Id, Runs = awarded } : null;
        }
        catch (Exception e) when (e is System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException or FormatException) { return null; }
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

    /// <summary>Sixes in a row by the batter of the last ball, counting back from it. A wide is skipped (it is not a delivery to the batter);
    /// any other ball by him that was not a six, or a ball by someone else, ends the run. 0 when the last ball was not a six.</summary>
    public static int SixStreak(IReadOnlyList<Ball> balls)
    {
        if (balls.Count == 0 || !balls[^1].IsSix) return 0;
        var striker = balls[^1].StrikerId;
        var count = 0;
        for (var i = balls.Count - 1; i >= 0; i--)
        {
            var b = balls[i];
            if (b.ExtrasType == ExtrasType.Wide && !b.IsSix) continue;
            if (!b.IsSix || b.StrikerId != striker) break;
            count++;
        }
        return count;
    }

    /// <summary>The Power Surge block for the innings: when it can start, whether it is on now, and how much is left.
    /// Every limited-overs match has one button for it. With BBL-style rules (PowerSurgeEnabled) it may only start inside the rule's window;
    /// otherwise it can be taken any time, once per innings, for 2 overs. A start in the middle of an over takes effect from the next over (Pending until then).</summary>
    public static PowerSurgeDto PowerSurgeState(Innings inn, MatchRules rules, int legalBalls, bool inProgress)
    {
        var bpo = Math.Max(1, rules.BallsPerOver);
        var overs = rules.PowerSurgeOvers > 0 ? rules.PowerSurgeOvers : 2;
        var overNow = legalBalls / bpo + 1;                    // the over being bowled, or about to start
        var atBoundary = legalBalls % bpo == 0;
        var (from, to) = PowerSurgeWindow(rules, overs, CricketRules.OversLimit(rules, inn));
        var nextStart = atBoundary ? overNow : overNow + 1;    // where a surge started now would begin
        var start = inn.PowerSurgeStartOver;
        var used = start is not null;
        var active = inProgress && used && overNow >= start!.Value && overNow < start.Value + overs;
        var pending = inProgress && used && overNow < start!.Value;
        return new PowerSurgeDto
        {
            Used = used, Active = active, Pending = pending, StartOver = start ?? 0, Overs = overs,
            OversLeft = active ? start!.Value + overs - overNow : 0,
            CanStart = inProgress && !used && nextStart >= from && nextStart <= to,
            CanCancel = inProgress && used && (pending || (atBoundary && overNow == start!.Value)),
            EarliestOver = from, LatestOver = to, NextStartOver = nextStart,
        };
    }

    /// <summary>The overs a surge may begin in: the rule's window for BBL-style rules, otherwise anywhere it still fits in the innings.</summary>
    public static (int From, int To) PowerSurgeWindow(MatchRules rules, int overs, int? oversLimit = null)
    {
        if (rules.PowerSurgeEnabled) return (Math.Max(1, rules.PowerSurgeFromOver), Math.Max(rules.PowerSurgeFromOver, rules.PowerSurgeToOver));
        var limit = oversLimit ?? rules.OversPerInnings;
        return (1, limit > 0 ? Math.Max(1, limit - overs + 1) : int.MaxValue);
    }

    /// <summary>Fours in a row by the batter of the last ball (same rules as <see cref="SixStreak"/>: a wide is skipped, anything else ends the run).</summary>
    public static int FourStreak(IReadOnlyList<Ball> balls)
    {
        if (balls.Count == 0 || !balls[^1].IsFour) return 0;
        var striker = balls[^1].StrikerId;
        var count = 0;
        for (var i = balls.Count - 1; i >= 0; i--)
        {
            var b = balls[i];
            if (b.ExtrasType == ExtrasType.Wide && !b.IsFour) continue;
            if (!b.IsFour || b.StrikerId != striker) break;
            count++;
        }
        return count;
    }

    private static bool CreditedToBowler(Ball b) => b.IsWicket && b.Dismissal is { } d && CricketRules.CreditsBowler(d.DismissalType);

    /// <summary>Wickets in a row by the bowler of the last ball, counting only his own deliveries (other bowlers' overs in between do not matter).
    /// A wide or no-ball that is not itself a wicket is not a delivery for this purpose. 3 is a hat-trick. 0 when the last ball was not his wicket.</summary>
    public static int WicketStreak(IReadOnlyList<Ball> balls)
    {
        if (balls.Count == 0 || !CreditedToBowler(balls[^1])) return 0;
        var bowler = balls[^1].BowlerId;
        var count = 0;
        for (var i = balls.Count - 1; i >= 0; i--)
        {
            var b = balls[i];
            if (b.BowlerId != bowler) continue;
            if (!b.IsLegal && !b.IsWicket) continue;
            if (!CreditedToBowler(b)) break;
            count++;
        }
        return count;
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

        // A batter who retired hurt is on the scorecard even if he never faced a ball; an absent-hurt one is not.
        foreach (var st in inn.BatterStatuses.OrderBy(x => x.AfterBallSequence))
        {
            if (st.Kind == BatterStatusKind.RetiredHurt) { c.Retired.Add(st.PlayerId); Touch(st.PlayerId, c.BatOrder); c.Bat.TryAdd(st.PlayerId, new BatAcc()); }
            else c.Absent.Add(st.PlayerId);
        }

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

    public static MatchStateDto Build(Match m, IEnumerable<OverlayGraphicState> graphics, AuditLog? lastPenalty = null)
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
                LogoUrl = th.LogoUrl, WatermarkUrl = th.WatermarkUrl, WatermarkText = th.WatermarkText, UseTeamColors = th.UseTeamColors,
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
                RunRate = CricketRules.RunRate(c.Runs, c.LegalBalls, bpo),
                IsCompleted = i.Status == InningsStatus.Completed, FallOfWickets = c.Fow,
                IsSuperOver = i.IsSuperOver, IsFollowOn = i.IsFollowOn, IsDls = i.IsDls,
            };
            foreach (var id in c.BatOrder)
            {
                var a = c.Bat.GetValueOrDefault(id) ?? new BatAcc();
                var atCrease = id == i.CurrentStrikerId || id == i.CurrentNonStrikerId;
                card.Batting.Add(new BatterLineDto
                {
                    PlayerId = id, Name = Name(id), IsCaptain = roster.TryGetValue(id, out var rp) && rp.IsCaptain, IsWicketKeeper = rp is not null && rp.IsWicketKeeper,
                    Runs = a.Runs, Balls = a.Balls, Fours = a.Fours, Sixes = a.Sixes,
                    StrikeRate = CricketRules.StrikeRate(a.Runs, a.Balls),
                    Status = a.Out is not null ? "out"
                           : atCrease ? (i.Status == InningsStatus.InProgress ? "batting" : "not out")
                           : c.Retired.Contains(id) ? "retired hurt"
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
            card.YetToBat = Xi(i.BattingTeamId).Where(p => !c.BatOrder.Contains(p.PlayerId) && !c.Absent.Contains(p.PlayerId))
                .Select(p => p.Player.DisplayName).ToList();
            card.AbsentHurt = Xi(i.BattingTeamId).Where(p => c.Absent.Contains(p.PlayerId)).Select(p => p.Player.DisplayName).ToList();
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
                PowerSurge = !rules.IsTest && !latest.IsSuperOver ? PowerSurgeState(latest, rules, c.LegalBalls, inProgress) : null,
                IsSuperOver = latest.IsSuperOver, IsDls = latest.IsDls, DeadBalls = latest.DeadBalls,
                OversLimit = CricketRules.OversLimit(rules, latest),
            };
            if (!rules.IsTest) dto.OversPerInnings = s.OversLimit;   // the overs this innings is really limited to (reduced by rain, or a Super Over)

            if (s.OversLimit > 0 && !rules.IsTest)
            {
                var total = s.OversLimit * bpo;
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
                var pl = roster.TryGetValue(pid, out var rmp) ? rmp.Player : null;
                var hasCareer = pl is not null && (pl.CareerMatches is not null || pl.CareerRuns is not null || pl.CareerAverage is not null
                    || pl.CareerStrikeRate is not null || pl.CareerFifties is not null || pl.CareerHundreds is not null);
                return new BatterDto
                {
                    FirstName = pl?.FirstName ?? "", LastName = pl?.LastName ?? "", JerseyNumber = pl?.JerseyNumber ?? 0, BattingStyle = pl?.BattingStyle ?? "",
                    Career = hasCareer ? new BatterCareerDto {
                        Label = pl!.CareerLabel, Matches = pl.CareerMatches, Runs = pl.CareerRuns, Average = pl.CareerAverage,
                        StrikeRate = pl.CareerStrikeRate, Fifties = pl.CareerFifties, Hundreds = pl.CareerHundreds,
                    } : null,
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
                // a batter out for nought: Golden = out to the first ball he faced, Diamond = out without facing one
                string? duckType = null;
                if (last.IsWicket && last.Dismissal is { } dd && c.Bat.GetValueOrDefault(dd.DismissedPlayerId) is { Runs: 0 } duckAcc)
                    duckType = duckAcc.Balls == 0 ? "Diamond" : duckAcc.Balls == 1 ? "Golden" : "Duck";
                dto.LastBall = new LastBallDto { BallId = last.Id, Label = label, Kind = kind, Runs = last.RunsBat + last.RunsExtra, SixStreak = SixStreak(c.Balls), FourStreak = FourStreak(c.Balls), DuckType = duckType, DuckName = duckType is null ? null : Name(last.Dismissal!.DismissedPlayerId), WicketStreak = WicketStreak(c.Balls), SixDistance = last.IsSix ? last.SixDistance : null };
            }
            if (last is not null)
            {
                // ---- milestones reached on this very ball ----
                if (last.RunsBat > 0 && c.Bat.TryGetValue(last.StrikerId, out var sa))
                {
                    var before = sa.Runs - last.RunsBat;
                    foreach (var t in new[] { 50, 100, 150, 200, 250, 300 })
                        if (before < t && sa.Runs >= t)
                            dto.Milestones.Add(new MilestoneDto {
                                Id = $"{last.Id}:runs:{t}", Kind = "Runs", Value = t, PlayerId = last.StrikerId, Name = Name(last.StrikerId), PhotoUrl = Photo(last.StrikerId),
                                Detail = $"{sa.Runs} ({sa.Balls})  {sa.Fours}x4  {sa.Sixes}x6",
                            });
                }
                if (last.IsWicket && c.Bowl.TryGetValue(last.BowlerId, out var wa) && wa.Wickets is 4 or 5
                    && last.Dismissal is { } dm && dm.DismissalType is not (DismissalType.RunOut or DismissalType.RetiredHurt or DismissalType.TimedOut or DismissalType.ObstructingField or DismissalType.HandledBall))
                    dto.Milestones.Add(new MilestoneDto {
                        Id = $"{last.Id}:wkts:{wa.Wickets}", Kind = wa.Wickets == 5 ? "FiveWickets" : "FourWickets", Value = wa.Wickets, PlayerId = last.BowlerId, Name = Name(last.BowlerId), PhotoUrl = Photo(last.BowlerId),
                        Detail = $"{wa.Wickets}-{wa.Runs} ({CricketRules.FormatOvers(wa.LegalBalls, bpo)})",
                    });
                var lastOver = latest.Overs.FirstOrDefault(o => o.Id == last.OverId);
                if (lastOver is { IsCompleted: true } && last.IsLegal)
                {
                    var lv = lastOver.Balls.Where(b => !b.IsVoided).ToList();
                    if (lv.Count(b => b.IsLegal) >= bpo && lv.Select(b => b.BowlerId).Distinct().Count() == 1 && lv.Sum(b => b.BowlerRuns) == 0 && c.Bowl.TryGetValue(lv[0].BowlerId, out var ma))
                        dto.Milestones.Add(new MilestoneDto {
                            Id = $"{last.Id}:maiden", Kind = "Maiden", Value = ma.Maidens, PlayerId = lv[0].BowlerId, Name = Name(lv[0].BowlerId), PhotoUrl = Photo(lv[0].BowlerId),
                            Detail = $"{CricketRules.FormatOvers(ma.LegalBalls, bpo)}-{ma.Maidens}-{ma.Runs}-{ma.Wickets}",
                        });
                }
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

        dto.LastPenalty = ParsePenalty(lastPenalty);
        dto.Lineups = new LineupsDto { Home = BuildLineup(m, m.HomeTeam), Away = BuildLineup(m, m.AwayTeam) };
        if (m.BreakKind is { } bk && m.Status is MatchStatus.Paused or MatchStatus.RainDelay)
            dto.Break = new BreakDto {
                Kind = bk.ToString(), Label = BreakKinds.Label(bk), PlannedSeconds = m.BreakPlannedSeconds,
                ElapsedSeconds = m.BreakStartedAt is { } started ? Math.Max(0, (int)(DateTimeOffset.UtcNow - started).TotalSeconds) : 0,
            };
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
        var superInnings = innings.Count(i => i.IsSuperOver);
        var secondSuperInnings = latest is { IsSuperOver: true } && superInnings % 2 == 1;   // the first Super Over innings is done; the chase is next
        a2.CanStartInnings = liveish && (latest is null || latest.Status == InningsStatus.Completed) && (innings.Count < maxInnings || secondSuperInnings);
        a2.CanStartSuperOver = !rules.IsTest && m.Status == MatchStatus.Completed && m.ResultType == MatchResultType.Tie
            && innings.Count >= maxInnings && latest?.Status == InningsStatus.Completed;
        a2.CanComplete = liveish;
        a2.CanUndo = (liveish || m.Status == MatchStatus.Completed) && latest is not null && calcs[latest.Id].Balls.Count > 0;
        a2.InningsOver = latest?.Status == InningsStatus.Completed;
        var inProg = latest is { Status: InningsStatus.InProgress };
        a2.CanDeclare = inProg && rules.IsTest;
        if (rules.IsTest && innings.Count == 2 && latest!.Status == InningsStatus.Completed)
        {
            a2.FollowOnLead = calcs[innings[0].Id].Runs - calcs[innings[1].Id].Runs;
            a2.FollowOnAvailable = CricketRules.FollowOnAvailable(rules, calcs[innings[0].Id].Runs, calcs[innings[1].Id].Runs);
        }

        Guid? activeBatTeam = inProg ? latest!.BattingTeamId : NextBattingTeamId(m, innings);
        a2.NextBattingTeamId = a2.CanStartInnings || a2.CanStartSuperOver ? NextBattingTeamId(m, innings) : null;
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
                .Where(p => !dismissedAll.Contains(p.Id) && !c.Absent.Contains(p.Id) && p.Id != l.CurrentStrikerId && p.Id != l.CurrentNonStrikerId).ToList();
            a2.RetiredHurtIds = c.Retired.Where(id => id != l.CurrentStrikerId && id != l.CurrentNonStrikerId && !dismissedAll.Contains(id)).ToList();
            a2.AbsentHurtIds = c.Absent.ToList();
            a2.CanReviseTarget = !rules.IsTest && !l.IsSuperOver;
            a2.EligibleBowlers = a2.FieldingXI.Where(p =>
                p.Id != l.LastBowlerId && p.Id != l.CurrentBowlerId &&
                CricketRules.BowlerHasQuotaLeft(c.Bowl.GetValueOrDefault(p.Id)?.LegalBalls ?? 0, rules, l)).ToList();
        }
        return dto;
    }
}
