using Requests.Domain.Entities;

namespace Requests.Domain.Interfaces;

/// <summary>Data access for application users (registration and login lookups).</summary>
public interface IUserRepository
{
    Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string username, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
}
