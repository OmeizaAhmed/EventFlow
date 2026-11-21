using EventFlow.Infrastructure;
using EventFlow.Application;
using EventFlow.Domain.Exceptions;
using Scalar.AspNetCore;
using EventFlow.Presentation.Api.Middlewares;
using Serilog;
using EventFlow.Application.Interfaces;
using EventFlow.Presentation.Api.Services;

using var startupLogger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    startupLogger.Information("Starting up");
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddSerilog((services, configuration) => configuration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services), preserveStaticLogger: true);

    builder.Services.AddOpenApi();
    builder.Services.AddControllers();
    builder.Services.AddEventFlowInfrastructure(builder.Configuration);
    builder.Services.AddApplicationServices();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, CurrentUser>();
    var app = builder.Build();
    app.UseMiddleware<GlobalErrorHandler>();
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
        options.GetLevel = (context, _, _) => context.Response.StatusCode >= 500
            ? Serilog.Events.LogEventLevel.Error
            : context.Response.StatusCode >= 400
                ? Serilog.Events.LogEventLevel.Warning
                : Serilog.Events.LogEventLevel.Information;
        options.EnrichDiagnosticContext = (diagnosticContext, context) =>
            diagnosticContext.Set("TraceId", context.TraceIdentifier);
    });
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
// EF Core design-time tools intentionally abort after building the host.
catch (Exception ex) when (ex is not HostAbortedException)
{
    startupLogger.Fatal(ex, "Application start-up failed");
    throw;
}
finally
{
    startupLogger.Information("Shut down complete");
}

// Expose the entry point to the integration test host.
public partial class Program { }


