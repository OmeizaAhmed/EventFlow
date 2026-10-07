using EventFlow.Domain.Exceptions;

namespace EventFlow.Presentation.Api.Middlewares
{
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
                            context.Response.StatusCode = (int)exception.StatusCode;
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
    
}
