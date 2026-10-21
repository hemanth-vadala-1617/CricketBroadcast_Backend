using CricketStream.Domain.Common;
namespace CricketStream.Domain.Entities;
public class MatchRules : BaseEntity {
    public string Name { get; set; } = string.Empty;
    public bool IsTest { get; set; }
    public int BallsPerOver { get; set; } = 6;
    public int OversPerInnings { get; set; } = 20;      // 0 = unlimited (Test)
    public int MaxOversPerBowler { get; set; } = 4;     // 0 = unlimited (Test)
    public int PowerplayOvers { get; set; } = 6;        // 0 = none
    public int WideRuns { get; set; } = 1;
    public int NoBallRuns { get; set; } = 1;
    public bool FreeHitEnabled { get; set; } = true;
    public bool DRSEnabled { get; set; }
    public bool FollowOnEnabled { get; set; }
    public int InningsPerSide { get; set; } = 1;        // 2 for Test
    public int Days { get; set; } = 1;
    public int OversPerDay { get; set; }                // 90 for Test, 0 otherwise
    public int SessionsPerDay { get; set; } = 1;        // 3 for Test
    public int NewBallAfterOvers { get; set; }          // 80 for Test, 0 otherwise
    public int FollowOnMargin { get; set; } = 200;
    // BBL "Power Surge": the batting side picks a block of overs with the powerplay field restrictions back on.
    public bool PowerSurgeEnabled { get; set; }
    public int PowerSurgeOvers { get; set; } = 2;        // length of the block
    public int PowerSurgeFromOver { get; set; } = 11;    // earliest over it may begin (the over number, 1-based)
    public int PowerSurgeToOver { get; set; } = 16;      // latest over it may begin
}
