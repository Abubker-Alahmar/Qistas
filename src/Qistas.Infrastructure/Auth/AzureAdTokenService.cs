using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qistas.Application.Abstractions;
using Qistas.Domain.Contracts;
using Qistas.Domain.Json;
using Qistas.Domain.Models;
using Qistas.Infrastructure.Options;

namespace Qistas.Infrastructure.Auth;

/// <summary>
/// Azure AD v1 client-credentials token service. Caches one token per
/// <see cref="D365Environment"/> -- a token from one environment must never be sent to
/// another (AGENT_INSTRUCTION.md section 4; Balance/CLAUDE.md #16.20). Thread-safe via a
/// per-environment <see cref="SemaphoreSlim"/>; never requests a token per call or in a
/// loop. Proactively refreshes ~5 minutes before expiry, and exposes
/// <see cref="InvalidateToken"/> for the 401-triggered refresh-once-and-retry-once flow
/// in <see cref="D365.D365Client"/>.
/// </summary>
public sealed class AzureAdTokenService : ITokenService
{
    private static readonly TimeSpan ProactiveRefreshWindow = TimeSpan.FromMinutes(5);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<QistasOptions> _options;
    private readonly ISecretProtector _secretProtector;
    private readonly ILogger<AzureAdTokenService> _logger;

    private readonly ConcurrentDictionary<D365Environment, SemaphoreSlim> _locks = new();
    private readonly ConcurrentDictionary<D365Environment, CachedToken> _cache = new();

    public AzureAdTokenService(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<QistasOptions> options,
        ISecretProtector secretProtector,
        ILogger<AzureAdTokenService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _secretProtector = secretProtector;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync(D365Environment environment, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(environment, out var cached) && !NeedsRefresh(cached))
        {
            return cached.AccessToken;
        }

        var gate = _locks.GetOrAdd(environment, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            // Re-check after acquiring the lock -- another caller may have refreshed already.
            if (_cache.TryGetValue(environment, out cached) && !NeedsRefresh(cached))
            {
                return cached.AccessToken;
            }

            var fresh = await AcquireTokenAsync(environment, cancellationToken);
            _cache[environment] = fresh;
            return fresh.AccessToken;
        }
        finally
        {
            gate.Release();
        }
    }

    public void InvalidateToken(D365Environment environment)
    {
        _cache.TryRemove(environment, out _);
    }

    public TokenStatus GetStatus(D365Environment environment)
    {
        if (_cache.TryGetValue(environment, out var cached))
        {
            return new TokenStatus(environment, HasToken: true, cached.ExpiresAtUtc, IsExpired: NeedsRefresh(cached));
        }

        return new TokenStatus(environment, HasToken: false, ExpiresAtUtc: null, IsExpired: true);
    }

    private static bool NeedsRefresh(CachedToken token) =>
        DateTimeOffset.UtcNow >= token.ExpiresAtUtc - ProactiveRefreshWindow;

    private async Task<CachedToken> AcquireTokenAsync(D365Environment environment, CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        string envKey = environment.ToString();

        if (!options.Environments.TryGetValue(envKey, out var envOptions))
        {
            throw new InvalidOperationException($"No D365 environment configuration found for '{envKey}'.");
        }

        string clientSecret;
        try
        {
            if (!string.IsNullOrEmpty(envOptions.ClientSecretProtected))
            {
                clientSecret = _secretProtector.Unprotect(envOptions.ClientSecretProtected);
            }
            else if (!string.IsNullOrEmpty(envOptions.ClientSecret))
            {
                // Dev/Test convenience fallback (plaintext from user-secrets/env-vars/
                // untracked appsettings). Warn on every acquisition so it cannot slip
                // into production unnoticed (AGENT_INSTRUCTION.md section 7).
                _logger.LogWarning(
                    "Environment '{Environment}' is using a PLAINTEXT ClientSecret. Acceptable for Dev/Test only -- use ClientSecretProtected (DPAPI) in production.",
                    envKey);
                clientSecret = envOptions.ClientSecret;
            }
            else
            {
                clientSecret = string.Empty;
            }
        }
        catch (Exception ex)
        {
            // DPAPI unprotect fails if the secret was encrypted on a different machine or
            // Windows user profile (e.g. config copied between scale PCs). Surface a clear
            // auth-configuration failure, not a generic crypto error (edge case #22).
            throw new InvalidOperationException(
                $"Could not decrypt the client secret for environment '{envKey}'. It was likely " +
                "encrypted on a different machine/user profile -- re-enter the secret via the " +
                "admin screen on this machine.", ex);
        }

        if (string.IsNullOrWhiteSpace(options.Tenant) || string.IsNullOrWhiteSpace(envOptions.ClientId))
        {
            throw new InvalidOperationException(
                $"D365 environment '{envKey}' is missing Tenant/ClientId configuration -- configure via the admin screen.");
        }

        var client = _httpClientFactory.CreateClient("QistasTokenClient");
        string tokenUrl = $"https://login.microsoftonline.com/{options.Tenant}/oauth2/token";

        // Standard client_credentials POST, application/x-www-form-urlencoded. The
        // Postman collection shows GET-with-body; that is non-standard and NOT what we
        // implement here -- flagged for confirmation with Ferdas (Balance/CLAUDE.md #16.23).
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = envOptions.ClientId,
            ["client_secret"] = clientSecret,
            ["resource"] = envOptions.BaseUrl,
        };

        using var content = new FormUrlEncodedContent(form);
        using var response = await client.PostAsync(tokenUrl, content, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Token request failed for environment {Environment}: {StatusCode}",
                environment, response.StatusCode);
            throw new InvalidOperationException($"Token request failed with status {(int)response.StatusCode}.");
        }

        var tokenResponse = JsonSerializer.Deserialize<AzureAdTokenResponse>(body, QistasJson.Options)
            ?? throw new InvalidOperationException("Empty token response from Azure AD.");

        int expiresInSeconds = int.TryParse(tokenResponse.ExpiresIn, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 3599;

        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds);

        return new CachedToken(tokenResponse.AccessToken, expiresAt);
    }

    private sealed record CachedToken(string AccessToken, DateTimeOffset ExpiresAtUtc);
}
