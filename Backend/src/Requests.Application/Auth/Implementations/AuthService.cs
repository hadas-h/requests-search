using Requests.Domain.Entities;
using Requests.Domain.Interfaces;

namespace Requests.Application.Auth;

/// <summary>
/// Registration and login. Passwords are stored only as salted PBKDF2 hashes, and the issued JWT
/// carries the user's id and role — so the client can never elevate itself.
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly ITokenService _tokens;

    public AuthService(IUserRepository users, ITokenService tokens)
    {
        _users = users;
        _tokens = tokens;
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var username = request.Username?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new AuthException("Username and password are required.");
        }

        if (await _users.ExistsAsync(username, cancellationToken))
        {
            throw new AuthException("That username is already taken.");
        }

        var user = new User
        {
            Username = username,
            IsAdministrator = false,
            PasswordHash = PasswordHasher.Hash(request.Password),
        };

        await _users.AddAsync(user, cancellationToken);

        return new AuthResult(_tokens.CreateToken(user), ToDto(user));
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var username = request.Username?.Trim() ?? string.Empty;
        var user = await _users.FindByUsernameAsync(username, cancellationToken);

        if (user is null || !PasswordHasher.Verify(request.Password ?? string.Empty, user.PasswordHash))
        {
            throw new AuthException("Invalid username or password.");
        }

        return new AuthResult(_tokens.CreateToken(user), ToDto(user));
    }

    private static UserDto ToDto(User user) => new(user.Id, user.Username, user.IsAdministrator);
}
