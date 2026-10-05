namespace Requests.Application.Auth;

/// <summary>Raised for expected auth failures (duplicate username, bad credentials) so the API can map them to 400/401.</summary>
public sealed class AuthException : Exception
{
    public AuthException(string message) : base(message)
    {
    }
}
