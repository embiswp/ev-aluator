using EvAluator.Domain.ValueObjects;
using EvAluator.Infrastructure.Configuration;
using EvAluator.Shared.Types;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;

namespace EvAluator.Infrastructure.Authentication;

public sealed class GoogleAuthService
{
    private readonly GoogleAuthOptions _options;

    public GoogleAuthService(IOptions<GoogleAuthOptions> options)
    {
        _options = options.Value;
    }

    public async Task<Result<GoogleProfile>> ValidateTokenAsync(string idToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(idToken))
                return Result<GoogleProfile>.Failure("ID token cannot be null or empty");

            var payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { _options.ClientId }
                });

            if (payload?.Subject == null || payload.Email == null || payload.Name == null)
                return Result<GoogleProfile>.Failure("Invalid token payload");

            var profile = GoogleProfile.Create(
                payload.Subject,
                payload.Email,
                payload.Name,
                payload.Picture);

            return Result<GoogleProfile>.Success(profile);
        }
        catch (InvalidJwtException ex)
        {
            return Result<GoogleProfile>.Failure($"Invalid JWT token: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result<GoogleProfile>.Failure($"Token validation failed: {ex.Message}");
        }
    }
}