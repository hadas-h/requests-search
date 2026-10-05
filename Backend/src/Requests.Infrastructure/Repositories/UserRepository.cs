using Microsoft.EntityFrameworkCore;
using Requests.Application.Auth;
using Requests.Domain.Entities;
using Requests.Domain.Interfaces;
using Requests.Infrastructure.Persistence;

namespace Requests.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly RequestsDbContext _db;

    public UserRepository(RequestsDbContext db)
    {
        _db = db;
    }

    public Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default)
        => _db.Users.FirstOrDefaultAsync(u => u.Username == username, cancellationToken);

    public Task<bool> ExistsAsync(string username, CancellationToken cancellationToken = default)
        => _db.Users.AnyAsync(u => u.Username == username, cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
