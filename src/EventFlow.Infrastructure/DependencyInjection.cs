using Microsoft.Extensions.DependencyInjection;
using EventFlow.Infrastructure.Persistence;
using EventFlow.Infrastructure.Services;
using EventFlow.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using EventFlow.Domain.Interfaces;
using EventFlow.Infrastructure.Repositories;
using EventFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
namespace EventFlow.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddEventFlowInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Register your infrastructure services here
        services.AddDbContext<EventFlowDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));
        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 8;

            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;
        })
            .AddEntityFrameworkStores<EventFlowDbContext>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IIdentityRepository, IdentityRepository>();
        services.AddScoped<ITokenService, TokenService>();
    
        return services;
    }
}