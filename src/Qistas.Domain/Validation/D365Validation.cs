using Qistas.Domain.Models;

namespace Qistas.Domain.Validation;

/// <summary>
/// Business-rule validation performed locally in Qistas (and mirrored/displayed by
/// Balance) before calling D365. Covers: license expiry, weight sanity, and the
/// tolerance check between total gross weight and the sum of load line weights
/// (Balance/CLAUDE.md #14 tolerance rule, #16.8 tolerance breach, #16.14 license expiry,
/// #16.16 weight sanity).
/// </summary>
public static class D365Validation
{
    /// <summary>
    /// License (driver or vehicle) must expire strictly after "asOf" (normally UtcNow).
    /// A DateOnly.MinValue or default value is treated as "missing" and fails.
    /// </summary>
    public static ValidationResult ValidateLicenseExpiry(DateOnly expiryDate, DateOnly asOf, string licenseKind)
    {
        if (expiryDate == default)
        {
            return ValidationResult.Fail($"{licenseKind} expiry date is missing.");
        }

        return expiryDate > asOf
            ? ValidationResult.Success()
            : ValidationResult.Fail($"{licenseKind} has expired (expiry {expiryDate:yyyy-MM-dd}).");
    }

    /// <summary>
    /// Entry weight sanity: must be positive and finite. For the exit call, exit weight
    /// (loaded) must be strictly greater than entry weight (empty) on a Sales Order
    /// load -- guards against swapped in/out readings (Balance/CLAUDE.md #16.16).
    /// </summary>
    public static ValidationResult ValidateWeightSanity(decimal weightKg, string label)
    {
        if (weightKg <= 0)
        {
            return ValidationResult.Fail($"{label} must be a positive weight (got {weightKg}).");
        }

        return ValidationResult.Success();
    }

    public static ValidationResult ValidateLoadedExitGreaterThanEntry(decimal entryWeightKg, decimal exitWeightKg)
    {
        var entrySanity = ValidateWeightSanity(entryWeightKg, "Entry weight");
        var exitSanity = ValidateWeightSanity(exitWeightKg, "Exit weight");
        if (!entrySanity.IsValid || !exitSanity.IsValid)
        {
            return ValidationResult.Combine(entrySanity, exitSanity);
        }

        return exitWeightKg > entryWeightKg
            ? ValidationResult.Success()
            : ValidationResult.Fail(
                $"Exit weight ({exitWeightKg} kg) must be greater than entry weight ({entryWeightKg} kg) for a loaded truck -- possible swapped readings.");
    }

    /// <summary>
    /// Tolerance check: |TotalGrossWeight - sum(load line weights)| must be within the
    /// configured Telorence. Breach means the driver returns to the Warehouse Clerk per
    /// the TDD (Balance/CLAUDE.md #16.8); Qistas surfaces the breach, it does not block
    /// the call itself (validation happens on the Scale/Balance side per the meeting).
    /// </summary>
    public static ValidationResult ValidateTolerance(
        decimal totalGrossWeightKg,
        decimal sumOfLoadLineWeightsKg,
        decimal toleranceKg)
    {
        decimal difference = Math.Abs(totalGrossWeightKg - sumOfLoadLineWeightsKg);
        return difference <= toleranceKg
            ? ValidationResult.Success()
            : ValidationResult.Fail(
                $"Tolerance breach: |{totalGrossWeightKg} - {sumOfLoadLineWeightsKg}| = {difference} kg exceeds Telorence {toleranceKg} kg.");
    }

    public static ValidationResult ValidateEntrySubmission(EntryWeightSubmission submission, DateOnly asOf)
    {
        return ValidationResult.Combine(
            ValidateWeightSanity(submission.EntryWeightKg, "Entry weight"),
            ValidateLicenseExpiry(submission.Driver.LicenseExpiryDate, asOf, "Driver license"),
            ValidateLicenseExpiry(submission.Vehicle.LicenseExpiryDate, asOf, "Vehicle license"));
    }

    /// <summary>
    /// Exit-submission check performed by Qistas itself: weight sanity only. Tolerance
    /// (|TotalGrossWeight - sum(load line weights)|) is validated by Balance using the
    /// freshly fetched load from call point 2 (GET /api/scale/loads/{loadId}) before this
    /// call is ever made -- this endpoint does not re-fetch the load, so it has no line
    /// weights to compare against and does not repeat that check.
    /// </summary>
    public static ValidationResult ValidateExitSubmission(ExitWeightSubmission submission)
    {
        return ValidateLoadedExitGreaterThanEntry(submission.EntryWeightKg, submission.ExitWeightKg);
    }
}
