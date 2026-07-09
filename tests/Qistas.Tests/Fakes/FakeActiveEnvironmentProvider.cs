using Qistas.Application.Abstractions;
using Qistas.Domain.Models;

namespace Qistas.Tests.Fakes;

/// <summary>Hand-rolled fake for <see cref="IActiveEnvironmentProvider"/>.</summary>
public sealed class FakeActiveEnvironmentProvider : IActiveEnvironmentProvider
{
    private readonly Dictionary<D365Environment, D365EnvironmentSettings> _settings = new();
    private D365Environment _active;

    public FakeActiveEnvironmentProvider(D365Environment active, D365EnvironmentSettings? settings = null)
    {
        _active = active;
        _settings[active] = settings ?? new D365EnvironmentSettings(
            active, $"https://{active}.example.com/", "AlsahlGroup.com", "BELL", "client-id", true);
    }

    public void AddSettings(D365Environment environment, D365EnvironmentSettings settings) =>
        _settings[environment] = settings;

    public D365Environment GetActiveEnvironment() => _active;

    public D365EnvironmentSettings GetSettings(D365Environment environment) => _settings[environment];

    public void SetActiveEnvironment(D365Environment environment) => _active = environment;
}
