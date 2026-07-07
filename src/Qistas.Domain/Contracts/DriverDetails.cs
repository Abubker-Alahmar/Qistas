using System.Text.Json.Serialization;

namespace Qistas.Domain.Contracts;

/// <summary>
/// Driver details sent inside the setEntryWeightDetails "_request". Full details are sent
/// on EVERY call even if the driver already exists in D365 -- D365 only creates if new
/// (AGENT_INSTRUCTION.md / Balance/CLAUDE.md #13). Field list matches APIs V2.0 exactly:
/// no Userid here (that sits at the "_request" top level), no DriverPhone in the request
/// (response-only).
/// </summary>
public sealed class DriverDetails
{
    /// <summary>Mandatory.</summary>
    [JsonPropertyName("DriverNationalId")]
    public string DriverNationalId { get; set; } = string.Empty;

    /// <summary>Mandatory.</summary>
    [JsonPropertyName("DriverName")]
    public string DriverName { get; set; } = string.Empty;

    /// <summary>Optional -- only when the driver is an Alsahl Group worker.</summary>
    [JsonPropertyName("DriverInternalId")]
    public string? DriverInternalId { get; set; }

    /// <summary>Optional -- only when the driver is an Alsahl Group worker.</summary>
    [JsonPropertyName("IsInternal")]
    public bool IsInternal { get; set; }

    /// <summary>Mandatory.</summary>
    [JsonPropertyName("DriverLicenseId")]
    public string DriverLicenseId { get; set; } = string.Empty;

    /// <summary>Mandatory. Sent as ISO "yyyy-MM-dd".</summary>
    [JsonPropertyName("DriverLicenseExpiryDate")]
    public string DriverLicenseExpiryDate { get; set; } = string.Empty;
}

/// <summary>
/// Driver details as echoed back inside a response Context. Contract quirk: the response
/// carries "UserId" (capital I) unlike the request top-level "Userid". Response dates may
/// be "2029-03-26T12:00:00" or sentinel "1900-01-01T12:00:00" (= empty) -- kept as raw
/// strings here; parse via LenientNullableDateTimeConverter helpers when needed.
/// </summary>
public sealed class DriverDetailsResponse
{
    [JsonPropertyName("$id")]
    public string? DollarId { get; set; }

    [JsonPropertyName("Dri