namespace Qistas.Domain.Enums;

/// <summary>
/// Vehicle category as sent/received on the D365 "VehicleType" wire field. Values inferred
/// from the field comment on <see cref="Qistas.Domain.Contracts.VehicleDetails"/>
/// ("Optional -- one of Truck / Van / Pickup") and the serialization tests that exercise
/// "Truck" as a valid literal. Serialized as its member name (string) via
/// JsonStringEnumConverter so the D365 wire contract (a plain string field) is unaffected --
/// the wire DTOs (VehicleDetails/VehicleDetailsResponse) intentionally stay string-typed.
/// </summary>
public enum VehicleType
{
    Truck = 0,
    Van = 1,
    Pickup = 2,
}
