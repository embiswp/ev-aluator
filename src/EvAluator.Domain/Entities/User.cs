using EvAluator.Domain.ValueObjects;

namespace EvAluator.Domain.Entities;

public sealed class User
{
    public UserId Id { get; private set; }
    public string GoogleId { get; private set; }
    public string Email { get; private set; }
    public string Name { get; private set; }
    public string? PictureUrl { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public bool IsActive { get; private set; }

    private User() { }

    private User(
        UserId id,
        string googleId,
        string email,
        string name,
        string? pictureUrl,
        DateTime createdAt)
    {
        Id = id;
        GoogleId = googleId;
        Email = email;
        Name = name;
        PictureUrl = pictureUrl;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        IsActive = true;
    }

    public static User Create(
        string googleId,
        string email,
        string name,
        string? pictureUrl = null)
    {
        if (string.IsNullOrWhiteSpace(googleId))
            throw new ArgumentException("Google ID cannot be null or empty", nameof(googleId));
        
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be null or empty", nameof(email));
        
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or empty", nameof(name));

        return new User(
            UserId.New(),
            googleId,
            email,
            name,
            pictureUrl,
            DateTime.UtcNow);
    }

    public void UpdateProfile(string name, string? pictureUrl)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or empty", nameof(name));

        Name = name;
        PictureUrl = pictureUrl;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reactivate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }
}