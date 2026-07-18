namespace Qistas.Api.Validators;

/// <summary>
/// Thrown by <see cref="ValidationFilter{T}"/> when a request DTO fails
/// <see cref="System.ComponentModel.DataAnnotations"/> validation. Caught by
/// <c>Qistas.Api.Middlewares.ExceptionMiddleware</c> and turned into a 400 response with
/// a property/message list.
///
/// Implemented with the built-in <see cref="System.ComponentModel.DataAnnotations"/>
/// validator rather than a FluentValidation NuGet package: this sandbox's outbound network
/// only allows Ubuntu package mirrors and github.com, so `dotnet restore` cannot reach
/// nuget.org to add a new package. DataAnnotations ships in the BCL, so it needs no
/// restore. There is no MediatR pipeline in this project, so validation is wired
/// per-endpoint via <c>AddEndpointFilter&lt;ValidationFilter&lt;T&gt;&gt;()</c>.
/// </summary>
public sealed class AppValidationException : Exception
{
    public IReadOnlyList<AppValidationError> Errors { get; }

    public AppValidationException(IReadOnlyList<AppValidationError> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }
}

public sealed record AppValidationError(string? PropertyName, string ErrorMessage);
