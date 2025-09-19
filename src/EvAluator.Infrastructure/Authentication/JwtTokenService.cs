using EvAluator.Domain.Entities;
using EvAluator.Infrastructure.Configuration;
using EvAluator.Shared.Types;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace EvAluator.Infrastructure.Authentication;

public sealed class JwtTokenService
{
    private readonly JwtOptions _options;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private readonly byte[] _secretKey;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        _tokenHandler = new JwtSecurityTokenHandler();
        _secretKey = Encoding.UTF8.GetBytes(_options.SecretKey);
    }

    public Result<string> GenerateAccessToken(User user)
    {
        try
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.Value.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim("google_id", user.GoogleId),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, 
                    new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString(), 
                    ClaimValueTypes.Integer64)
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes),
                Issuer = _options.Issuer,
                Audience = _options.Audience,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(_secretKey),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = _tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = _tokenHandler.WriteToken(token);

            return Result<string>.Success(tokenString);
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"Failed to generate access token: {ex.Message}");
        }
    }

    public Result<string> GenerateRefreshToken()
    {
        try
        {
            var randomBytes = new byte[32];
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            
            var refreshToken = Convert.ToBase64String(randomBytes);
            return Result<string>.Success(refreshToken);
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"Failed to generate refresh token: {ex.Message}");
        }
    }

    public Result<ClaimsPrincipal> ValidateToken(string token)
    {
        try
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _options.Issuer,
                ValidAudience = _options.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(_secretKey),
                ClockSkew = TimeSpan.Zero
            };

            var principal = _tokenHandler.ValidateToken(token, tokenValidationParameters, out _);
            return Result<ClaimsPrincipal>.Success(principal);
        }
        catch (SecurityTokenExpiredException)
        {
            return Result<ClaimsPrincipal>.Failure("Token has expired");
        }
        catch (SecurityTokenException ex)
        {
            return Result<ClaimsPrincipal>.Failure($"Invalid token: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result<ClaimsPrincipal>.Failure($"Token validation failed: {ex.Message}");
        }
    }
}