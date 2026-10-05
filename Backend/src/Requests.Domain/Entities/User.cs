namespace Requests.Domain.Entities;

/// <summary>
/// An application user who can sign in. The <see cref="PasswordHash"/> stores a hashed password
/// (never plaintext), and <see cref="IsAdministrator"/> determines whether the user sees all
/// Requests or only their own. The role is stored server-side and cannot be set by the client.
/// </summary>
public sealed class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsAdministrator { get; set; }
}
