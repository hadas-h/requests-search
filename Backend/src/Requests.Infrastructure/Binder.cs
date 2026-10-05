using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Requests.Application.Auth;
using Requests.Domain.Interfaces;
using Requests.Infrastructure.Auth;
using Requests.Infrastructure.Persistence;
using Requests.Infrastructure.Repositories;

namespace Requests.Infrastructure;

/// <summary>
/// Infrastructure-layer DI composition. Registers the EF Core <see cref="RequestsDbContext"/>
/// (InMemory provider), repository implementations, and the JWT token issuer. Following the
/// platform convention, each layer exposes a <c>Binder</c> with <c>Use*</c> extension methods
/// composed by the API host.
/// </summary>
public static class Binder
{
    public static IServiceCollection UseInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<RequestsDbContext>(options =>
            options.UseInMemoryDatabase("CandidateRequests"));

        services.AddScoped<IRequestRepository, RequestRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        // JWT token issuing is an infrastructure adapter (depends on System.IdentityModel.Tokens.Jwt).
        // Bearer *validation* remains an API host concern, wired in Program.cs.
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.AddScoped<ITokenService, JwtTokenService>();

        return services;
    }
}
