using EvAluator.Domain.Entities;
using EvAluator.Domain.Repositories;
using EvAluator.Domain.Services;
using EvAluator.Domain.ValueObjects;
using EvAluator.Shared.Types;

namespace EvAluator.Infrastructure.Services;

public sealed class UserAuthenticationService : IUserAuthenticationService
{
    private readonly IUserRepository _userRepository;

    public UserAuthenticationService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<Result<User>> AuthenticateWithGoogleAsync(GoogleProfile profile)
    {
        var existingUserResult = await _userRepository.GetByGoogleIdAsync(profile.Id);
        
        if (existingUserResult.IsSuccess)
        {
            var existingUser = existingUserResult.Value;
            
            existingUser.UpdateProfile(profile.Name, profile.Picture);
            existingUser.RecordLogin();
            
            var updateResult = await _userRepository.UpdateAsync(existingUser);
            return updateResult.IsSuccess 
                ? Result<User>.Success(updateResult.Value)
                : Result<User>.Failure($"Failed to update user: {updateResult.Error}");
        }

        var newUser = User.Create(profile.Id, profile.Email, profile.Name, profile.Picture);
        newUser.RecordLogin();
        
        var createResult = await _userRepository.CreateAsync(newUser);
        return createResult.IsSuccess 
            ? Result<User>.Success(createResult.Value)
            : Result<User>.Failure($"Failed to create user: {createResult.Error}");
    }

    public async Task<Result<User>> GetUserByIdAsync(UserId userId)
    {
        return await _userRepository.GetByIdAsync(userId);
    }

    public async Task<Result<User>> GetUserByGoogleIdAsync(string googleId)
    {
        return await _userRepository.GetByGoogleIdAsync(googleId);
    }
}