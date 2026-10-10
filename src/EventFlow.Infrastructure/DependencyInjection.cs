using EventFlow.Application.Interfaces;
using EventFlow.Domain.Interfaces;
using EventFlow.Infrastructure.Identity;
using EventFlow.Infrastructure.Persistence;
using EventFlow.Infrastructure.Repositories;
using EventFlow.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
namespace EventFlow.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddEventFlowInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Register your infrastructure services here
        // DB Context
        services.AddDbContext<EventFlowDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));
        // Identity
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
        // Repositories 
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IIdentityRepository, IdentityRepository>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IRefreshRepository, RefreshRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();

        // Authentication and Authorization

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = configuration["JwtSettings:Issuer"],
            ValidAudience = configuration["JwtSettings:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(configuration["JwtSettings:SecretKey"] ?? throw new InvalidOperationException("JwtSettings:SecretKey is not configured"))),
            ClockSkew = TimeSpan.Zero // Optional: Set clock skew to zero to prevent token expiration issues

        };
    });
        services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder(
                    JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .Build();

            options.AddPolicy("ValidUserId", policy =>
            {
                policy.Combine(options.DefaultPolicy);

                policy.RequireAssertion(context =>
                {
                    var userId = context.User
                        .FindFirst(ClaimTypes.NameIdentifier)?.Value;

                    return Guid.TryParse(userId, out _);
                });
            });
        });


        return services;
    }
}