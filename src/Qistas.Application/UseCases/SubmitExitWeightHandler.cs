using Qistas.Application.Abstractions;
using Qistas.Application.Mapping;
using Qistas.Application.Outbox;
using Qistas.Domain.Contracts;
using Qistas.Domain.Json;
using Qistas.Domain.Models;
using Qistas.Domain.Validation;

namespace Qistas.Application.UseCases;

/// <summary>
/// Use case for Weight-Out: does ONE thing -- submits setExitWeightDetails to D365. It does
/// NOT re-fetch getLoadDetails (Balance already fetched a fresh load and validated tolerance
/// at Weight-Out screen open, GET /api/scale/loads/{loadId}); the only local check left here
/// is basic weight sanity (exit > entry). Access-token acquisition/refresh is handled inside
/// <see cref="ID365Client"/>. <see cref="ExitWeightSubmission.ScaleSystemReferenceId"/>
/// (Balance's Transaction GUID) is the idempotency key -- before calling D365, checks whether
/// this reference already has a Sent outbox record so a duplicate submit (e.g. operator
/// double-click, or a retry after a ghost-success timeout) is short-circuited
/// (AGENT_INSTRUCTION.md section 6). On transport failure the resilience pipeline's retry
/// rule runs first (HttpClient-level Polly policy); once exhausted, the message is archived
/// to the BalanceOutbox table for manual review -- never dropped.
/// </summary>
public sealed class SubmitExitWeightHandler
{
    private readonly ID365Client _client;
    private readonly IOutboxRepository _outbox;
    private readonly IActiveEnvironmentProvider _environmentProvider;
    private readonly IClock _clock;

    public SubmitExitWeightHandler(
        ID365Client client,
        IOutboxRepository outbox,
        IActiveEnvironmentProvider environmentProvider,
        IClock clock)
    {
        _client = client;
        _outbox = outbox;
        _environmentProvider = environmentProvider;
        _clock = clock;
    }

    public async Task<D365OperationResult> HandleAsync(
        ExitWeightSubmission submission,
        CancellationToken cancellationToken)
    {
        // Idempotency guard: if a previous attempt for this exact reference already made
        // it to D365 (recorded as Sent), never submit it again as a new logical transaction.
        var priorMessages = await _outbox.GetByReferenceIdAsync(submission.ScaleSystemReferenceId, cancellationToken);
        var priorSent = priorMessages.FirstOrDefault(m =>
            m is { Operation: "setExitWeightDetails", Status: OutboxStatus.Sent });
        if (priorSent is not null)
        {
            return D365OperationResult.Ok(
                "Already submitted for this transaction (idempotent replay).",
                priorSent.LastResponseJson,
                alreadyProcessed: true);
        }

        // Only basic weight sanity is checked here -- tolerance against load-line weights
        // was already validated by Balance using the freshly fetched load from call point 2
        // (GET /api/scale/loads/{loadId}), so it is not re-checked against D365 again here.
        var validation = D365Validation.ValidateExitSubmission(submission);
        if (!validation.IsValid)
        {
            return D365OperationResult.Fail(string.Join(" | ", validation.Errors), rawJson: null);
        }

        var environment = _environmentProvider.GetActiveEnvironment();
        var request = ExitWeightMapper.ToContract(submission);

        var callResult = await _client.SetExitWeightDetailsAsync(request, environment, cancellationToken);

        if (!callResult.TransportSucceeded)
        {
            await ArchiveFailedAsync(submission, environment, request, callResult.TransportError, cancellationToken);
            return D365OperationResult.Fail(
                $"Could not reach D365 ({callResult.TransportError}). Saved locally and archived for manual review -- do not re-weigh; an employee will re-send or enter it in D365.",
                rawJson: null);
        }

        var domainResult = ExitWeightMapper.ToDomainResult(callResult.Response, callResult.RawResponseJson, callResult.TransportError);

        if (domainResult.Success)
        {
            await RecordSentAsync(submission, environment, request, callResult.RawResponseJson, cancellationToken);
        }

        return domainResult;
    }

    private async Task ArchiveFailedAsync(
        ExitWeightSubmission submission,
        D365Environment environment,
        SetExitWeightDetailsRequest request,
        string? error,
        CancellationToken cancellationToken)
    {
        var payloadJson = System.Text.Json.JsonSerializer.Serialize(request, QistasJson.Options);
        var now = _clock.UtcNow.UtcDateTime;

        await _outbox.AddAsync(new OutboxMessage
        {
            ScaleSystemReferenceId = submission.ScaleSystemReferenceId,
            Operation = "setExitWeightDetails",
            Environment = environment.ToString(),
            PayloadJson = payloadJson,
            Status = OutboxStatus.Failed,
            Attempts = 1,
            LastError = error,
            CreatedUtc = now,
            UpdatedUtc = now,
        }, cancellationToken);
    }

    private async Task RecordSentAsync(
        ExitWeightSubmission submission,
        D365Environment environment,
        SetExitWeightDetailsRequest request,
        string? responseJson,
        CancellationToken cancellationToken)
    {
        // Record successful sends too, so the idempotency guard above has something to
        // find on a later duplicate call (e.g. after an app crash + Balance-side retry).
        var payloadJson = System.Text.Json.JsonSerializer.Serialize(request, QistasJson.Options);
        var now = _clock.UtcNow.UtcDateTime;

        await _outbox.AddAsync(new OutboxMessage
        {
            ScaleSystemReferenceId = submission.ScaleSystemReferenceId,
            Operation = "setExitWeightDetails",
            Environment = environment.ToString(),
            PayloadJson = payloadJson,
            Status = OutboxStatus.Sent,
            Attempts = 1,
            LastResponseJson = responseJson,
            CreatedUtc = now,
            UpdatedUtc = now,
        }, cancellationToken);
    }
}
