namespace Qistas.Application.Abstractions;

/// <summary>Testable clock abstraction -- always UTC, never culture/locale dependent.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
