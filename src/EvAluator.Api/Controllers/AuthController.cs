using EvAluator.Application.Auth.Commands;
using EvAluator.Application.Auth.DTOs;
using EvAluator.Application.Auth.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EvAluator.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly GoogleSignInCommandHandler _googleSignInHandler;
    private readonly GetUserProfileQueryHandler _getUserProfileHandler;

    public AuthController(
        GoogleSignInCommandHandler googleSignInHandler,
        GetUserProfileQueryHandler getUserProfileHandler)
    {
        _googleSignInHandler = googleSignInHandler;
        _getUserProfileHandler = getUserProfileHandler;
    }

    [HttpPost("google-signin")]
    public async Task<IActionResult> GoogleSignIn([FromBody] GoogleSignInRequest request)
    {
        var command = new GoogleSignInCommand(request.IdToken);
        var result = await _googleSignInHandler.HandleAsync(command);

        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        var response = result.Value;
        
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = response.ExpiresAt
        };

        Response.Cookies.Append("access_token", response.AccessToken, cookieOptions);
        
        var refreshCookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddDays(30)
        };

        Response.Cookies.Append("refresh_token", response.RefreshToken, refreshCookieOptions);

        return Ok(new
        {
            success = true,
            user = response.User,
            expiresAt = response.ExpiresAt
        });
    }

    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> GetProfile()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { error = "User ID not found in token" });

        var query = new GetUserProfileQuery(userId);
        var result = await _getUserProfileHandler.HandleAsync(query);

        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return Ok(new { user = result.Value });
    }

    [HttpPost("signout")]
    [Authorize]
    public IActionResult SignOut()
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddDays(-1)
        };

        Response.Cookies.Append("access_token", "", cookieOptions);
        Response.Cookies.Append("refresh_token", "", cookieOptions);

        return Ok(new { success = true, message = "Signed out successfully" });
    }
}