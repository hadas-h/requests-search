using Requests.Application.Auth;
using Requests.Domain.Entities;
using Requests.Infrastructure.Persistence;

namespace Requests.Api.Auth;

/// <summary>
/// Seeds a default administrator account on startup so there is always an admin to sign in with.
/// Credentials come from the <c>Seed:Admin</c> configuration section; the password is stored only
/// as a salted hash. Regular users register themselves through <c>/api/auth/register</c>.
/// </summary>
public static class UserSeeder
{
    public static void SeedAdmin(RequestsDbContext db, IConfiguration configuration)
    {
        var username = configuration["Seed:Admin:Username"] ?? "admin";
        var password = configuration["Seed:Admin:Password"] ?? "admin";

        if (db.Users.Any(u => u.Username == username))
        {
            return;
        }

        db.Users.Add(new User
        {
            Username = username,
            IsAdministrator = true,
            PasswordHash = PasswordHasher.Hash(password),
        });
        db.SaveChanges();
    }
}
