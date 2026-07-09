using System.Text.Json;
using Qistas.Domain.Contracts;
using Qistas.Domain.Json;
using Xunit;

namespace Qistas.Tests.Serialization;

/// <summary>
/// Verifies deserialization of a realistic D365 common response: "$id" DataContract
/// metadata is ignored, "Vehichle*" header typos are read correctly, sentinel and
/// non-padded dates parse leniently, and the response-side "UserId" (capital I) casing is
/// distinct from the request-side "Userid" (AGENT_INSTRUCTION.md section 2).
/// </summary>
public class ResponseDeserializationTests
{
    private const string RealisticResponseJson = """
        {
          "$id": "1",
          "UnknownTopLevelField": "should be ignored, not throw",
          "Context": {
            "$id": "2",
            "$type": "SomeDataContractTypeHint",
            "LoadHeader": {
              "$id": "3",
              "SalesId": "SO-1001",
              "LoadId": "LOAD-1",
              "CustAccount": "CUST-1",
              "CustName": "Acme Trading",
              "WarehouseId": "WH-1",
              "WarehouseName": "Main WH",
              "TotalNetWeight": 1500.00,
              "TotalGrossWeight": 1550.00,
              "VehichleNetWeight": 1500.00,
              "VehichleGrossWeight": 1550.00,
              "Note": "test note"
            },
            "LoadLines": [
              {
                "$id": "4",
                "ItemId": "260000018",
                "ItemName": "Cement",
                "ItemDescription": "Bag Cement 50kg",
                "UnitId": "KG",
                "Qty": 500.00,
                "ItemNetWeight": 500.00,
                "ItemGrossWeight": 520.00,
                "BatchNumber": "B-1",
                "BatchExpirationDate": "1900-01-01T12:00:00",
                "LocationId": "LOC-1",
                "LocationName": "Zone A"
              },
              {
                "$id": "5",
                "ItemId": "260000019",
                "ItemName": "Sand",
                "ItemDescription": "Bulk Sand",
                "UnitId": "KG",
                "Qty": 1000.00,
                "ItemNetWeight": 1000.00,
                "ItemGrossWeight": 1030.00,
                "BatchNumber": "B-2",
                "BatchExpirationDate": "2029-3-26",
                "LocationId": "LOC-2",
                "LocationName": "Zone B"
              }
            ],
            "DriverDetails": {
              "$id": "6",
              "DriverNationalId": "119xxxxxxxx",
              "DriverName": "Mohamed Ali",
              "DriverInternalId": null,
              "IsInternal": false,
              "DriverLicenseId": "DL-1",
              "DriverLicenseExpiryDate": "2029-03-26T12:00:00",
              "DriverPhone": "0910000000",
              "CompanyId": "BELL",
              "UserId": "operator1"
            },
            "VehicleDetails": {
              "$id": "7",
              "VehiclePlateNumber": "12345",
              "VehicleNetWeight": 12000.00,
              "IsInternal": false,
              "VehicleLicenselId": "VL-1",
              "VehicleLicenseExpiryDate": "2028-01-01T12:00:00",
              "VehicleNote": null,
              "VehicleType": "Truck",
              "CompanyId": "BELL",
              "UserId": "operator1"
            }
          },
          "CompanyId": "BELL",
          "Status": true,
          "Message": "OK"
        }
        """;

    [Fact]
    public void Deserialize_RealisticResponse_DoesNotThrowOnUnknownDollarIdMembers()
    {
        var response = JsonSerializer.Deserialize<D365Response>(RealisticResponseJson, QistasJson.Options);

        Assert.NotNull(response);
        Assert.True(response!.Status);
        Assert.Equal("OK", response.Message);
    }

    [Fact]
    public void Deserialize_CompanyId_ComesBackUppercase_MatchesCaseInsensitively()
    {
        var response = JsonSerializer.Deserialize<D365Response>(RealisticResponseJson, QistasJson.Options)!;

        Assert.Equal("BELL", response.CompanyId);
        Assert.True(D365ResponseSemantics.CompanyIdMatches("Bell", response.CompanyId));
    }

    [Fact]
    public void Deserialize_LoadHeader_ReadsVehichleTypoFieldsAndDollarId()
    {
        var response = JsonSerializer.Deserialize<D365Response>(RealisticResponseJson, QistasJson.Options)!;
        var header = response.Context!.LoadHeader!;

        Assert.Equal("1", response.DollarId);
        Assert.Equal("LOAD-1", header.LoadId);
        Assert.Equal("SO-1001", header.SalesId);
        Assert.Equal(1500.00m, header.VehichleNetWeight);
        Assert.Equal(1550.00m, header.VehichleGrossWeight);
    }

    [Fact]
    public void Deserialize_LoadLine_SentinelBatchExpirationDate_BecomesNull()
    {
        var response = JsonSerializer.Deserialize<D365Response>(RealisticResponseJson, QistasJson.Options)!;
        var firstLine = response.Context!.LoadLines[0];

        Assert.Equal("260000018", firstLine.ItemId);
        Assert.Null(firstLine.BatchExpirationDate);
    }

    [Fact]
    public void Deserialize_LoadLine_NonPaddedDate_ParsesLeniently()
    {
        var response = JsonSerializer.Deserialize<D365Response>(RealisticResponseJson, QistasJson.Options)!;
        var secondLine = response.Context!.LoadLines[1];

        Assert.Equal(new DateTime(2029, 3, 26), secondLine.BatchExpirationDate);
    }

    [Fact]
    public void Deserialize_DriverDetailsResponse_HasCapitalIUserIdField()
    {
        var response = JsonSerializer.Deserialize<D365Response>(RealisticResponseJson, QistasJson.Options)!;
        var driver = response.Context!.DriverDetails!;

        Assert.Equal("operator1", driver.UserId);
        Assert.Equal("BELL", driver.CompanyId);
        Assert.Equal("DL-1", driver.DriverLicenseId);
    }

    [Fact]
    public void Deserialize_VehicleDetailsResponse_KeepsVehicleLicenselIdTypo()
    {
        var response = JsonSerializer.Deserialize<D365Response>(RealisticResponseJson, QistasJson.Options)!;
        var vehicle = response.Context!.VehicleDetails!;

        Assert.Equal("VL-1", vehicle.VehicleLicenselId);
        Assert.Equal("operator1", vehicle.UserId);
        Assert.Equal(12000.00m, vehicle.VehicleNetWeight);
    }

    [Fact]
    public void Deserialize_LoadLines_WeightsAndQuantitiesReadAsDecimal()
    {
        var response = JsonSerializer.Deserialize<D365Response>(RealisticResponseJson, QistasJson.Options)!;
        var lines = response.Context!.LoadLines;

        Assert.Equal(500.00m, lines[0].Qty);
        Assert.Equal(500.00m, lines[0].ItemNetWeight);
        Assert.Equal(520.00m, lines[0].ItemGrossWeight);
        Assert.Equal(1000.00m, lines[1].Qty);
    }
}
