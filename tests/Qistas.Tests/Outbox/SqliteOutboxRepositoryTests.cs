using Qistas.Application.Outbox;
using Qistas.Infrastructure.Options;
using Qistas.Infrastructure.Outbox;
using Qistas.Tests.Fakes;
using Xunit;

namespace Qistas.Tests.Outbox;

/// <summary>
/// Exercises the real SQLite-backed <see cref="SqliteOutboxRepository"/> (schema creation,
/// add/read/update) against a private temp-file database per test class instance -- proves
/// the actual persistence path, not just the in-memory fake used by the use-case tests.
/// </summary>
public sealed class SqliteOutboxRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteOutboxRepository _repository;

    public SqliteOutboxRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"qistas-outbox-tests-{Guid.NewGuid():N}.db");
        var options = new QistasOptions { Outbox = new OutboxOptions { SqlitePath = _dbPath } };
        _repository = new SqliteOutboxRepository(new TestOptionsMonitor<QistasOptions>(options));
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup only; not test-critical.
        }
    }

    private static OutboxMessage NewMessage(string reference = "ref-1", string operation = "setEntryWeightDetails", int attempts = 0) => new()
    {
        ScaleSystemReferenceId = reference,
        Operation = operation,
        Environment = "Dev",
        PayloadJson = "{\"LoadId\":\"LOAD-1\"}",
        Status = OutboxStatus.Failed,
        Attempts = attempts,
        LastError = "Connection timed out",
        CreatedUtc = DateTime.UtcNow,
        UpdatedUtc = DateTime.UtcNow,
    };

    [Fact]
    public async Task AddAsync_Then_GetByReferenceIdAsync_RoundTrips()
    {
        var message = NewMessage(reference: "ref-roundtrip");

        long id = await _repository.AddAsync(message, CancellationToken.None);
        Assert.True(id > 0);

        var found = await _repository.GetByReferenceIdAsync("ref-roundtrip", CancellationToken.None);

        Assert.Single(found);
        Assert.Equal(id, found[0].Id);
        Assert.Equal("setEntryWeightDetails", found[0].Operation);
        Assert.Equal("Dev", found[0].Environment);
        Assert.Equal(OutboxStatus.Failed, found[0].Status);
        Assert.Equal("{\"LoadId\":\"LOAD-1\"}", found[0].PayloadJson);
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        var found = await _repository.GetByIdAsync(999_999, CancellationToken.None);
        Assert.Null(found);
    }

    [Fact]
    public async Task GetRetryableAsync_ExcludesMessagesAtOrAboveMaxAttempts()
    {
        await _repository.AddAsync(NewMessage(reference: "ref-under", attempts: 1), CancellationToken.None);
        await _repository.AddAsync(NewMessage(reference: "ref-at-max", attempts: 3), CancellationToken.None);

        var retryable = await _repository.GetRetryableAsync(maxAttempts: 3, take: 10, CancellationToken.None);

        Assert.Contains(retryable, m => m.ScaleSystemReferenceId == "ref-under");
        Assert.DoesNotContain(retryable, m => m.ScaleSystemReferenceId == "ref-at-max");
    }

    [Fact]
    public async Task GetRetryableAsync_OnlyReturnsPendingOrFailed()
    {
        var sent = NewMessage(reference: "ref-sent", attempts: 0);
        sent.Status = OutboxStatus.Sent;
        long sentId = await _repository.AddAsync(sent, CancellationToken.None);
        await _repository.MarkSentAsync(sentId, "{}", CancellationToken.None);

        await _repository.AddAsync(NewMessage(reference: "ref-failed", attempts: 0), CancellationToken.None);

        var retryable = await _repository.GetRetryableAsync(maxAttempts: 5, take: 10, CancellationToken.None);

        Assert.DoesNotContain(retryable, m => m.ScaleSystemReferenceId == "ref-sent");
        Assert.Contains(retryable, m => m.ScaleSystemReferenceId == "ref-failed");
    }

    [Fact]
    public async Task MarkSentAsync_TransitionsStatusAndStoresResponse()
    {
        long id = await _repository.AddAsync(NewMessage(reference: "ref-sent-transition"), CancellationToken.None);

        await _repository.MarkSentAsync(id, "{\"Status\":true}", CancellationToken.None);

        var updated = await _repository.GetByIdAsync(id, CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal(OutboxStatus.Sent, updated!.Status);
        Assert.Equal("{\"Status\":true}", updated.LastResponseJson);
    }

    [Fact]
    public async Task MarkFailedAttemptAsync_IncrementsAttemptsAndRecordsError()
    {
        long id = await _repository.AddAsync(NewMessage(reference: "ref-fail-transition", attempts: 1), CancellationToken.None);

        await _repository.MarkFailedAttemptAsync(id, "Timeout on retry", "{\"Status\":false}", CancellationToken.None);

        var updated = await _repository.GetByIdAsync(id, CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal(OutboxStatus.Failed, updated!.Status);
        Assert.Equal(2, updated.Attempts);
        Assert.Equal("Timeout on retry", updated.LastError);
    }

    [Fact]
    public async Task MarkManualAsync_TransitionsStatusToManual()
    {
        long id = await _repository.AddAsync(NewMessage(reference: "ref-manual-transition"), CancellationToken.None);

        await _repository.MarkManualAsync(id, CancellationToken.None);

        var updated = await _repository.GetByIdAsync(id, CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal(OutboxStatus.Manual, updated!.Status);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByStatus()
    {
        long pendingId = await _repository.AddAsync(NewMessage(reference: "ref-all-1"), CancellationToken.None);
        long manualId = await _repository.AddAsync(NewMessage(reference: "ref-all-2"), CancellationToken.None);
        await _repository.MarkManualAsync(manualId, CancellationToken.None);

        var manualOnly = await _repository.GetAllAsync(OutboxStatus.Manual, take: 50, CancellationToken.None);
        var failedOnly = await _repository.GetAllAsync(OutboxStatus.Failed, take: 50, CancellationToken.None);

        Assert.Contains(manualOnly, m => m.Id == manualId);
        Assert.DoesNotContain(manualOnly, m => m.Id == pendingId);
        Assert.Contains(failedOnly, m => m.Id == pendingId);
    }
}
