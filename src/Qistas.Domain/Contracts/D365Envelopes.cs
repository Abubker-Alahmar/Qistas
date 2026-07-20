using System.Text.Json.Serialization;

namespace Qistas.Domain.Contracts;

/// <summary>
/// Common request envelope for all three BTOLoadIntService operations. The wire property
/// is literally "_request" (APIs V2.0 "Common Request Scheme" / Alsahl Postman
/// collection) -- NOT "{operation}_request". Do not rename.
/// </summary>
public sealed class D365RequestEnvelope<TRequest>
{
    [JsonPropertyName("_request")]
    public required TRequest Request { get; init; }
}

/// <summary>
/// Common response shape for all three operations (APIs V2.0 "Common Response Scheme").
/// Responses are FLAT: "$id", "Context", "CompanyId", "Status", "Message" at top level --
/// there is no "{operation}Result" wrapper. "$id" members are DataContract serializer
/// metadata; unknown members must be ignored, never treated as errors
/// (AGENT_INSTRUCTION.md section 2). Response "CompanyId" may differ in case from the
/// request ("Bell" vs "BELL") -- compare via <see cref="D365ResponseSemantics.CompanyIdMatches"/>.
/// </summary>
public sealed class D365Response
{
    [JsonPropertyName("$id")]
    public string? DollarId { get; set; }

    [JsonPropertyName("Context")]
    public D365ResponseContext? Context { get; set; }

    [JsonPropertyName("CompanyId")]
    public string? CompanyId { get; set; }

    [JsonPropertyName("Status")]
    public bool Status { get; set; }

    [JsonPropertyName("Message")]
    public string? Message { get; set; }
}

/// <summary>
/// The "Context" object present in all three responses: LoadHeader, LoadLines,
/// DriverDetails, VehicleDetails (each echoed back with response-side extras such as
/// "UserId" -- capital I -- and "CompanyId").
/// </summary>
public sealed class D365ResponseContext
{
    [JsonPropertyName("$id")]
    public string? Id { get; set; }

    [JsonPropertyName("LoadHeader")]
    public LoadHeader? LoadHeader { get; set; }

    [JsonPropertyName("LoadLines")]
    public List<LoadLine> LoadLines { get; set; } = new();

    [JsonPropertyName("DriverDetails")]
    public DriverDetailsResponse? DriverDetails { get; set; }

    [JsonPropertyName("VehicleDetails")]
    public VehicleDetailsResponse? VehicleDetails { get; set; }
}
