using System.Text.Json.Serialization;

namespace Qistas.Domain.Contracts;

/// <summary>
/// Response body from the Azure AD v1 token endpoint
/// (https://login.microsoftonline.com/{tenant}/oauth2/token), client_credentials grant.
/// The request itself is standard POST application/x-www-form-urlencoded, NOT the
/// GET-with-body shown in the Postman collection (Balance/CLAUDE.md #16.23 -- flagged
/// for confirmation with Ferdas; Qistas implements the standard POST form).
/// </summary>
public sealed class AzureAdTokenResponse
{
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public string ExpiresIn { get; set; } = "3599";

    [JsonPropertyName("ext_expires_in")]
    public string? ExtExpiresIn { get; set; }

    [JsonPropertyName("expires_on")]
    public string? ExpiresOn { get; set; }

    [JsonPropertyName("not_before")]
    public string? NotBefore { get; set; }

    [JsonPropertyName("resource")]
    public string? Resource { get; set; }

    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;
}
