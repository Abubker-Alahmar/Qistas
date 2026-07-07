using Microsoft.Extensions.Options;

namespace Qistas.Tests.Fakes;

/// <summary>
/// Minimal hand-rolled <see cref="IOptionsMonitor{T}"/> that just returns a fixed value --
/// enough for constructing infrastructure classes (AzureAdTokenService,
/// SqliteOutboxRepository) in tests without spinning up the full DI/configuration stack.
/// </summary>
public sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
{
    public TestOptionsMonitor(T currentValue) => CurrentValue = currentValue;

    public T CurrentValue { get; }

    public T Get(string? name) => CurrentValue;

    public IDisposable OnChange(Action<T, string> listener) => NoopDisposable.Instance;

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();
        public void Dispose()
        {
        }
    }
}
