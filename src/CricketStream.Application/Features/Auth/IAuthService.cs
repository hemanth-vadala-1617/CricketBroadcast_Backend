using CricketStream.Application.Common;
using CricketStream.Contracts.Auth;
namespace CricketStream.Application.Features.Auth;
public interface IAuthService {
    Task<Result<AuthResponse>> LoginAsync(LoginRequest request);
    Task<Result<AuthResponse>> RefreshTokenAsync(RefreshTokenRequest request);
    Task LogoutAsync(string refreshToken);
}
