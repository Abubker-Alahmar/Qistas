using Qistas.Application.Outbox;

namespace Qistas.Tests.Fakes;

/// <summary>
/// In-memory hand-rolled fake for <see cref="IOutboxRepository"/>, used by use-case tests
/// that need to assert whether a message was (or was not) queued, without touching SQLite.
/// The real repository is exercised separately by SqliteOutboxRepositoryTests.
/// </summary>
public sealed class FakeOutboxRepository : IOutboxRepository
{
    private readonly List<OutboxMessage> _messages = new();
    private long _nextId = 1;

    public IReadOnlyList<OutboxMessage> Messages => _messages;

    public Task<long> AddAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        message.Id = _nextId++;
        _messages.Add(message);
        return Task.FromResult(message.Id);
    }

    public Task<IReadOnlyList<OutboxMessage>> GetRetryableAsync(int maxAttempts, int take, CancellationToken cancellationToken)
    {
        var result = _messages
            .Where(m => (m.Status == OutboxStatus.Pending || m.Status == OutboxStatus.Failed) && m.Attempts < maxAttempts)
            .OrderBy(m => m.CreatedUtc)
            .Take(take)
            .ToList();
        return Task.FromResult<IReadOnlyList<OutboxMessage>>(result);
    }

    public Task<OutboxMessage?> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        return Task.FromResult(_messages.FirstOrDefault(m => m.Id == id));
    }

    public Task<IReadOnlyList<OutboxMessage>> GetByReferenceIdAsync(string scaleSystemReferenceId, CancellationToken cancellationToken)
    {
        var result = _messages
            .Where(m => m.ScaleSystemReferenceId == scaleSystemReferenceId)
            .OrderByDescending(m => m.CreatedUtc)
            .ToList();
        return Task.FromResult<IReadOnlyList<OutboxMessage>>(result);
    }

    public Task<IReadOnlyList<OutboxMessage>> GetAllAsync(OutboxStatus? statusFilter, int take, CancellationToken cancellationToken)
    {
        var query = statusFilter is null ? _messages.AsEnumerable() : _messages.Where(m => m.Status == statusFilter);
        var result = query.OrderByDescending(m => m.CreatedUtc).Take(take).ToList();
        return Task.FromResult<IReadOnlyList<OutboxMessage>>(result);
    }

    public Task MarkSentAsync(long id, string? responseJson, CancellationToken cancellationToken)
    {
        var message = _messages.First(m => m.Id == id);
        message.Status = OutboxStatus.Sent;
        message.LastResponseJson = responseJson;
        message.UpdatedUtc = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    public Task MarkFailedAttemptAsync(long id, string error, string? responseJson, CancellationToken cancellationToken)
    {
        var message = _messages.First(m => m.Id == id);
        message.Status = OutboxStatus.Failed;
        message.Attempts += 1;
        message.LastError = error;
        message.LastResponseJson = responseJson;
        message.UpdatedUtc = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    public Task MarkManualAsync(long id, CancellationToken cancellationToken)
    {
        var message = _messages.First(m => m.Id == id);
        message.Status = OutboxStatus.Manual;
        message.UpdatedUtc = DateTime.UtcNow;
        return Task.CompletedTask;
    }
}
