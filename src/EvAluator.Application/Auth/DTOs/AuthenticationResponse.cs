namespace EvAluator.Application.Auth.DTOs;

public sealed record AuthenticationResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserProfileDto User);

public sealed record UserProfileDto(
    string Id,
    string Email,
    string Name,
    string? PictureUrl);