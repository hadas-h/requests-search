using Microsoft.Extensions.DependencyInjection;
using Requests.Application.Auth;
using Requests.Application.Requests;

namespace Requests.Application;

/// <summary>
/// Application-layer DI composition. Registers use-case services, the query validator, and
/// authentication services. Following the platform convention, each layer exposes a
/// <c>Binder</c> with <c>Use*</c> extension methods composed by the API host.
/// </summary>
public static class Binder
{
    public static IServiceCollection UseApplication(this IServiceCollection services)
    {
        return services
            .AddScoped<IRequestService, RequestService>()
            .AddScoped<RequestQueryValidator>()
            .AddScoped<IAuthService, AuthService>();
    }
}
