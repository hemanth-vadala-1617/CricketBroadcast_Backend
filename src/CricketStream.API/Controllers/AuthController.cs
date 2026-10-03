using CricketStream.Application.Features.Auth;
using CricketStream.Contracts.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace CricketStream.API.Controllers;

[Route("api/[controller]")]
public class AuthController : ApiControllerBase {
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) { _auth = auth; }

    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginRequest request) => From(await _auth.LoginAsync(request));

    [HttpPost("refresh"), AllowAnonymous, EnableRateLimiting("login")]
    public async Task<IActionResult> Refresh(RefreshTokenRequest request) => From(await _auth.RefreshTokenAsync(request));

    [HttpPost("logout"), AllowAnonymous]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request) {
        await _auth.LogoutAsync(request.RefreshToken);
        return NoContent();
    }

    [HttpGet("me"), Authorize]
    public IActionResult Me() => Ok(new {
        userId = UserId,
        email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email"),
        roles = User.FindAll(ClaimTypes.Role).Select(r => r.Value).ToList(),
    });
}
