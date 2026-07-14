namespace Qistas.Domain.Models;

/// <summary>
/// Domain-side view of a getLoadDetails response, used by Balance to display lines and
/// perform the local tolerance validation before Weight-Out save (Balance/CLAUDE.md
/// section 14, call point 2). Always fetched fresh immediately before validation -- never
/// cached across the loading window (Balance/CLAUDE.md #16.7).
/// </summary>
public sealed class LoadValidationResult
{
    public required bool Success { get; init; }
    public string? Message { get; init; }
    public string? LoadId { get; init; }
    public string? CompanyId { get; init; }
    public string? SalesId { get; init; }
    public string? CustAccount { get; init; }
    public string? CustName { get; init; }
    public decimal? HeaderNetWeightKg { get; init; }
    public decimal? HeaderGrossWeightKg { get; init; }
    public IReadOnlyList<LoadLineInfo> Lines { get; init; } = Array.Empty<LoadLineInfo>();

    /// <summary>D365's canonical driver master data for this load, from Context.DriverDetails.
    /// Null when D365 did not echo driver details. Used by Balance to sync local driver master
    /// data with D365's values -- does not affect existing validation behavior.</summary>
    public DriverDetailsResult? Driver { get; init; }

    /// <summary>D365's canonical vehicle master data for this load, from Context.VehicleDetails.
    /// Null when D365 did not echo vehicle details. Used by Balance to sync local vehicle master
    /// data with D365's values -- does not affect existing validation behavior.</summary>
    public VehicleDetailsResult? Vehicle { get; init; }

    public decimal TotalLineNetWeightKg => Lines.Sum(l => l.NetWeightKg);
    public decimal TotalLineGrossWeightKg => Lines.Sum(l => l.GrossWeightKg);
}

public sealed class LoadLineInfo
{
    public required string ItemId { get; init; }
    public string? ItemName { get; init; }
    public string? ItemDescription { get; init; }
    public string? BatchNumber { get; init; }
    public decimal QuantityKg { get; init; }
    public decimal NetWeightKg { get; init; }
    public decimal GrossWeightKg { get; init; }

    /// <summary>D365 warehouse location id for this line (Context.LoadLines[].LocationId).</summary>
    public string? LocationId { get; init; }

    /// <summary>D365 warehouse location name for this line (Context.LoadLines[].LocationName).</summary>
    public string? LocationName { get; init; }
}

/// <summary>
/// Clean, read-only view of D365's echoed-back driver master data (Context.DriverDetails on a
/// getLoadDetails response). Distinct from <see cref="DriverInfo"/>, which is the INPUT shape
/// Balance sends to D365 on entry-weight -- this is the OUTPUT shape D365 returns, used by
/// Balance to sync its local driver master data with D365's canonical values.
/// </summary>
public sealed class DriverDetailsResult
{
    public string? DriverNationalId { get; init; }
    public string? DriverName { get; init; }
    public string? DriverInternalId { get; init; }
    public bool IsInternal { get; init; }
    public string? DriverLicenseId { get; init; }
    public DateOnly? DriverLicenseExpiryDate { get; init; }
    public string? DriverPhone { get; init; }
}

/// <summary>
/// Clean, read-only view of D365's echoed-back vehicle master data (Context.VehicleDetails on a
/// getLoadDetails response). Distinct from <see cref="VehicleInfo"/>, which is the INPUT shape
/// Balance sends to D365 on entry-weight -- this is the OUTPUT shape D365 returns, used by
/// Balance to sync its local vehicle master data with D365's canonical values. Note:
/// <see cref="VehicleLicenseId"/> is spelled correctly here even though the wire contract
/// (<c>VehicleDetailsResponse.VehicleLicenselId</c>) has an extra "l" -- the typo is confined
/// to the wire layer.
/// </summary>
public sealed class VehicleDetailsResult
{
    public string? VehiclePlateNumber { get; init; }
    public string? VehicleLicenseId { get; init; }
    public DateOnly? VehicleLicenseExpiryDate { get; init; }
    public string? VehicleType { get; init; }
    public bool IsInternal { get; init; }
    public string? VehicleNote { get; init; }
}
