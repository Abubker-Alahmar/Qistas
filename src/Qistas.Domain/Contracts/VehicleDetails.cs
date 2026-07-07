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

    /// <summary>Opt