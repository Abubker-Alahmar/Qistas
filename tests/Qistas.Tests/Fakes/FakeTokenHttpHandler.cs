using System.Net;
using System.Net.Http;
using System.Text;

namespace Qistas.Tests.Fakes;

/// <summary>
/// Fake <see cref="HttpMessageHandler"/> standing in for the Azure AD token endpoint.
/// Returns a canned "expires_in" token JSON, echoing back the "client_id" posted in the
/// x-www-form-urlencoded body (so per-environment cache isolation can be asserted -- Dev
/// and Test environments post different client_id values and must get different tokens).
/// </summary>
public sealed class FakeTokenHttpHandler : HttpMessageHandler
{
    public int CallCount { get; private set; }
    public List<string> RequestBodies { get; } = new();

    /// <summary>Seconds until expiry to embed in the canned response ("expires_in"). Default matches the real Azure AD value (3599s).</summary>
    public string ExpiresInSeconds { get; set; } = "3599";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        string body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        RequestBodies.Add(body);

        string clientId = ExtractFormValue(body, "client_id");
        string accessToken = $"token-for-{clientId}";

        string json =
            $$"""
            {
              "token_type": "Bearer",
              "expires_in": "{{ExpiresInSeconds}}",
              "ext_expires_in": "{{ExpiresInSeconds}}",
              "expires_on": "0",
              "not_before": "0",
              "resource": "https://example.com/",
              "access_token": "{{accessToken}}"
            }
            """;

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private static string ExtractFormValue(string body, string key)
    {
        foreach (string pair in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = pair.Split('=', 2);
            if (parts.Length == 2 && Uri.UnescapeDataString(parts[0]) == key)
            {
                return Uri.UnescapeDataString(parts[1]);
            }
        }

        return string.Empty;
    }
}

/// <summary>Single-client <see cref="IHttpClientFactory"/> fake -- always returns the same pre-wired <see cref="HttpClient"/>.</summary>
public sealed class SingleClientFactory : IHttpClientFactory
{
    private readonly HttpClient _client;

    public SingleClientFactory(HttpClient client) => _client = client;

    public HttpClient CreateClient(string name) => _client;
}
