using Microsoft.Extensions.DependencyInjection;
using EventFlow.Application.Interfaces;
using EventFlow.Application.Services;

namespace EventFlow.Application;
public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}