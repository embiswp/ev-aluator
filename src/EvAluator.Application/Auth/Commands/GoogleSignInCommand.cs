using EvAluator.Application.Auth.DTOs;
using EvAluator.Domain.Services;
using EvAluator.Infrastructure.Authentication;
using EvAluator.Infrastructure.Configuration;
using EvAluator.Shared.Types;
using Microsoft.Extensions.Options;

namespace EvAluator.Application.Auth.Commands;

public sealed record GoogleSignInCommand(string IdToken);

public sealed class GoogleSignInCommandHandler
{
    private readonly GoogleAuthService _googleAuthService;
    private readonly IUserAuthenticationService _userAuthService;
    private readonly JwtTokenService _jwtTokenService;
    private readonly JwtOptions _jwtOptions;

    public GoogleSignInCommandHandler(
        GoogleAuthService googleAuthService,
        IUserAuthenticationService userAuthService,
        JwtTokenService jwtTokenService,
        IOptions<JwtOptions> jwtOptions)
    {
        _googleAuthService = googleAuthService;
        _userAuthService = userAuthService;
        _jwtTokenService = jwtTokenService;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<Result<AuthenticationResponse>> HandleAsync(GoogleSignInCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.IdToken))
            return Result<AuthenticationResponse>.Failure("ID token is required");

        var googleProfileResult = await _googleAuthService.ValidateTokenAsync(command.IdToken);
        if (googleProfileResult.IsFailure)
            return Result<AuthenticationResponse>.Failure(googleProfileResult.Error);

        var userResult = await _userAuthService.AuthenticateWithGoogleAsync(googleProfileResult.Value);
        if (userResult.IsFailure)
            return Result<AuthenticationResponse>.Failure(userResult.Error);

        var accessTokenResult = _jwtTokenService.GenerateAccessToken(userResult.Value);
        if (accessTokenResult.IsFailure)
            return Result<AuthenticationResponse>.Failure(accessTokenResult.Error);

        var refreshTokenResult = _jwtTokenService.GenerateRefreshToken();
        if (refreshTokenResult.IsFailure)
            return Result<AuthenticationResponse>.Failure(refreshTokenResult.Error);

        var user = userResult.Value;
        var response = new AuthenticationResponse(
            accessTokenResult.Value,
            refreshTokenResult.Value,
            DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes),
            new UserProfileDto(
                user.Id.Value.ToString(),
                user.Email,
                user.Name,
                user.PictureUrl));

        return Result<AuthenticationResponse>.Success(response);
    }
}