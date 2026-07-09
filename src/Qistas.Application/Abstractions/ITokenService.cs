using Qistas.Domain.Models;

namespace Qistas.Application.Abstractions;

/// <summary>
/// Azure AD client-credentials token cache, keyed per environment (AGENT_INSTRUCTION.md
/// section 4). Implemented in Qistas.Infrastructure.
/// </summary>
public interface ITokenService
{
    /// <summary>Returns a valid bearer token for the environment, refreshing if needed.</summary>
    Task<string> GetAccessTokenAsync(D365Environment environment, CancellationToken cancellationToken);

    /// <summary>Forces the next call to acquire a fresh token (used after a 401).</summary>
    void InvalidateToken(D365Environment environment);

    /// <summary>Non-secret status snapshot for the /api/admin/token-status and /api/health endpoints.</summary>
    TokenStatus GetStatus(D365Environment environment);
}

public sealed record TokenStatus(
    D365Environment Environment,
    bool HasToken,
    DateTimeOffset? ExpiresAtUtc,
    bool IsExpired);
