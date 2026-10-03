using System.Security.Cryptography;
using System.Text;
using CricketStream.Application.Common;
using CricketStream.Application.Features.Auth;
using CricketStream.Application.Interfaces;
using CricketStream.Contracts.Auth;
using CricketStream.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
namespace CricketStream.Infrastructure.Identity;

public class AuthService : IAuthService {
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IJwtTokenGenerator _jwt;
    private readonly ICricketDbContext _context;
    private readonly int _refreshDays;

    public AuthService(UserManager<IdentityUser> userManager, IJwtTokenGenerator jwt, ICricketDbContext context, IConfiguration config) {
        _userManager = userManager; _jwt = jwt; _context = context;
        _refreshDays = int.TryParse(config["JwtSettings:RefreshExpiryDays"], out var d) ? d : 7;
    }

    /// <summary>Only the hash of a refresh token is stored, so a database leak does not leak sessions.</summary>
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static readonly Error BadCredentials = Fail.Validation("Invalid email or password.");

    private async Task<AuthResponse> IssueAsync(IdentityUser user) {
        var roles = await _userManager.GetRolesAsync(user);
        var refresh = _jwt.GenerateRefreshToken();
        _context.RefreshTokens.Add(new RefreshToken { UserId = user.Id, Token = Hash(refresh), ExpiresAt = DateTimeOffset.UtcNow.AddDays(_refreshDays) });
        await _context.SaveChangesAsync();
        return new AuthResponse { Token = _jwt.GenerateToken(user.Id, user.Email!, roles), RefreshToken = refresh, UserId = user.Id, Email = user.Email!, Roles = roles };
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request) {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password)) return Result<AuthResponse>.Fail(BadCredentials);
        var user = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null) return Result<AuthResponse>.Fail(BadCredentials);
        if (await _userManager.IsLockedOutAsync(user))
            return Result<AuthResponse>.Fail(Fail.Unprocessable("Too many failed attempts. Try again in a few minutes."));
        if (!await _userManager.CheckPasswordAsync(user, request.Password)) {
            await _userManager.AccessFailedAsync(user);
            return Result<AuthResponse>.Fail(BadCredentials);
        }
        await _userManager.ResetAccessFailedCountAsync(user);
        return Result<AuthResponse>.Ok(await IssueAsync(user));
    }

    public async Task<Result<AuthResponse>> RefreshTokenAsync(RefreshTokenRequest request) {
        var invalid = Result<AuthResponse>.Fail(Fail.Validation("Invalid token."));
        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.RefreshToken)) return invalid;
        var principal = _jwt.GetPrincipalFromExpiredToken(request.Token);
        var userId = principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return invalid;
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return invalid;

        var hash = Hash(request.RefreshToken);
        var saved = await _context.RefreshTokens.FirstOrDefaultAsync(r => r.Token == hash && r.UserId == user.Id);
        if (saved is null || saved.IsRevoked || saved.ExpiresAt <= DateTimeOffset.UtcNow) return invalid;
        saved.IsRevoked = true;
        return Result<AuthResponse>.Ok(await IssueAsync(user));
    }

    public async Task LogoutAsync(string refreshToken) {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;
        var hash = Hash(refreshToken);
        var rt = await _context.RefreshTokens.FirstOrDefaultAsync(r => r.Token == hash);
        if (rt is not null) { rt.IsRevoked = true; await _context.SaveChangesAsync(); }
    }
}

