namespace EvAluator.Domain.ValueObjects;

public sealed record UserId(Guid Value)
{
    public static UserId New() => new(Guid.NewGuid());
    
    public static UserId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty", nameof(value));
        
        return new UserId(value);
    }

    public static UserId From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("User ID cannot be null or empty", nameof(value));
        
        if (!Guid.TryParse(value, out var guid))
            throw new ArgumentException("Invalid User ID format", nameof(value));
        
        return From(guid);
    }

    public override string ToString() => Value.ToString();
}