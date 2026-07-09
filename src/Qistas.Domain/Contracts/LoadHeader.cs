using System.Text.Json.Serialization;
using Qistas.Domain.Json;

namespace Qistas.Domain.Contracts;

/// <summary>
/// "LoadHeader" object inside a response Context (APIs V2.0). Contract quirk: the vehicle
/// weight fields are spelled "Vehichle*" (transposed) -- NOT "Vehicle*". Preserve exactly
/// (AGENT_INSTRUCTION.md, section 2). Field list matches the documented response:
/// SalesId, LoadId, CustAccount, CustName, WarehouseId, WarehouseName, TotalNetWeight,
/// TotalGrossWeight, VehichleNetWeight, VehichleGrossWeight, Note.
/// </summary>
public sealed class LoadHeader
{
    [JsonPropertyName("$id")]
    public string? DollarId { get; set; }

    [JsonPropertyName("SalesId")]
    public string? SalesId { get; set; }

    [JsonPropertyName("LoadId")]
    public string LoadId { get; set; } = string.Empty;

    [JsonPropertyName("CustAccount")]
    public string? CustAccount { get; set; }

    [JsonPropertyName("CustName")]
    public string? CustName { get; set; }

    [JsonPropertyName("WarehouseId")]
    public string? WarehouseId { get; set; }

    [JsonPropertyName("WarehouseName")]
    public string? WarehouseName { get; set; }

    [JsonPropertyName("TotalNetWeight")]
    [JsonConverter(typeof(NullableTwoDecimalConverter))]
    public decimal? TotalNetWeight { get; set; }

    [JsonPropertyName("TotalGrossWeight")]
    [JsonConverter(typeof(NullableTwoDecimalConverter))]
    public decimal? TotalGrossWeight { get; set; }

    /// <summary>Contract quirk: "Vehichle" transposition, not "Vehicle".</summary>
    [JsonPropertyName("VehichleNetWeight")]
    [JsonConverter(typeof(NullableTwoDecimalConverter))]
    public decimal? VehichleNetWeight { get; set; }

    /// <summary>Contract quirk: "Vehichle" transposition, not "Vehicle".</summary>
    [JsonPropertyName("VehichleGrossWeight")]
    [JsonConverter(typeof(NullableTwoDecimalConverter))]
    public decimal? VehichleGrossWeight { get; set; }

    [JsonPropertyName("Note")]
    public string? Note { get; set; }
}
