namespace Qistas.Domain.Models;

/// <summary>
/// Domain-side (non-wire) representation of a vehicle/truck for an entry-weight call.
/// PlateNumber maps to the wire "VehiclePlateNumber" (mandatory); LicenseId maps to the
/// typo'd wire "VehicleLicenselId" -- the typo stays isolated in the mapper layer.
/// </summary>
public sealed class VehicleInfo
{
    /// <summary>Mandatory in the D365 contract (Balance's TruckNo).</summary>
    public string PlateNumber { get; set; } = string.Empty;

    public string LicenseId { get; set; } = string.Empty;
    public DateOnly LicenseExpiryDate { get; set; }

    /// <summary>Optional -- one of Truck / Van / Pickup.</summary>
    public string? VehicleType { get; set; }

    /// <summary>Optional -- Alsahl Group vehicle.</summary>
    public bool IsInternal { get; set; }

    /// <summary>Optional free text (e.g. trailer number).</summary>
    public string? Note { get; set; }
}
