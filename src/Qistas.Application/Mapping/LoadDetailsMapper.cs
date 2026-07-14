using Qistas.Domain.Contracts;
using Qistas.Domain.Json;
using Qistas.Domain.Models;
using Qistas.Domain.Validation;

namespace Qistas.Application.Mapping;

/// <summary>
/// Maps the common D365 response (Context.LoadHeader / Context.LoadLines, with the
/// Vehichle* typo'd header fields) into the clean domain <see cref="LoadValidationResult"/>,
/// normalizing every weight to KG (Balance/CLAUDE.md #16.11). Also surfaces
/// Context.DriverDetails / Context.VehicleDetails (as <see cref="LoadValidationResult.Driver"/>
/// / <see cref="LoadValidationResult.Vehicle"/>, for Balance to sync local driver/vehicle
/// master data with D365's canonical values) and each line's LocationId/LocationName (for
/// auto-filling the materials grid's warehouse location). All additive -- no change to the
/// existing header/line/weight mapping behavior.
/// </summary>
public static class LoadDetailsMapper
{
    public static LoadValidationResult ToDomainResult(D365Response? response, string? transportError)
    {
        if (response is null)
        {
            return new LoadValidationResult { Success = false, Message = transportError ?? "No response from D365." };
        }

        var header = response.Context?.LoadHeader;
        if (!response.Status || header is null)
        {
            return new LoadValidationResult { Success = false, Message = response.Message ?? "Load not found." };
        }

        var lines = (response.Context?.LoadLines ?? new List<LoadLine>()).Select(line => new LoadLineInfo
        {
            ItemId = line.ItemId,
            ItemName = line.ItemName,
            ItemDescription = line.ItemDescription,
            BatchNumber = line.BatchNumber,
            QuantityKg = WeightUnitConverter.ToKg(line.Qty, line.UnitId),
            NetWeightKg = WeightUnitConverter.ToKg(line.ItemNetWeight, line.UnitId),
            GrossWeightKg = WeightUnitConverter.ToKg(line.ItemGrossWeight, line.UnitId),
            LocationId = line.LocationId,
            LocationName = line.LocationName,
        }).ToList();

        var driverDetails = response.Context?.DriverDetails;
        var driver = driverDetails is null
            ? null
            : new DriverDetailsResult
            {
                DriverNationalId = driverDetails.DriverNationalId,
                DriverName = driverDetails.DriverName,
                DriverInternalId = driverDetails.DriverInternalId,
                IsInternal = driverDetails.IsInternal,
                DriverLicenseId = driverDetails.DriverLicenseId,
                DriverLicenseExpiryDate = LenientNullableDateTimeConverter.TryParseLenientDate(driverDetails.DriverLicenseExpiryDate),
                DriverPhone = driverDetails.DriverPhone,
            };

        var vehicleDetails = response.Context?.VehicleDetails;
        var vehicle = vehicleDetails is null
            ? null
            : new VehicleDetailsResult
            {
                VehiclePlateNumber = vehicleDetails.VehiclePlateNumber,
                // Wire field is typo'd "VehicleLicenselId" (extra "l"); clean name on the domain side.
                VehicleLicenseId = vehicleDetails.VehicleLicenselId,
                VehicleLicenseExpiryDate = LenientNullableDateTimeConverter.TryParseLenientDate(vehicleDetails.VehicleLicenseExpiryDate),
                VehicleType = vehicleDetails.VehicleType,
                IsInternal = vehicleDetails.IsInternal,
                VehicleNote = vehicleDetails.VehicleNote,
            };

        return new LoadValidationResult
        {
            Success = true,
            Message = response.Message,
            LoadId = header.LoadId,
            // CompanyId lives at the top level of the common response (may differ in
            // case from the request, e.g. "BELL" -- compare case-insensitively).
            CompanyId = response.CompanyId,
            SalesId = header.SalesId,
            CustAccount = header.CustAccount,
            CustName = header.CustName,
            HeaderNetWeightKg = header.TotalNetWeight ?? header.VehichleNetWeight,
            HeaderGrossWeightKg = header.TotalGrossWeight ?? header.VehichleGrossWeight,
            Lines = lines,
            Driver = driver,
            Vehicle = vehicle,
        };
    }
}
