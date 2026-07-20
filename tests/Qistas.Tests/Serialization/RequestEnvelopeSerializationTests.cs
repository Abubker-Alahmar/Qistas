using System.Text.Json;
using Qistas.Domain.Contracts;
using Qistas.Domain.Json;
using Xunit;

namespace Qistas.Tests.Serialization;

/// <summary>
/// Protects the D365 wire contract: the "_request" envelope wrapper, the exact (and
/// intentionally mis-spelled) field names, and 2-fraction-digit numeric weights. See
/// AGENT_INSTRUCTION.md section 2 -- these are NOT bugs to "fix".
/// </summary>
public class RequestEnvelopeSerializationTests
{
    [Fact]
    public void EntryWeightRequest_SerializesWithRequestWrapperAndExactFieldNames()
    {
        var request = new SetEntryWeightDetailsRequest
        {
            CompanyId = "Bell",
            Userid = "operator1",
            LoadId = "LOAD-1",
            EntryWeight = 2355.5m,
            DriverDetails = new DriverDetails
            {
                DriverNationalId = "119xxxxxxxx",
                DriverName = "Mohamed Ali",
                IsInternal = false,
                DriverLicenseId = "DL-1",
                DriverLicenseExpiryDate = "2029-03-26",
            },
            VehicleDetails = new VehicleDetails
            {
                VehiclePlateNumber = "12345",
                VehicleNetWeight = 2355.5m,
                IsInternal = false,
                VehicleLicenselId = "VL-1",
                VehicleLicenseExpiryDate = "2028-01-01",
                VehicleType = "Truck",
            },
        };

        var envelope = new D365RequestEnvelope<SetEntryWeightDetailsRequest> { Request = request };
        string json = JsonSerializer.Serialize(envelope, QistasJson.Options);

        // The wrapper is literally "_request" -- not "setEntryWeightDetails_request" or "EntryRequest".
        Assert.StartsWith("{\"_request\":{", json);

        // Contract quirk: lower-case "id" in "Userid" (request side only).
        Assert.Contains("\"Userid\":\"operator1\"", json);
        Assert.DoesNotContain("\"UserId\":", json);

        // Contract quirk: "VehicleLicenselId" (extra "l"), not "VehicleLicenseId".
        Assert.Contains("\"VehicleLicenselId\":\"VL-1\"", json);
        Assert.DoesNotContain("\"VehicleLicenseId\":", json);

        // Weights: numeric (never quoted), exactly 2 fraction digits.
        Assert.Contains("\"EntryWeight\":2355.50", json);
        Assert.DoesNotContain("\"EntryWeight\":\"2355.50\"", json);
        Assert.Contains("\"VehicleNetWeight\":2355.50", json);

        Assert.Contains("\"CompanyId\":\"Bell\"", json);
        Assert.Contains("\"LoadId\":\"LOAD-1\"", json);
    }

    [Fact]
    public void ExitWeightRequest_SerializesTelorenceFieldNameAndScaleSystemReferenceId()
    {
        var request = new SetExitWeightDetailsRequest
        {
            CompanyId = "Bell",
            Userid = "operator2",
            LoadId = "LOAD-2",
            ScaleSystemReferenceId = "11111111-2222-3333-4444-555555555555",
            EntryWeight = 12000m,
            ExitWeight = 25000m,
            TotalNetWeight = 13000m,
            TotalGrossWeight = 25000m,
            Telorence = 5m,
        };

        var envelope = new D365RequestEnvelope<SetExitWeightDetailsRequest> { Request = request };
        string json = JsonSerializer.Serialize(envelope, QistasJson.Options);

        Assert.StartsWith("{\"_request\":{", json);

        // Contract quirk: "Telorence", never "Tolerance".
        Assert.Contains("\"Telorence\":5.00", json);
        Assert.DoesNotContain("Tolerance", json);

        Assert.Contains("\"Userid\":\"operator2\"", json);
        Assert.Contains("\"ScaleSystemReferenceId\":\"11111111-2222-3333-4444-555555555555\"", json);

        // Weights: numeric, exactly 2 fraction digits -- including whole numbers like 12000.
        Assert.Contains("\"EntryWeight\":12000.00", json);
        Assert.Contains("\"ExitWeight\":25000.00", json);
        Assert.Contains("\"TotalNetWeight\":13000.00", json);
        Assert.Contains("\"TotalGrossWeight\":25000.00", json);
    }

    [Fact]
    public void GetLoadDetailsRequest_SerializesLowerCaseUseridOnly()
    {
        var request = new GetLoadDetailsRequest
        {
            CompanyId = "Bell",
            Userid = "operator3",
            LoadId = "LOAD-3",
        };

        var envelope = new D365RequestEnvelope<GetLoadDetailsRequest> { Request = request };
        string json = JsonSerializer.Serialize(envelope, QistasJson.Options);

        Assert.StartsWith("{\"_request\":{", json);
        Assert.Contains("\"Userid\":\"operator3\"", json);
        Assert.DoesNotContain("\"UserId\":", json);
    }

    [Theory]
    [InlineData("1500")]
    public void Weights_AlwaysSerializeWithExactlyTwoFractionDigits(string wholeNumberInput)
    {
        decimal weight = decimal.Parse(wholeNumberInput);
        var request = new SetExitWeightDetailsRequest
        {
            CompanyId = "Bell",
            Userid = "op",
            LoadId = "L",
            ScaleSystemReferenceId = "ref",
            EntryWeight = weight,
            ExitWeight = weight,
            TotalNetWeight = weight,
            TotalGrossWeight = weight,
            Telorence = weight,
        };

        string json = JsonSerializer.Serialize(request, QistasJson.Options);

        Assert.Contains("\"EntryWeight\":1500.00", json);
        Assert.DoesNotContain("\"EntryWeight\":1500}", json);
        Assert.DoesNotContain("\"EntryWeight\":1500,", json);
    }

    [Fact]
    public void Weight_WithMoreThanTwoDecimals_RoundsToTwoFractionDigits()
    {
        var request = new SetEntryWeightDetailsRequest
        {
            CompanyId = "Bell",
            Userid = "op",
            LoadId = "L",
            EntryWeight = 2355.545m, // rounds away-from-zero to 2355.55 (not truncated to 2355.54)
        };

        string json = JsonSerializer.Serialize(request, QistasJson.Options);

        Assert.Contains("\"EntryWeight\":2355.55", json);
    }
}
