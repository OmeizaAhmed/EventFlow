using Microsoft.Extensions.DependencyInjection;
using EventFlow.Infrastructure.Persistence;
using EventFlow.Infrastructure.Services;
using EventFlow.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using EventFlow.Domain.Interfaces;
using EventFlow.Infrastructure.Repositories;
namespace EventFlow.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddEventFlowInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Register your infrastructure services here
        services.AddDbContext<EventFlowDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITokenService, TokenService>();
    
        return services;
    }
}