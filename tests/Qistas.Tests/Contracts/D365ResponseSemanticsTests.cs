using Qistas.Domain.Contracts;
using Xunit;

namespace Qistas.Tests.Contracts;

public class D365ResponseSemanticsTests
{
    [Theory]
    [InlineData("Bell", "BELL", true)]
    [InlineData("Bell", "bell", true)]
    [InlineData("BELL", "Bell", true)]
    [InlineData(" Bell ", "BELL", true)]
    [InlineData("Bell", "OtherCo", false)]
    [InlineData(null, "BELL", false)]
    [InlineData("Bell", null, false)]
    public void CompanyIdMatches_IsCaseInsensitiveAndTrims(string? sent, string? received, bool expected)
    {
        Assert.Equal(expected, D365ResponseSemantics.CompanyIdMatches(sent, received));
    }

    [Theory]
    [InlineData("This load was already processed", true)]
    [InlineData("Duplicate submission detected", true)]
    [InlineData("Record already exists", true)]
    [InlineData("already submitted for this load", true)]
    [InlineData("already sent to warehouse", true)]
    [InlineData("ALREADY PROCESSED", true)]
    [InlineData("Load ID not found", false)]
    [InlineData("Invalid CompanyId", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IndicatesAlreadyProcessed_MatchesKnownPhrasing(string? message, bool expected)
    {
        Assert.Equal(expected, D365ResponseSemantics.IndicatesAlreadyProcessed(message));
    }
}
