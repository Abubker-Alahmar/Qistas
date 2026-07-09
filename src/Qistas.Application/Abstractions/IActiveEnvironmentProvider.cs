using Qistas.Domain.Models;

namespace Qistas.Application.Abstractions;

/// <summary>
/// Resolves the currently configured D365 environment (Qistas:ActiveEnvironment) plus its
/// non-secret settings, and must be shown prominently wherever Qistas surfaces state, to
/// avoid a Dev/Prod mix-up (Balance/CLAUDE.md #16.20).
/// </summary>
public interface IActiveEnvironmentProvider
{
    D365Environment GetActiveEnvironment();

    D365EnvironmentSettings GetSettings(D365Environment environment);

    /// <summary>
    /// Switches the active environment at runtime (in-memory override on top of
    /// appsettings.json's Qistas:ActiveEnvironment). Used by PUT /api/admin/config.
    /// Deliberately does NOT touch ClientId/ClientSecret -- those remain
    /// config/DPAPI-only per AGENT_INSTRUCTION.md section 7 and are never accepted over
    /// this endpoint.
    /// </summary>
    void SetActiveEnvironment(D365Environment environment);
}

public sealed record D365EnvironmentSettings(
    D365Environment Environment,
    string BaseUrl,
    string Tenant,
    string CompanyId,
    string ClientId,
    bool HasClientSecret);
