using System.ComponentModel.DataAnnotations;

namespace Qistas.Api.Validators;

/// <summary>
/// Minimal-API endpoint filter that runs <see cref="System.ComponentModel.DataAnnotations"/>
/// validation (via <see cref="Validator.TryValidateObject"/>) against the endpoint's bound
/// request DTO before the handler runs. On failure, throws <see cref="AppValidationException"/>
/// so <see cref="Qistas.Api.Middlewares.ExceptionMiddleware"/> can turn it into a consistent
/// 400 response with property/message details.
/// </summary>
public sealed class ValidationFilter<T> : IEndpointFilter
    where T : notnull
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var argument = context.Arguments.OfType<T>().FirstOrDefault();
        if (argument is null)
        {
            return next(context);
        }

        var validationContext = new ValidationContext(argument);
        var results = new List<ValidationResult>();
        bool isValid = Validator.TryValidateObject(argument, validationContext, results, validateAllProperties: true);

        if (!isValid)
        {
            var errors = results
                .Select(r => new AppValidationError(r.MemberNames.FirstOrDefault(), r.ErrorMessage ?? "Invalid value."))
                .ToArray();
            throw new AppValidationException(errors);
        }

        return next(context);
    }
}
