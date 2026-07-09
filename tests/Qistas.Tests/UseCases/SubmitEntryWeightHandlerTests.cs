using Qistas.Application.Abstractions;
using Qistas.Application.Outbox;
using Qistas.Application.UseCases;
using Qistas.Domain.Contracts;
using Qistas.Domain.Models;
using Qistas.Tests.Fakes;
using Xunit;

namespace Qistas.Tests.UseCases;

/// <summary>
/// Covers SubmitEntryWeightHandler's three-way split: local validation failure never
/// touches D365 or the outbox; a D365 business rejection is surfaced as a failure without
/// queuing (there is nothing to retry -- D365 said no); a transport failure queues the
/// message so nothing is silently dropped (AGENT_INSTRUCTION.md section 5).
/// </summary>
public class SubmitEntryWeightHandlerTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 7, 8, 0, 0, TimeSpan.Zero);

    private static EntryWeightSubmission ValidSubmission(DateOnly licenseExpiry) => new()
    {
        LoadId = "LOAD-1",
        CompanyId = "Bell",
        UserId = "operator1",
        ScaleSystemReferenceId = "ref-entry-1",
        EntryWeightKg = 12000.00m,
        EntryDateTimeUtc = FixedNow,
        Driver = new DriverInfo
        {
            DriverName = "Mohamed Ali",
            NationalId = "119xxxxxxxx",
            LicenseId = "DL-1",
            LicenseExpiryDate = licenseExpiry,
        },
        Vehicle = new VehicleInfo
        {
            PlateNumber = "12345",
            LicenseId = "VL-1",
            LicenseExpiryDate = licenseExpiry,
        },
    };

    private static (SubmitEntryWeightHandler handler, FakeD365Client client, FakeOutboxRepository outbox) CreateHandler()
    {
        var client = new FakeD365Client();
        var outbox = new FakeOutboxRepository();
        var environmentProvider = new FakeActiveEnvironmentProvider(D365Environment.Dev);
        var clock = new FakeClock(FixedNow);

        var handler = new SubmitEntryWeightHandler(client, outbox, environmentProvider, clock);
        return (handler, client, outbox);
    }

    [Fact]
    public async Task HandleAsync_ExpiredDriverLicense_ShortCircuitsWithoutCallingD365()
    {
        var (handler, client, outbox) = CreateHandler();
        var submission = ValidSubmission(licenseExpiry: DateOnly.FromDateTime(FixedNow.UtcDateTime).AddDays(-1));

        var result = await handler.HandleAsync(submission, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(0, client.EntryCallCount);
        Assert.Empty(outbox.Messages);
    }

    [Fact]
    public async Task HandleAsync_TransportFailure_QueuesMessageToOutbox()
    {
        var (handler, client, outbox) = CreateHandler();
        client.EntryResultFactory = _ => D365CallResult<D365Response>.TransportFailure("Connection timed out");

        var submission = ValidSubmission(licenseExpiry: DateOnly.FromDateTime(FixedNow.UtcDateTime).AddYears(1));

        var result = await handler.HandleAsync(submission, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(1, client.EntryCallCount);
        Assert.Single(outbox.Messages);

        var queued = outbox.Messages[0];
        Assert.Equal("setEntryWeightDetails", queued.Operation);
        Assert.Equal(OutboxStatus.Failed, queued.Status);
        Assert.Equal(1, queued.Attempts);
        Assert.Equal("ref-entry-1", queued.ScaleSystemReferenceId);
    }

    [Fact]
    public async Task HandleAsync_D365BusinessRejection_FailsWithoutQueuing()
    {
        var (handler, client, outbox) = CreateHandler();
        client.EntryResultFactory = request => D365CallResult<D365Response>.Ok(
            new D365Response { Status = false, Message = "Load ID not found" }, "{}");

        var submission = ValidSubmission(licenseExpiry: DateOnly.FromDateTime(FixedNow.UtcDateTime).AddYears(1));

        var result = await handler.HandleAsync(submission, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Load ID not found", result.Message);
        Assert.False(result.WasAlreadyProcessed);
        Assert.Equal(1, client.EntryCallCount);
        Assert.Empty(outbox.Messages);
    }

    [Fact]
    public async Task HandleAsync_D365Success_ReturnsSuccessAndRecordsSentAuditRow()
    {
        var (handler, client, outbox) = CreateHandler();
        client.EntryResultFactory = request => D365CallResult<D365Response>.Ok(
            new D365Response { Status = true, Message = "OK" }, "{}");

        var submission = ValidSubmission(licenseExpiry: DateOnly.FromDateTime(FixedNow.UtcDateTime).AddYears(1));

        var result = await handler.HandleAsync(submission, CancellationToken.None);

        Assert.True(result.Success);
        // Successful entry calls are recorded as Sent for crash reconciliation (edge case
        // #17: Balance reconciles Table_StillInside against the outbox on startup).
        var audit = Assert.Single(outbox.Messages);
        Assert.Equal(OutboxStatus.Sent, audit.Status);
        Assert.Equal("setEntryWeightDetails", audit.Operation);
        Assert.Equal(submission.ScaleSystemReferenceId, audit.ScaleSystemReferenceId);
    }
}
