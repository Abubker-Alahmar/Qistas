namespace Qistas.Infrastructure.Options;

/// <summary>
/// Root configuration section "Qistas". Bound from appsettings.json + environment
/// variables/user-secrets for anything sensitive. Never put a real client_secret in a
/// committed appsettings file (AGENT_INSTRUCTION.md section 7).
/// </summary>
public sealed class QistasOptions
{
    public const string SectionName = "Qistas";

    /// <summary>Which of Environments{Dev,Test,Prod} is currently active.</summary>
    public string ActiveEnvironment { get; set; } = "Dev";

    public string Tenant { get; set; } = string.Empty;

    public Dictionary<string, D365EnvironmentOptions> Environments { get; set; } = new();

    public RetryOptions Retry { get; set; } = new();

    public OutboxOptions Outbox { get; set; } = new();
}

public sealed class D365EnvironmentOptions
{
    public string BaseUrl { get; set; } = string.Empty;

    public string CompanyId { get; set; } = string.Empty;

    /// <summary>Azure AD application (client) id. Empty by default -- must be configured per-deployment.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// DPAPI-protected client_secret (produced by ISecretProtector.Protect). Left empty in
    /// committed files -- configure via the admin screen, an untracked appsettings file,
    /// or user-secrets/environment variables.
    /// </summary>
    public string ClientSecretProtected { get; set; } = string.Empty;

    /// <summary>
    /// PLAINTEXT client_secret -- Dev/Test convenience ONLY (user-secrets, environment
    /// variable, or an untracked appsettings.Development.json). Used only when
    /// <see cref="ClientSecretProtected"/> is empty; a warning is logged. NEVER put a
    /// production secret here and NEVER commit a value (AGENT_INSTRUCTION.md section 7).
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;
}

public sealed class RetryOptions
{
    public int MaxAttempts { get; set; } = 3;

    public int BaseDelaySeconds { get; set; } = 2;

    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>Fraction of requests in the sampling window that must fail to trip the breaker (0-1).</summary>
    public double CircuitBreakerFailureRatio { get; set; } = 0.5;

    public int CircuitBreakerSamplingDurationSeconds { get; set; } = 30;

    public int CircuitBreakerMinimumThroughput { get; set; } = 5;

    public int CircuitBreakerBreakDurationSeconds { get; set; } = 30;
}

public sealed class OutboxOptions
{
    /// <summary>Unused since the automatic background retry worker was removed (owner
    /// decision: archived messages are handled manually). Kept for config compatibility.</summary>
    public int BatchSize { get; set; } = 25;

    /// <summary>Unused -- see <see cref="BatchSize"/>.</summary>
    public int PollIntervalSeconds { get; set; } = 30;
}
