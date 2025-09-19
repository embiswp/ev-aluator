using EvAluator.Infrastructure.Authentication;
using EvAluator.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace EvAluator.UnitTests.Authentication;

public sealed class GoogleAuthServiceTests
{
    private readonly GoogleAuthService _service;

    public GoogleAuthServiceTests()
    {
        var options = Options.Create(new GoogleAuthOptions 
        { 
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret"
        });
        _service = new GoogleAuthService(options);
    }

    [Fact]
    public async Task ValidateTokenAsync_WithNullToken_ReturnsFailure()
    {
        var result = await _service.ValidateTokenAsync(null!);

        Assert.True(result.IsFailure);
        Assert.Contains("null or empty", result.Error);
    }

    [Fact]
    public async Task ValidateTokenAsync_WithEmptyToken_ReturnsFailure()
    {
        var result = await _service.ValidateTokenAsync(string.Empty);

        Assert.True(result.IsFailure);
        Assert.Contains("null or empty", result.Error);
    }

    [Fact]
    public async Task ValidateTokenAsync_WithInvalidToken_ReturnsFailure()
    {
        var result = await _service.ValidateTokenAsync("invalid-token");

        Assert.True(result.IsFailure);
        Assert.Contains("validation failed", result.Error);
    }
}