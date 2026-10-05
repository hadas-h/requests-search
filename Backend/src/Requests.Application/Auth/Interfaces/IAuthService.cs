namespace Requests.Application.Auth;

/// <summary>Registration, login, and token issuing for application users.</summary>
public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}
