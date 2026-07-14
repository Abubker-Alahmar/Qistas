using System.Diagnostics;

namespace Qistas.Api.Middlewares;

/// <summary>
/// Logs each request's method + path on the way in, and status code + elapsed duration on
/// the way out. Registered after <see cref="ExceptionMiddleware"/> so a request that
/// throws is still timed and logged. Complements (does not replace)
/// <c>app.UseSerilogRequestLogging()</c>, which stays for its structured request-summary
/// output.
/// </summary>
public sealed class LoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LoggingMiddleware> _logger;

    public LoggingMiddleware(RequestDelegate next, ILogger<LoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        _logger.LogInformation("Incoming request {Method} {Path}", context.Request.Method, context.Request.Path);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogInformation(
                "Completed request {Method} {Path} with status {StatusCode} in {ElapsedMilliseconds}ms",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
    }
}
