using Microsoft.EntityFrameworkCore;
using Requests.Domain.Entities;

namespace Requests.Infrastructure.Persistence;

public sealed class RequestsDbContext : DbContext
{
    public RequestsDbContext(DbContextOptions<RequestsDbContext> options) : base(options)
    {
    }

    public DbSet<Request> Requests => Set<Request>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Usernames are unique — used as the login identifier.
        modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();

        modelBuilder.Entity<Request>(entity =>
        {
            // Composite indexes supporting query-side authorization combined with the
            // default sort (CreatedAt) and the Id tie-breaker used by keyset pagination.
            // Ordering the columns (filter/scope, sort, tie-breaker) lets a relational
            // provider satisfy the seek predicate with a single index seek.
            entity.HasIndex(r => new { r.OwnerId, r.CreatedAt, r.Id });
            entity.HasIndex(r => new { r.AssignedToUserId, r.CreatedAt, r.Id });

            // Single-column indexes supporting the status, type, and request-number filters.
            entity.HasIndex(r => r.Status);
            entity.HasIndex(r => r.RequestType);
            entity.HasIndex(r => r.RequestNumber);
        });
    }
}
