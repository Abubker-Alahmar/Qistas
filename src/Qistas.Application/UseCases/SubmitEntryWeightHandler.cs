using Qistas.Application.Abstractions;
using Qistas.Application.Mapping;
using Qistas.Application.Outbox;
using Qistas.Domain.Contracts;
using Qistas.Domain.Json;
using Qistas.Domain.Models;
using Qistas.Domain.Validation;

namespace Qistas.Application.UseCases;

/// <summary>
/// Use case for Weight-In: validates locally, calls setEntryWeightDetails, and -- only
/// when the transport itself is exhausted (not on a business rejection from D365) --
/// queues the message to the outbox for later retry (AGENT_INSTRUCTION.md section 5).
/// Only ever invoked for Sales Order loads with a LoadId (scope guarded by the caller /
/// Balance side per AGENT_INSTRUCTION.md section 1).
/// </summary>
public sealed class SubmitEntryWeightHandler
{
    private readonly ID365Client _client;
    private readonly IOutboxRepository _outbox;
    private readonly IActiveEnvironmentProvider _environmentProvider;
    private readonly IClock _clock;

    public SubmitEntryWeightHandler(
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

    public async Task<D365OperationResult> HandleAsync(EntryWeightSubmission submission, CancellationToken cancellationToken)
    {
        var asOf = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var validation = D365Validation.ValidateEntrySubmission(submission, asOf);
        if (!validation.IsValid)
        {
            return D365OperationResult.Fail(string.Join(" | ", validation.Errors), rawJson: null);
        }

        var environment = _environmentProvider.GetActiveEnvironment();
        var request = EntryWeightMapper.ToContract(submission);

        var callResult = await _client.SetEntryWeightDetailsAsync(request, environment, cancellationToken);

        if (!callResult.TransportSucceeded)
        {
            await QueueForRetryAsync(submission, environment, request, callResult.TransportError, cancellationToken);
            return D365OperationResult.Fail(
                $"Could not reach D365 ({callResult.TransportError}). Queued for retry.", rawJson: null);
        }

        var domainResult = EntryWeightMapper.ToDomainResult(callResult.Response, callResult.RawResponseJson, callResult.TransportError);

        if (domainResult.Success)
        {
            // Record successful entry calls as Sent too: after a crash between entry and
            // exit (edge case #17), Balance reconciles Table_StillInside against the
            // outbox -- without this row a successful entry would leave no trace.
            await RecordSentAsync(submission, environment, request, callResult.RawResponseJson, cancellationToken);
        }

        return domainResult;
    }

    private async Task RecordSentAsync(
        EntryWeightSubmission submission,
        D365Environment environment,
        SetEntryWeightDetailsRequest request,
        string? responseJson,
        CancellationToken cancellationToken)
    {
        var payloadJson = System.Text.Json.JsonSerializer.Serialize(request, QistasJson.Options);
        var now = _clock.UtcNow.UtcDa