using EvAluator.Application.Auth.DTOs;
using EvAluator.Domain.Services;
using EvAluator.Domain.ValueObjects;
using EvAluator.Shared.Types;

namespace EvAluator.Application.Auth.Queries;

public sealed record GetUserProfileQuery(string UserId);

public sealed class GetUserProfileQueryHandler
{
    private readonly IUserAuthenticationService _userAuthService;

    public GetUserProfileQueryHandler(IUserAuthenticationService userAuthService)
    {
        _userAuthService = userAuthService;
    }

    public async Task<Result<UserProfileDto>> HandleAsync(GetUserProfileQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.UserId))
            return Result<UserProfileDto>.Failure("User ID is required");

        if (!Guid.TryParse(query.UserId, out var userGuid))
            return Result<UserProfileDto>.Failure("Invalid user ID format");

        var userId = UserId.From(userGuid);
        var userResult = await _userAuthService.GetUserByIdAsync(userId);

        if (userResult.IsFailure)
            return Result<UserProfileDto>.Failure(userResult.Error);

        var user = userResult.Value;
        var profile = new UserProfileDto(
            user.Id.Value.ToString(),
            user.Email,
            user.Name,
            user.PictureUrl);

        return Result<UserProfileDto>.Success(profile);
    }
}