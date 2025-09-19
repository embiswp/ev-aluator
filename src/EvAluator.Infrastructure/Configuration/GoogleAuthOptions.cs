namespace EvAluator.Infrastructure.Configuration;

public sealed class GoogleAuthOptions
{
    public const string SectionName = "GoogleAuth";
    
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}