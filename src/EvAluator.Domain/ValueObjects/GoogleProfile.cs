namespace EvAluator.Domain.ValueObjects;

public sealed record GoogleProfile(
    string Id,
    string Email,
    string Name,
    string? Picture)
{
    public static GoogleProfile Create(string id, string email, string name, string? picture = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Google ID cannot be null or empty", nameof(id));
        
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be null or empty", nameof(email));
        
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or empty", nameof(name));

        return new GoogleProfile(id, email, name, picture);
    }
}