using System.Net;
using System.Text.Json;
using Qistas.Api.Validators;

namespace Qistas.Api.Middlewares;

/// <summary>
/// Outermost middleware in the pipeline: catches any exception that escapes endpoint
/// handlers, logs it, and returns a consistent JSON error envelope instead of the default
/// ASP.NET Core HTML error page. Registered first in Program.cs so it wraps everything
/// else, including <see cref="LoggingMiddleware"/>.
/// </summary>
public sealed class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
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
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message, details) = exception switch
        {
            AppValidationException validationEx => (
                HttpStatusCode.BadRequest,
                "One or more validation errors occurred.",
                (object?)validationEx.Errors.Select(e => new { property = e.PropertyName, message = e.ErrorMessage })),

            KeyNotFoundException => (
                HttpStatusCode.NotFound,
                exception.Message,
                null),

            ArgumentException => (
                HttpStatusCode.BadRequest,
                exception.Message,
                null),

            _ => (
                HttpStatusCode.InternalServerError,
                "An unexpected error occurred while processing the request.",
                null),
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
        }
        else
        {
            _logger.LogWarning(exception, "Handled exception ({StatusCode}) for {Method} {Path}", (int)statusCode, context.Request.Method, context.Request.Path);
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var payload = new
        {
            statusCode = (int)statusCode,
            message,
            details,
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
