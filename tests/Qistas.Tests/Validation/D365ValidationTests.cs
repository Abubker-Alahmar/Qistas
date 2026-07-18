using Qistas.Domain.Validation;
using Xunit;

namespace Qistas.Tests.Validation;

public class D365ValidationTests
{
    private static readonly DateOnly Today = new(2026, 7, 7);

    [Fact]
    public void ValidateLicenseExpiry_PastDate_Fails()
    {
        var result = D365Validation.ValidateLicenseExpiry(Today.AddDays(-1), Today, "Driver license");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("expired"));
    }

    [Fact]
    public void ValidateLicenseExpiry_ExpiresToday_Fails()
    {
        // Must expire strictly AFTER asOf -- same-day expiry is not valid.
        var result = D365Validation.ValidateLicenseExpiry(Today, Today, "Driver license");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateLicenseExpiry_FutureDate_Succeeds()
    {
        var result = D365Validation.ValidateLicenseExpiry(Today.AddDays(1), Today, "Driver license");

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateLicenseExpiry_DefaultValue_TreatedAsMissing()
    {
        var result = D365Validation.ValidateLicenseExpiry(default, Today, "Vehicle license");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("missing"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void ValidateWeightSanity_ZeroOrNegative_Fails(decimal weight)
    {
        var result = D365Validation.ValidateWeightSanity(weight, "Entry weight");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateWeightSanity_Positive_Succeeds()
    {
        var result = D365Validation.ValidateWeightSanity(1500.00m, "Entry weight");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateLoadedExitGreaterThanEntry_ExitGreater_Succeeds()
    {
        var result = D365Validation.ValidateLoadedExitGreaterThanEntry(entryWeightKg: 12000m, exitWeightKg: 25000m);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateLoadedExitGreaterThanEntry_ExitNotGreater_Fails()
    {
        // Swapped in/out readings -- exit (should be loaded) is not greater than entry (empty).
        var result = D365Validation.ValidateLoadedExitGreaterThanEntry(entryWeightKg: 25000m, exitWeightKg: 12000m);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("swapped"));
    }

    [Fact]
    public void ValidateLoadedExitGreaterThanEntry_ExitEqualsEntry_Fails()
    {
        var result = D365Validation.ValidateLoadedExitGreaterThanEntry(entryWeightKg: 12000m, exitWeightKg: 12000m);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateLoadedExitGreaterThanEntry_NonPositiveWeights_ReportsSanityErrorsNotSwapError()
    {
        var result = D365Validation.ValidateLoadedExitGreaterThanEntry(entryWeightKg: 0m, exitWeightKg: -1m);

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
    }

    [Fact]
    public void ValidateTolerance_WithinTolerance_Succeeds()
    {
        var result = D365Validation.ValidateTolerance(
            totalGrossWeightKg: 1000.00m,
            sumOfLoadLineWeightsKg: 998.00m,
            toleranceKg: 5.00m);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateTolerance_ExactlyAtTolerance_Succeeds()
    {
        var result = D365Validation.ValidateTolerance(
            totalGrossWeightKg: 1005.00m,
            sumOfLoadLineWeightsKg: 1000.00m,
            toleranceKg: 5.00m);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateTolerance_Breach_Fails()
    {
        var result = D365Validation.ValidateTolerance(
            totalGrossWeightKg: 1050.00m,
            sumOfLoadLineWeightsKg: 1000.00m,
            toleranceKg: 5.00m);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Tolerance breach"));
    }
}
