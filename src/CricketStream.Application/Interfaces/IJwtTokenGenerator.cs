using System.Security.Claims;
namespace CricketStream.Application.Interfaces;
public interface IJwtTokenGenerator {
    string GenerateToken(string userId, string email, IList<string> roles);
    string GenerateRefreshToken();
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}
