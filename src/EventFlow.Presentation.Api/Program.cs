using EventFlow.Infrastructure;
using EventFlow.Application;
using EventFlow.Domain.Exceptions;
using Scalar.AspNetCore;
using EventFlow.Presentation.Api.Middlewares;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting up");
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddSerilog((services, configuration) => configuration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services));

    builder.Services.AddOpenApi();
    builder.Services.AddControllers();
    builder.Services.AddEventFlowInfrastructure(builder.Configuration);
    builder.Services.AddApplicationServices();

    var app = builder.Build();
    app.UseMiddleware<GlobalErrorHandler>();
    app.UseHttpsRedirection();
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


    app.Run();

}
catch (Exception ex)
{
    Log.Fatal(ex, "Application start-up failed");
}
finally
{
    Log.Information("Shut down complete");
    Log.CloseAndFlush();
}


