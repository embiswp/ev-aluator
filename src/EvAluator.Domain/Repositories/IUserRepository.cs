using EvAluator.Domain.Entities;
using EvAluator.Domain.ValueObjects;
using EvAluator.Shared.Types;

namespace EvAluator.Domain.Repositories;

public interface IUserRepository
{
    Task<Result<User>> GetByIdAsync(UserId userId);
    Task<Result<User>> GetByGoogleIdAsync(string googleId);
    Task<Result<User>> CreateAsync(User user);
    Task<Result<User>> UpdateAsync(User user);
    Task<Result<Unit>> DeleteAsync(UserId userId);
}