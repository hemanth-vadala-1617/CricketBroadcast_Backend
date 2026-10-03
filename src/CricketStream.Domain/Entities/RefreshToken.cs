using CricketStream.Domain.Common;
namespace CricketStream.Domain.Entities;
public class RefreshToken : BaseEntity {
    public string UserId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
}
