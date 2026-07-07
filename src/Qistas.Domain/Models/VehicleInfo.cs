namespace Qistas.Domain.Models;

/// <summary>
/// Domain-side (non-wire) representation of a vehicle/truck for an entry-weight call.
/// PlateNumber maps to the wire "VehiclePlateNumber" (mandatory); LicenseId maps to the
/// typo'd wire "VehicleLicenselId" -- the typo stays isolated in the mapper layer.
/// </summary>
public sealed class VehicleInfo
{
    /// <summary>Mandatory in the D365 contract (Balance's TruckNo).