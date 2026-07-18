using Microsoft.EntityFrameworkCore;
using Qistas.Application.Logging;
using Qistas.Infrastructure.Logging;
using Qistas.Infrastructure.Persistence;
using Xunit;

namespace Qistas.Tests.Logging;

/// <summary>
/// Fast, always-run unit tests for <see cref="SqlServerIntegrationLogRepository"/> against
/// EF Core's InMemory provider (a testing tool, not a production dependency -- SQLite is
/// intentionally not used here).
/// </summary>
public sealed class EfCoreIntegrationLogRepositoryInMemoryTests
{
    private static QistasDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<QistasDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new QistasDbContext(options);
    }

    private static IntegrationLogEntry NewEntry(string operation = "setEntryWeightDetails", bool success = true) => new()
    {
        TimestampUtc = DateTime.UtcNow,
        Environment = "Dev",
        Operation = operation,
        Success = success,
        HttpStatusCode = success ? 200 : 500,
        DurationMs = 42,
    };

    [Fact]
    public async Task AddAsync_Then_GetRecentAsync_RoundTrips()
    {
        await using var dbContext = NewContext();
        var repository = new SqlServerIntegrationLogRepository(dbContext);

        await repository.AddAsync(NewEntry(operation: "getLoadDetails"), CancellationToken.None);

        var recent = await repository.GetRecentAsync(operation: null, success: null, take: 10, CancellationToken.None);

        Assert.Single(recent);
        Assert.Equal("getLoadDetails", recent[0].Operation);
    }

    [Fact]
    public async Task GetRecentAsync_FiltersByOperationAndSuccess_OrderedByIdDescending()
    {
        await using var dbContext = NewContext();
        var repository = new SqlServerIntegrationLogRepository(dbContext);

        await repository.AddAsync(NewEntry(operation: "setEntryWeightDetails", success: true), CancellationToken.None);
        await repository.AddAsync(NewEntry(operation: "setEntryWeightDetails", success: false), CancellationToken.None);
        await repository.AddAsync(NewEntry(operation: "setExitWeightDetails", success: true), CancellationToken.None);

        var entryOnly = await repository.GetRecentAsync("setEntryWeightDetails", success: null, take: 10, CancellationToken.None);
        Assert.Equal(2, entryOnly.Count);

        var entryFailedOnly = await repository.GetRecentAsync("setEntryWeightDetails", success: false, take: 10, CancellationToken.None);
        Assert.Single(entryFailedOnly);
        Assert.False(entryFailedOnly[0].Success);

        var all = await repository.GetRecentAsync(null, null, take: 10, CancellationToken.None);
        Assert.Equal(3, all.Count);
        Assert.True(all[0].Id > all[1].Id);
    }
}
