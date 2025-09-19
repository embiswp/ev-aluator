using EvAluator.Domain.Entities;
using EvAluator.Domain.Repositories;
using EvAluator.Domain.ValueObjects;
using EvAluator.Infrastructure.Data;
using EvAluator.Shared.Types;
using Microsoft.EntityFrameworkCore;

namespace EvAluator.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly EvAluatorDbContext _context;

    public UserRepository(EvAluatorDbContext context)
    {
        _context = context;
    }

    public async Task<Result<User>> GetByIdAsync(UserId userId)
    {
        try
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId);

            return user == null 
                ? Result<User>.Failure("User not found")
                : Result<User>.Success(user);
        }
        catch (Exception ex)
        {
            return Result<User>.Failure($"Failed to retrieve user: {ex.Message}");
        }
    }

    public async Task<Result<User>> GetByGoogleIdAsync(string googleId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(googleId))
                return Result<User>.Failure("Google ID cannot be null or empty");

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.GoogleId == googleId);

            return user == null 
                ? Result<User>.Failure("User not found")
                : Result<User>.Success(user);
        }
        catch (Exception ex)
        {
            return Result<User>.Failure($"Failed to retrieve user: {ex.Message}");
        }
    }

    public async Task<Result<User>> CreateAsync(User user)
    {
        try
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return Result<User>.Success(user);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_Users_GoogleId") == true)
        {
            return Result<User>.Failure("A user with this Google ID already exists");
        }
        catch (Exception ex)
        {
            return Result<User>.Failure($"Failed to create user: {ex.Message}");
        }
    }

    public async Task<Result<User>> UpdateAsync(User user)
    {
        try
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
            return Result<User>.Success(user);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<User>.Failure("User was modified by another process");
        }
        catch (Exception ex)
        {
            return Result<User>.Failure($"Failed to update user: {ex.Message}");
        }
    }

    public async Task<Result<Unit>> DeleteAsync(UserId userId)
    {
        try
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return Result<Unit>.Failure("User not found");

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            
            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit>.Failure($"Failed to delete user: {ex.Message}");
        }
    }
}