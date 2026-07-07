namespace Qistas.Application.Outbox;

/// <summary>
/// Persistence contract for the Outbox table. Implemented in Qistas.Infrastructure over
/// SQLite (Microsoft.Data.Sqlite + Dapper).
/// </summary>
public interface IOutboxRepository
{
    Task<long> AddAsync(OutboxMessage message, CancellationToken cancellationToken);

    /// <summary>Pending or Failed messages under the max attempt count, oldest first.</summary>
    Task<IReadOnlyList<OutboxMessage>> GetRetryableAsync(int maxAttempts, int take, CancellationToken cancellationToken);

    Task<OutboxMessage?> GetByIdAsync(long id, CancellationToken cancellationToken);

    Task<IReadOnlyList<OutboxMessage>> GetByReferenceIdAsync(string scaleSystemReferenceId, CancellationToken cancellationToken);

    Task<IReadOnlyList<OutboxMessage>> GetAllAsync(OutboxStatus? statusFilter, int take, CancellationToken cancellationToken);

    Task MarkSentAsync(long id, string? responseJson, CancellationToken cancellationToken);

    Task MarkFailedAttemptAsync(long id, string error, string? responseJson, CancellationToken cancellationToken);

    Task MarkManualAsync(long id, CancellationToken cancellationToken);
}
