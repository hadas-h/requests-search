namespace Requests.Infrastructure.Auth;

public sealed class JwtSettings
{
    public string Issuer { get; init; } = "Requests.Api";
    public string Audience { get; init; } = "Requests.Client";
    public string SigningKey { get; init; } = string.Empty;
    public int ExpiryMinutes { get; init; } = 480;
}
