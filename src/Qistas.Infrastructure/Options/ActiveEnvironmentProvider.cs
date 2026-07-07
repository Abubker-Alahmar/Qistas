using Microsoft.Extensions.Options;
using Qistas.Application.Abstractions;
using Qistas.Domain.Models;

namespace Qistas.Infrastructure.Options;

/// <summary>
/// Reads Qistas:ActiveEnvironment + Qistas:Environments:{env} from configuration. Bound
/// live via IOptionsMonitor so a config file change takes effect without a restart. A
/// runtime override (set via PUT /api/admin/config -> SetActiveEnvironment) takes
/// precedence over the file value until the process restarts, which then re-reads from
/// configuration again -- this keeps the override intentionally non-persistent so a
/// forgotten manual switch can never silently survive a deployment/restart into the
/// wrong environment (Balance/CLAUDE.md #16.20).
/// </summary>
public sealed class ActiveEnvironmentProvider : IActiveEnvironmentProvider
{
    private readonly IOptionsMonitor<QistasOptions> _options;
    private D365Environment? _runtimeOverride;
    private readonly object _lock = new();

    public ActiveEnvironmentProvider(IOptionsMonitor<QistasOptions> options)
    {
        _options = options;
    }

    public D365Environment GetActiveEnvironment()
    {
        lock (_lock)
        {
            if (_runtimeOverride is not null)
            {
                return _runtimeOverride.Value;
            }
        }

        var value = _options.CurrentValue.ActiveEnvironment;
        return Enum.TryParse<D365Environment>(value, ignoreCase: true, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"Invalid Qistas:ActiveEnvironment value '{value}'.");
    }

    public void SetActiveEnvironment(D365Environment environment)
    {
        lock (_lock)
        {
            _runtimeOverride = environment;
        }
    }

    public D365EnvironmentSettings GetSettings(D365Environment environment)
    {
        var options = _options.CurrentValue;
        string key = environment.ToString();

        if (!options.Environments.TryGetValue(key, out var envOptions))
        {
            throw new InvalidOperationException($"No D365 environment configuration found for '{key}'.");
        }

        return new D365EnvironmentSettings(
            environment,
            envOptions.BaseUrl,
            options.Tenant,
            envOptions.CompanyId,
            envOptions.ClientId,
            HasClientSecret: !string.IsNullOrEmpty(envOptions.ClientSecretProtected));
    }
}
