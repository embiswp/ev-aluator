using EvAluator.Infrastructure.Authentication;
using System.Security.Claims;

namespace EvAluator.Api.Middleware;

public sealed class JwtAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly JwtTokenService _jwtTokenService;

    public JwtAuthenticationMiddleware(RequestDelegate next, JwtTokenService jwtTokenService)
    {
        _next = next;
        _jwtTokenService = jwtTokenService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var token = ExtractTokenFromRequest(context.Request);
        
        if (!string.IsNullOrEmpty(token))
        {
            var validationResult = _jwtTokenService.ValidateToken(token);
            if (validationResult.IsSuccess)
            {
                context.User = validationResult.Value;
            }
        }

        await _next(context);
    }

    private static string? ExtractTokenFromRequest(HttpRequest request)
    {
        if (request.Cookies.TryGetValue("access_token", out var cookieToken))
            return cookieToken;

        var authHeader = request.Headers.Authorization.FirstOrDefault();
        if (authHeader?.StartsWith("Bearer ") == true)
            return authHeader.Substring("Bearer ".Length).Trim();

        return null;
    }
}