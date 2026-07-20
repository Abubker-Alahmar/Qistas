using Qistas.Application.Abstractions;
using Qistas.Application.Outbox;
using Qistas.Application.UseCases;
using Qistas.Domain.Contracts;
using Qistas.Domain.Models;
using Qistas.Tests.Fakes;
using Xunit;

namespace Qistas.Tests.UseCases;

/// <summary>
/// Covers the idempotency and outbox rules of SubmitExitWeightHandler
/// (AGENT_INSTRUCTION.md section 6): a prior Sent outbox row short-circuits without calling
/// D365; a "duplicate"/"already processed" business rejection is treated as a ghost
/// success; a normal business rejection fails without queuing; a transport failure queues
/// for retry.
/// </summary>
public class SubmitExitWeightHandlerTests
{
    private const string Reference = "ref-exit-1";
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 7, 9, 0, 0, TimeSpan.Zero);

    private static ExitWeightSubmission ValidSubmission() => new()
    {
        LoadId = "LOAD-1",
        CompanyId = "Bell",
        UserId = "operator1",
        ScaleSystemReferenceId = Reference,
        EntryWeightKg = 12000.00m,
        ExitWeightKg = 25000.00m,
        TotalNetWeightKg = 13000.00m,
        TotalGrossWeightKg = 1000.00m,
        ToleranceKg = 5.00m,
        ExitDateTimeUtc = FixedNow,
    };

    private static (SubmitExitWeightHandler handler, FakeD365Client client, FakeOutboxRepository outbox) CreateHandler()
    {
        var client = new FakeD365Client();
        var outbox = new FakeOutboxRepository();
        var environmentProvider = new FakeActiveEnvironmentProvider(D365Environment.Dev);
        var clock = new FakeClock(FixedNow);

        var handler = new SubmitExitWeightHandler(client, outbox, environmentProvider, clock);
        return (handler, client, outbox);
    }

    [Fact]
    public async Task HandleAsync_PriorSentOutboxRecordForSameReference_ShortCircuitsWithoutCallingD365()
    {
        var (handler, client, outbox) = CreateHandler();
        await outbox.AddAsync(new OutboxMessage
        {
            ScaleSystemReferenceId = Reference,
            Operation = "setExitWeightDetails",
            Environment = "Dev",
            PayloadJson = "{}",
            Status = OutboxStatus.Sent,
            LastResponseJson = "{\"Status\":true}",
            CreatedUtc = FixedNow.UtcDateTime.AddMinutes(-5),
            UpdatedUtc = FixedNow.UtcDateTime.AddMinutes(-5),
        }, CancellationToken.None);

        var result = await handler.HandleAsync(ValidSubmission(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.WasAlreadyProcessed);
        Assert.Equal(0, client.ExitCallCount);
    }

    [Fact]
    public async Task HandleAsync_D365ReportsDuplicateOnRetry_TreatedAsGhostSuccess()
    {
        var (handler, client, outbox) = CreateHandler();
        client.ExitResultFactory = _ => D365CallResult<D365Response>.Ok(
            new D365Response { Status = false, Message = "This ScaleSystemReferenceId was already processed" }, "{}");

        var result = await handler.HandleAsync(ValidSubmission(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.WasAlreadyProcessed);
        Assert.Equal(1, client.ExitCallCount);

        // A ghost-success is still recorded as Sent, so a further duplicate submit finds it.
        Assert.Contains(outbox.Messages, m => m.Status == OutboxStatus.Sent && m.ScaleSystemReferenceId == Reference);
    }

    [Fact]
    public async Task HandleAsync_D365NormalBusinessRejection_FailsAndDoesNotQueue()
    {
        var (handler, client, outbox) = CreateHandler();
        client.ExitResultFactory = _ => D365CallResult<D365Response>.Ok(
            new D365Response { Status = false, Message = "Load ID not found" }, "{}");

        var result = await handler.HandleAsync(ValidSubmission(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.WasAlreadyProcessed);
        Assert.Equal(1, client.ExitCallCount);
        Assert.Empty(outbox.Messages);
    }

    [Fact]
    public async Task HandleAsync_TransportFailure_QueuesToOutboxAsFailed()
    {
        var (handler, client, outbox) = CreateHandler();
        client.ExitResultFactory = _ => D365CallResult<D365Response>.TransportFailure("Connection refused");

        var result = await handler.HandleAsync(ValidSubmission(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(1, client.ExitCallCount);
        Assert.Single(outbox.Messages);
        Assert.Equal(OutboxStatus.Failed, outbox.Messages[0].Status);
        Assert.Equal("setExitWeightDetails", outbox.Messages[0].Operation);
    }

    [Fact]
    public async Task HandleAsync_ExitNotGreaterThanEntry_FailsLocallyWithoutCallingD365()
    {
        // The only local check left on this path -- tolerance is now Balance's
        // responsibility (validated against the load it already fetched at Weight-Out
        // screen open), so this endpoint no longer re-fetches getLoadDetails at all.
        var (handler, client, outbox) = CreateHandler();
        var submission = new ExitWeightSubmission
        {
            LoadId = "LOAD-1",
            CompanyId = "Bell",
            UserId = "operator1",
            ScaleSystemReferenceId = Reference,
            EntryWeightKg = 25000.00m,
            ExitWeightKg = 12000.00m,
            TotalNetWeightKg = 13000.00m,
            TotalGrossWeightKg = 1000.00m,
            ToleranceKg = 5.00m,
            ExitDateTimeUtc = FixedNow,
        };

        var result = await handler.HandleAsync(submission, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("swapped", result.Message);
        Assert.Equal(0, client.ExitCallCount);
        Assert.Empty(outbox.Messages);
    }
}
