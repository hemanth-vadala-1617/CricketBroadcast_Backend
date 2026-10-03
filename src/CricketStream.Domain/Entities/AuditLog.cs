using CricketStream.Domain.Common;
namespace CricketStream.Domain.Entities;
public class AuditLog : BaseEntity {
    public string UserId { get; set; } = string.Empty;   // always from the JWT
    public Guid? MatchId { get; set; }
    public string ActionType { get; set; } = string.Empty; // Undo, Edit, Penalty, Retire ...
    public string EntityName { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string PreviousStateJson { get; set; } = string.Empty;
    public string? AfterStateJson { get; set; }
    public string Reason { get; set; } = string.Empty;
}
