using EvAluator.Domain.Entities;
using EvAluator.Infrastructure.Authentication;
using EvAluator.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace EvAluator.UnitTests.Authentication;

public sealed class JwtTokenServiceTests
{
    private readonly JwtTokenService _service;
    private readonly User _testUser;

    public JwtTokenServiceTests()
    {
        var options = Options.Create(new JwtOptions 
        { 
            SecretKey = "test-secret-key-that-is-at-least-32-characters-long",
            Issuer = "test-issuer",
            Audience = "test-audience",
            ExpiryMinutes = 60
        });
        _service = new JwtTokenService(options);
        _testUser = User.Create("google-123", "test@example.com", "Test User");
    }

    [Fact]
    public void GenerateAccessToken_WithValidUser_ReturnsSuccess()
    {
        var result = _service.GenerateAccessToken(_testUser);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.NotEmpty(result.Value);
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsSuccess()
    {
        var result = _service.GenerateRefreshToken();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.NotEmpty(result.Value);
    }

    [Fact]
    public void ValidateToken_WithInvalidToken_ReturnsFailure()
    {
        var result = _service.ValidateToken("invalid-token");

        Assert.True(result.IsFailure);
        Assert.Contains("Invalid token", result.Error);
    }

    [Fact]
    public void ValidateToken_WithValidToken_ReturnsSuccess()
    {
        var tokenResult = _service.GenerateAccessToken(_testUser);
        Assert.True(tokenResult.IsSuccess);

        var validationResult = _service.ValidateToken(tokenResult.Value);

        Assert.True(validationResult.IsSuccess);
        Assert.NotNull(validationResult.Value);
    }
}