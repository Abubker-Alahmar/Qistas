using System.Text.Json.Serialization;
using Qistas.Domain.Json;

namespace Qistas.Domain.Contracts;

/// <summary>
/// Vehicle details sent inside the setEntryWeightDetails "_request". Contract quirk: the
/// license id field is spelled "VehicleLicenselId" (extra "l") -- NOT "VehicleLicenseId".
/// Preserve exactly (AGENT_INSTRUCTION.md, section 2). Field list matches APIs V2.0.
/// </summary>
public sealed class VehicleDetails
{
    /// <summary>Mandatory.</summary>
    [JsonPropertyName("VehiclePlateNumber")]
    public string VehiclePlateNumber { get; set; } = string.Empty;

    /// <summary>Mandatory -- the 1st (empty/tare) weight, same value as EntryWeight.</summary>
    [JsonPropertyName("VehicleNetWeight")]
    [JsonConverter(typeof(TwoDecimalConverter))]
    public decimal VehicleNetWeight { get; set; }

    /// <summary>Optional -- only when the vehicle is an Alsahl Group vehicle.</summary>
    [JsonPropertyName("IsInternal")]
    public bool IsInternal { get; set; }

    /// <summary>Contract quirk: "VehicleLicenselId" (extra "l"). Do not rename.</summary>
    [JsonPropertyName("VehicleLicenselId")]
    public string VehicleLicenselId { get; set; } = string.Empty;

    /// <summary>Mandatory. Sent as ISO "yyyy-MM-dd".</summary>
    [JsonPropertyName("VehicleLicenseExpiryDate")]
    public string VehicleLicenseExpiryDate { get; set; } = string.Empty;

    /// <summary>Optional.</summary>
    [JsonPropertyName("VehicleNote")]
    public string? VehicleNote { get; set; }

    /// <summary>Optional -- one of Truck / Van / Pickup.</summary>
    [JsonPropertyName("VehicleType")]
    public string? VehicleType { get; set; }
}

/// <summary>
/// Vehicle details as echoed back inside a response Context (adds "$id", "CompanyId",
/// "UserId" -- capital I, response-only).
/// </summary>
public sealed class VehicleDetailsResponse
{
    [JsonPropertyName("$id")]
    public string? DollarId { get; set; }

    [JsonPropertyName("VehiclePlateNumber")]
    public string? VehiclePlateNumber { get; set; }

    [JsonPropertyName("VehicleNetWeight")]
    [JsonConverter(typeof(NullableTwoDecimalConverter))]
    public decimal? VehicleNetWeight { get; set; }

    [JsonPropertyName("IsInternal")]
    public bool IsInternal { get; set; }

    /// <summary>Contract quirk: "VehicleLicenselId" (extra "l"). Do not rename.</summary>
    [JsonPropertyName("VehicleLicenselId")]
    public string? VehicleLicenselId { get; set; }

    [JsonPropertyName("VehicleLicenseExpiryDate")]
    public string? VehicleLicenseExpiryDate { get; set; }

    [JsonPropertyName("VehicleNote")]
    public string? VehicleNote { get; set; }

    [JsonPropertyName("VehicleType")]
    public string? VehicleType { get; set; }

    [JsonPropertyName("CompanyId")]
    public string? CompanyId { get; set; }

    [JsonPropertyName("UserId")]
    public string? UserId { get; set; }
}
