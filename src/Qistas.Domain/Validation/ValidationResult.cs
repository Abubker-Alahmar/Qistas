namespace Qistas.Domain.Validation;

/// <summary>
/// Lightweight validation outcome so business-rule failures can be surfaced to the
/// operator (as an Arabic-facing message from the caller) without throwing exceptions
/// for expected, data-driven rejections.
/// </summary>
public sealed class ValidationResult
{
    public bool IsValid { get; }
    public IReadOnlyList<string> Errors { get; }

    private ValidationResult(bool isValid, IReadOnlyList<string> errors)
    {
        IsValid = isValid;
        Errors = errors;
    }

    public static ValidationResult Success() => new(true, Array.Empty<string>());

    public static ValidationResult Fail(params string[] errors) => new(false, errors);

    public static ValidationResult Combine(params ValidationResult[] results)
    {
        var errors = results.Where(r => !r.IsValid).SelectMany(r => r.Errors).ToArray();
        return errors.Length == 0 ? Success() : Fail(errors);
    }
}
