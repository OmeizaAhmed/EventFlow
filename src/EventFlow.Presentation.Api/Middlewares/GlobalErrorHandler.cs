using EventFlow.Domain.Exceptions;

namespace EventFlow.Presentation.Api.Middlewares
{
    public class GlobalErrorHandler
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalErrorHandler> _logger;

            public GlobalErrorHandler(RequestDelegate next, ILogger<GlobalErrorHandler> logger)
            {
                _next = next;
                _logger = logger;
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
                            _logger.LogWarning("Request rejected with {StatusCode}: {ExceptionType}; trace {TraceId}",
                                (int)exception.StatusCode, exception.GetType().Name, context.TraceIdentifier);
                            context.Response.StatusCode = (int)exception.StatusCode;
                            await context.Response.WriteAsJsonAsync(new { error = exception.Message });
                            break;


                        default:
                            _logger.LogError(ex, "Unhandled request failure; trace {TraceId}", context.TraceIdentifier);
                            if (!context.Response.HasStarted)
                            {
                                context.Response.StatusCode = 500;
                                await context.Response.WriteAsJsonAsync(new { error = "Internal Server Error" });
                            }
                            break;
                    }
                }
            }
        }
    
}
