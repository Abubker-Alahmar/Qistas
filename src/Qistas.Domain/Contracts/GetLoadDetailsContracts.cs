using System.Text.Json.Serialization;

namespace Qistas.Domain.Contracts;

/// <summary>
/// Inner "_request" body of getLoadDetails (APIs V2.0). Wrapped by
/// <see cref="D365RequestEnvelope{TRequest}"/>. The response (common
/// <see cref="D365Response"/>) is the AUTHORITATIVE validation reference -- always
/// re-fetched immediately before exit validation, never the entry-time snapshot, because
/// items/qty can be edited in D365 during loading (Balance/CLAUDE.md #16.7).
/// </summary>
public sealed class GetLoadDetailsRequest
{
    [JsonPropertyName("CompanyId")]
    public string CompanyId { get; set; } = string.Empty;

    /// <summary>Contract quirk: lower-case "id" in requests. Do not rename.</summary>
    [JsonPropertyName("Userid")]
    public string Userid { get; set; } = string.Empty;

    [JsonPropertyName("LoadId")]
    public string LoadId { get; set; } = string.Empty;
}
