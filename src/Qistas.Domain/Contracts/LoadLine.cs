using System.Text.Json.Serialization;
using Qistas.Domain.Json;

namespace Qistas.Domain.Contracts;

/// <summary>
/// A single load line inside a response Context (APIs V2.0): ItemId, ItemName,
/// ItemDescription, UnitId, Qty, ItemNetWeight, ItemGrossWeight, BatchNumber,
/// BatchExpirationDate, LocationId, LocationName. "BatchExpirationDate" may carry the
/// sentinel "1900-01-01T12:00:00" (= empty). Weights/quantities are per D365's UnitId
/// (normally "KG") -- always normalize to KG with 2 decimals before comparing/sending
/// (Balance/CLAUDE.md #16.11; see Qistas.Domain.Validation.WeightUnitConverter).
/// </summary>
public sealed class LoadLine
{
    [JsonPropertyName("$id")]
    public string? DollarId { get; set; }

    [JsonPropertyName("ItemId")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("ItemName")]
    public string? ItemName { get; set; }

    [JsonPropertyName("ItemDescription")]
    public string? ItemDescription { get; set; }

    [JsonPropertyName("UnitId")]
    public string UnitId { get; set; } = "KG";

    [JsonPropertyName("Qty")]
    [JsonConverter(typeof(TwoDecimalConverter))]
    public decimal Qty { get; set; }

    [JsonPropertyName("ItemNetWeight")]
    [JsonCo