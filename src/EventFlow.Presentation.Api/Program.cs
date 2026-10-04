using EventFlow.Infrastructure;
using EventFlow.Application;
using EventFlow.Domain.Exceptions;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddEventFlowInfrastructure(builder.Configuration);
builder.Services.AddApplicationServices();
//builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
//    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
//    { 
//        options.RequireHttpsMetadata = false;
//        options.TokenValidationParameters = new TokenValidationParameters
//        {
//            ValidateIssuer = true,
//            ValidateAudience = true,
//            ValidateLifetime = true,
//            ValidateIssuerSigningKey = true,
//            ValidIssuer = builder.Configuration["Authentication:Issuer"],
//            ValidAudience = builder.Configuration["Authentication:Audience"],
//            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(builder.Configuration["Authentication:SecretKey"]?? throw new InvalidOperationException("Authentication:SecretKey is not configured"))),
//            ClockSkew = TimeSpan.Zero // Optional: Set clock skew to zero to prevent token expiration issues

//        };
//    });
//builder.Services.AddAuthorization();


var app = builder.Build();
app.UseMiddleware<GlobalErrorHandler>();
app.UseAuthentication();
app.UseAuthorization();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapControllers();
app.MapGet("/", () => "I am Root");
app.MapGet("/secure", () => "I am Secure").RequireAuthorization();

app.Run();

// Global error handling 
public class GlobalErrorHandler
{
    private readonly RequestDelegate _next;

    public GlobalErrorHandler(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            switch (ex)
            {
                case AppException exception:
                    context.Response.StatusCode = (int) exception.StatusCode;
                    await context.Response.WriteAsJsonAsync(new { error = exception.Message });
                    break;
                
                
                default:
                    if (!context.Response.HasStarted)
                    {
                        context.Response.StatusCode = 500;
                        // Log the exception details here if needed for debugging purposes
                        Console.Error.WriteLine(ex);
                        await context.Response.WriteAsJsonAsync(new { error = "Internal Server Error" });
                    }
                    break;
            }
        }
    }
}

