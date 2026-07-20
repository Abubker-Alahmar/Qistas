using Microsoft.EntityFrameworkCore;
using Qistas.Application.Outbox;
using Qistas.Infrastructure.Outbox;
using Qistas.Infrastructure.Persistence;
using Xunit;

namespace Qistas.Tests.Outbox;

/// <summary>
/// Fast, always-run unit tests for <see cref="SqlServerOutboxRepository"/> against EF Core's
/// InMemory provider. This is purely an EF Core testing tool (not SQLite, not a production
/// dependency) -- it exercises the LINQ query/update logic in the repository without
/// requiring a real SQL Server, complementing (not replacing)
/// <see cref="SqlServerOutboxRepositoryTests"/>, which is gated behind
/// QISTAS_TEST_SQLSERVER_CONNSTRING and proves the real SQL Server path.
/// </summary>
public sealed class EfCoreOutboxRepositoryInMemoryTests
{
    private static QistasDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<QistasDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new QistasDbContext(options);
    }

    private static OutboxMessage NewMessage(string reference = "ref-1", int attempts = 0, OutboxStatus status = OutboxStatus.Failed) => new()
    {
        ScaleSystemReferenceId = reference,
        Operation = "setEntryWeightDetails",
        Environment = "Dev",
        PayloadJson = "{\"LoadId\":\"LOAD-1\"}",
        Status = status,
        Attempts = attempts,
        CreatedUtc = DateTime.UtcNow,
        UpdatedUtc = DateTime.UtcNow,
    };

    [Fact]
    public async Task AddAsync_Then_GetByReferenceIdAsync_RoundTrips()
    {
        await using var dbContext = NewContext();
        var repository = new SqlServerOutboxRepository(dbContext);

        var message = NewMessage(reference: "ref-roundtrip");
        long id = await repository.AddAsync(message, CancellationToken.None);

        Assert.True(id > 0);

        var found = await repository.GetByReferenceIdAsync("ref-roundtrip", CancellationToken.None);

        Assert.Single(found);
        Assert.Equal(id, found[0].Id);
        Assert.Equal(OutboxStatus.Failed, found[0].Status);
    }

    [Fact]
    public async Task GetRetryableAsync_ExcludesMessagesAtOrAboveMaxAttempts()
    {
        await using var dbContext = NewContext();
        var repository = new SqlServerOutboxRepository(dbContext);

        await repository.AddAsync(NewMessage(reference: "ref-under", attempts: 1), CancellationToken.None);
        await repository.AddAsync(NewMessage(reference: "ref-at-max", attempts: 3), CancellationToken.None);

        var retryable = await repository.GetRetryableAsync(maxAttempts: 3, take: 10, CancellationToken.None);

        Assert.Contains(retryable, m => m.ScaleSystemReferenceId == "ref-under");
        Assert.DoesNotContain(retryable, m => m.ScaleSystemReferenceId == "ref-at-max");
    }

    [Fact]
    public async Task MarkSentAsync_TransitionsStatusAndStoresResponse()
    {
        await using var dbContext = NewContext();
        var repository = new SqlServerOutboxRepository(dbContext);

        long id = await repository.AddAsync(NewMessage(reference: "ref-sent"), CancellationToken.None);

        await repository.MarkSentAsync(id, "{\"Status\":true}", CancellationToken.None);

        var updated = await repository.GetByIdAsync(id, CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal(OutboxStatus.Sent, updated!.Status);
        Assert.Equal("{\"Status\":true}", updated.LastResponseJson);
    }

    [Fact]
    public async Task MarkFailedAttemptAsync_IncrementsAttemptsAndRecordsError()
    {
        await using var dbContext = NewContext();
        var repository = new SqlServerOutboxRepository(dbContext);

        long id = await repository.AddAsync(NewMessage(reference: "ref-fail", attempts: 1), CancellationToken.None);

        await repository.MarkFailedAttemptAsync(id, "Timeout on retry", "{\"Status\":false}", CancellationToken.None);

        var updated = await repository.GetByIdAsync(id, CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal(OutboxStatus.Failed, updated!.Status);
        Assert.Equal(2, updated.Attempts);
        Assert.Equal("Timeout on retry", updated.LastError);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByStatus()
    {
        await using var dbContext = NewContext();
        var repository = new SqlServerOutboxRepository(dbContext);

        long pendingId = await repository.AddAsync(NewMessage(reference: "ref-all-1"), CancellationToken.None);
        long manualId = await repository.AddAsync(NewMessage(reference: "ref-all-2"), CancellationToken.None);
        await repository.MarkManualAsync(manualId, CancellationToken.None);

        var manualOnly = await repository.GetAllAsync(OutboxStatus.Manual, take: 50, CancellationToken.None);
        var failedOnly = await repository.GetAllAsync(OutboxStatus.Failed, take: 50, CancellationToken.None);

        Assert.Contains(manualOnly, m => m.Id == manualId);
        Assert.DoesNotContain(manualOnly, m => m.Id == pendingId);
        Assert.Contains(failedOnly, m => m.Id == pendingId);
    }
}
