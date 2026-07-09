using Qistas.Application.Abstractions;

namespace Qistas.Tests.Fakes;

/// <summary>Deterministic <see cref="IClock"/> fake -- tests set UtcNow explicitly.</summary>
public sealed class FakeClock : IClock
{
    public FakeClock(DateTimeOffset utcNow) => UtcNow = utcNow;

    public DateTimeOffset UtcNow { get; set; }
}
