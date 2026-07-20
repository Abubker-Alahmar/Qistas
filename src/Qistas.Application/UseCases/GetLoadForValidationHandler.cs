using Qistas.Application.Abstractions;
using Qistas.Application.Mapping;
using Qistas.Domain.Contracts;
using Qistas.Domain.Models;

namespace Qistas.Application.UseCases;

/// <summary>
/// Use case for the Weight-Out screen open: ALWAYS fetches a fresh getLoadDetails
/// immediately before validation -- items/qty may have changed in D365 during loading,
/// so the entry-time snapshot must never be reused (Balance/CLAUDE.md #7 / #16.7).
/// </summary>
public sealed class GetLoadForValidationHandler(ID365Client client, IActiveEnvironmentProvider environmentProvider)
{
    public async Task<LoadValidationResult> HandleAsync(string loadId, string userId, CancellationToken cancellationToken)
    {
        /*var result = new LoadValidationResult
        {
            Success = true,
            CompanyId = "FRDS",
            LoadId = loadId,
            CustAccount = "IC-000001",
            CustName = "Abubker",
            SalesId = "SO-000086",
            Message = "Success",
            HeaderGrossWeightKg = 1920,
            HeaderNetWeightKg = 920,
            Driver = new DriverDetailsResult
            {
                DriverInternalId = "656500",
                DriverLicenseExpiryDate = new DateOnly(2026, 12, 9),
                DriverLicenseId = "256888",
                DriverName = "عبدالحميد محمد القذافى",
                DriverNationalId = "119950000000",
                DriverPhone = "0926296964"
            },
            Vehicle = new VehicleDetailsResult
            {
                IsInternal = true,
                VehicleLicenseExpiryDate = new DateOnly(2026, 12, 9),
                VehicleLicenseId = "256888",
                VehiclePlateNumber = "123456789",
                VehicleType = "Truck"
            },
            Lines =
            [
                new LoadLineInfo
            {
                ItemId = "11015010422",
                BatchNumber = "260617-BO-00000025",
                ItemDescription = "مكرونة بريما 400 غرام",
                QuantityKg = 10,
                ItemName = "مكرونة بريما",
                LocationId = "1",
                LocationName = "طرابلس",
                UnitId = "Carton",
                GrossWeightKg = new decimal(9.6),
                NetWeightKg = new decimal(0.400)
            },
                new LoadLineInfo
                {
                    ItemId = "11015010422",
                    BatchNumber = "260617-BO-00000025",
                    ItemDescription = "كسكسي بريما 900 غرام",
                    QuantityKg = 10,
                    ItemName = "كسكسي بريما",
                    LocationId = "1",
                    LocationName = "طرابلس",
                    UnitId = "Pack",
                    GrossWeightKg = new decimal(9.6),
                    NetWeightKg = new decimal(0.900)
                },
                new LoadLineInfo
                {
                    ItemId = "11015010422",
                    BatchNumber = "260617-BO-00000025",
                    ItemDescription = "أرز المبروك 900 غرام",
                    QuantityKg = 10,
                    ItemName = "أرز المبروك",
                    LocationId = "1",
                    LocationName = "طرابلس",
                    UnitId = "KG",
                    GrossWeightKg = new decimal(9.6),
                    NetWeightKg = new decimal(0.900)
                }
            ]
        };
        return result;*/
        var environment = environmentProvider.GetActiveEnvironment();
        var settings = environmentProvider.GetSettings(environment);
        
        var request = new GetLoadDetailsRequest
        {
            CompanyId = settings.CompanyId,
            Userid = userId,
            LoadId = loadId,
        };
        var callResult = await client.GetLoadDetailsAsync(request, environment, cancellationToken);
        
        if (!callResult.TransportSucceeded)
        {
            return new LoadValidationResult
            {
                Success = false,
                Message = $"Could not reach D365 ({callResult.TransportError}).",
            };
        }
        
        var result = LoadDetailsMapper.ToDomainResult(callResult.Response, callResult.TransportError);
        
        if (result.Success && !string.IsNullOrWhiteSpace(callResult.Response?.CompanyId))
        {
            // CompanyId may be echoed back in a different case ("BELL" vs "Bell") -- compare
            // case-insensitively rather than treating a casing difference as a mismatch.
            bool companyMatches = D365ResponseSemantics.CompanyIdMatches(settings.CompanyId, callResult.Response.CompanyId);
            if (!companyMatches)
            {
                return new LoadValidationResult
                {
                    Success = false,
                    Message = $"Load belongs to a different company ({callResult.Response.CompanyId}).",
                };
            }
        }
        
        return result;
    }
}
