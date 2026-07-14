using System.Globalization;
using Qistas.Domain.Contracts;
using Qistas.Domain.Models;

namespace Qistas.Application.Mapping;

/// <summary>
/// Explicit, hand-written mapping between the domain <see cref="EntryWeightSubmission"/>
/// and the D365 wire contract. No AutoMapper conventions near contract DTOs
/// (AGENT_INSTRUCTION.md section 8) -- convention mapping would silently "fix" the
/// intentional typos (Userid/VehicleLicenselId).
/// </summary>
public static class EntryWeightMapper
{
    public static SetEntryWeightDetailsRequest ToContract(EntryWeightSubmission submission)
    {
        return new SetEntryWeightDetailsRequest
        {
            CompanyId = submission.CompanyId,
            Userid = submission.UserId,
            LoadId = submission.LoadId,
            EntryWeight = submission.EntryWeightKg,
            DriverDetails = new DriverDetails
            {
                DriverNationalId = submission.Driver.NationalId,
                DriverName = submission.Driver.DriverName,
                DriverInternalId = submission.Driver.InternalId,
                IsInternal = submission.Driver.IsInternal,
                DriverLicenseId = submission.Driver.LicenseId,
                DriverLicenseExpiryDate = submission.Driver.LicenseExpiryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            },
            VehicleDetails = new VehicleDetails
            {
                VehiclePlateNumber = submission.Vehicle.PlateNumber,
                // Per APIs V2.0, VehicleNetWeight on the entry call IS the 1st weight.
                VehicleNetWeight = submission.EntryWeightKg,
                IsInternal = submission.Vehicle.IsInternal,
                VehicleLicenselId = submission.Vehicle.LicenseId,
                VehicleLicenseExpiryDate = submission.Vehicle.LicenseExpiryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                VehicleNote = submission.Vehicle.Note,
                VehicleType = submission.Vehicle.VehicleType?.ToString(),
            },
        };
    }

    public static D365OperationResult ToDomainResult(D365Response? response, string? rawJson, string? transportError)
    {
        if (response is null)
        {
            return D365OperationResult.Fail(transportError ?? "No response from D365.", rawJson);
        }

        bool alreadyProcessed = !response.Status && D365ResponseSemantics.IndicatesAlreadyProcessed(response.Message);

        return response.Status || alreadyProcessed
            ? D365OperationResult.Ok(response.Message, rawJson, alreadyProcessed)
            : D365OperationResult.Fail(response.Message, rawJson);
    }
}
