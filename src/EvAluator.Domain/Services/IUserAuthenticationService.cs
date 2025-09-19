using EvAluator.Domain.Entities;
using EvAluator.Domain.ValueObjects;
using EvAluator.Shared.Types;

namespace EvAluator.Domain.Services;

public interface IUserAuthenticationService
{
    Task<Result<User>> AuthenticateWithGoogleAsync(GoogleProfile profile);
    Task<Result<User>> GetUserByIdAsync(UserId userId);
    Task<Result<User>> GetUserByGoogleIdAsync(string googleId);
}