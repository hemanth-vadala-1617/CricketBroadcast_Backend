using CricketStream.Domain.Entities;
using CricketStream.Domain.Enums;

namespace CricketStream.Application.Features.Scoring;

/// <summary>Ends an innings and, where the result is unambiguous, closes the match.
/// Anything else (draw, abandonment, rain results) is entered by hand via /complete.</summary>
public static class MatchCompletion
{
    public static void EndInnings(InningsWork w, bool declared = false)
    {
        var inn = w.Innings; var m = w.Match;
        inn.Status = InningsStatus.Completed;
        inn.IsDeclared = declared;
        inn.UpdatedAt = DateTimeOffset.UtcNow;
        if (m.Status is MatchStatus.Live or MatchStatus.InningsBreak) m.Status = MatchStatus.InningsBreak;
        TryAutoResult(m, inn);
    }

    private static void TryAutoResult(Match m, Innings cur)
    {
        var rules = m.MatchRules;
        var all = m.Innings.OrderBy(i => i.InningsNumber).ToList();
        Team TeamOf(Guid id) => id == m.HomeTeamId ? m.HomeTeam : m.AwayTeam;
        Guid Other(Guid id) => id == m.HomeTeamId ? m.AwayTeamId : m.HomeTeamId;
        int Xi(Guid team) => m.MatchPlayers.Count(p => p.TeamId == team && p.IsPlayingXI);

        void Win(Guid winner, string text)
        {
            m.Status = MatchStatus.Completed; m.ActualEnd = DateTimeOffset.UtcNow;
            m.ResultType = MatchResultType.Win; m.WinnerTeamId = winner;
            m.ResultText = $"{TeamOf(winner).Name} won by {text}";
        }
        void Tie()
        {
            m.Status = MatchStatus.Completed; m.ActualEnd = DateTimeOffset.UtcNow;
            m.ResultType = MatchResultType.Tie; m.WinnerTeamId = null; m.ResultText = "Match tied";
        }
        void Chase()
        {
            var t = cur.Target!.Value;
            if (cur.TotalRuns >= t)
            {
                var wk = Math.Max(0, Xi(cur.BattingTeamId) - 1 - cur.TotalWickets);
                Win(cur.BattingTeamId, $"{wk} wicket{(wk == 1 ? "" : "s")}");
            }
            else if (cur.TotalRuns == t - 1) Tie();
            else
            {
                var margin = t - 1 - cur.TotalRuns;
                Win(Other(cur.BattingTeamId), $"{margin} run{(margin == 1 ? "" : "s")}");
            }
        }

        if (!rules.IsTest)
        {
            if (all.Count >= Math.Max(1, rules.InningsPerSide) * 2 && cur.Target is not null) Chase();
            return;
        }

        if (cur.Target is not null) { Chase(); return; }

        // Test: innings victory = a side that has batted twice is still behind the other side's single innings.
        var mine = all.Where(i => i.BattingTeamId == cur.BattingTeamId && i.Status == InningsStatus.Completed).ToList();
        var theirs = all.Where(i => i.BattingTeamId != cur.BattingTeamId && i.Status == InningsStatus.Completed).ToList();
        if (!cur.IsDeclared && mine.Count == 2 && theirs.Count == 1 && mine.Sum(i => i.TotalRuns) < theirs[0].TotalRuns)
        {
            var margin = theirs[0].TotalRuns - mine.Sum(i => i.TotalRuns);
            Win(Other(cur.BattingTeamId), $"an innings and {margin} run{(margin == 1 ? "" : "s")}");
        }
    }
}
