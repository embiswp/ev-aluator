using EvAluator.Domain.Entities;
using Xunit;

namespace EvAluator.UnitTests.Entities;

public sealed class UserTests
{
    [Fact]
    public void Create_WithValidData_CreatesUser()
    {
        var user = User.Create("google-123", "test@example.com", "Test User", "https://example.com/pic.jpg");

        Assert.NotNull(user);
        Assert.Equal("google-123", user.GoogleId);
        Assert.Equal("test@example.com", user.Email);
        Assert.Equal("Test User", user.Name);
        Assert.Equal("https://example.com/pic.jpg", user.PictureUrl);
        Assert.True(user.IsActive);
        Assert.True(user.CreatedAt <= DateTime.UtcNow);
        Assert.True(user.UpdatedAt <= DateTime.UtcNow);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidGoogleId_ThrowsArgumentException(string googleId)
    {
        Assert.Throws<ArgumentException>(() => 
            User.Create(googleId, "test@example.com", "Test User"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidEmail_ThrowsArgumentException(string email)
    {
        Assert.Throws<ArgumentException>(() => 
            User.Create("google-123", email, "Test User"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidName_ThrowsArgumentException(string name)
    {
        Assert.Throws<ArgumentException>(() => 
            User.Create("google-123", "test@example.com", name));
    }

    [Fact]
    public void UpdateProfile_WithValidData_UpdatesProfile()
    {
        var user = User.Create("google-123", "test@example.com", "Test User");
        var originalUpdateTime = user.UpdatedAt;

        Thread.Sleep(1);
        user.UpdateProfile("Updated Name", "https://new-pic.com/pic.jpg");

        Assert.Equal("Updated Name", user.Name);
        Assert.Equal("https://new-pic.com/pic.jpg", user.PictureUrl);
        Assert.True(user.UpdatedAt > originalUpdateTime);
    }

    [Fact]
    public void RecordLogin_UpdatesLastLoginTime()
    {
        var user = User.Create("google-123", "test@example.com", "Test User");
        Assert.Null(user.LastLoginAt);

        user.RecordLogin();

        Assert.NotNull(user.LastLoginAt);
        Assert.True(user.LastLoginAt <= DateTime.UtcNow);
    }

    [Fact]
    public void Deactivate_SetsIsActiveToFalse()
    {
        var user = User.Create("google-123", "test@example.com", "Test User");
        Assert.True(user.IsActive);

        user.Deactivate();

        Assert.False(user.IsActive);
    }

    [Fact]
    public void Reactivate_SetsIsActiveToTrue()
    {
        var user = User.Create("google-123", "test@example.com", "Test User");
        user.Deactivate();
        Assert.False(user.IsActive);

        user.Reactivate();

        Assert.True(user.IsActive);
    }
}