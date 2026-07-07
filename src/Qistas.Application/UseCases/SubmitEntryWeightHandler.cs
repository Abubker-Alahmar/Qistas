using Qistas.Application.Abstractions;
using Qistas.Application.Mapping;
using Qistas.Application.Outbox;
using Qistas.Domain.Contracts;
using Qistas.Domain.Json;
using Qistas.Domain.Models;
using Qistas.Domain.Validation;

namespace Qistas.Application.UseCases;

/// <summary>
/// Use case for Weight-In: validates locally, calls setEntryWeightDetails (Polly retries
/// inline per configuration -- short, because the truck is waiting), and -- only when the
/// transport itself is exhausted (not on a business rejection from D365) -- ARCHIVES the
/// message in the database for manual employee action via the review screen. No automatic
/// background retry (AGENT_INSTRUCTION.md section 5). Only ever invoked for Sales Order
/// loads with a LoadId (scope guarded by the caller / Balance side).
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
            await ArchiveFailedAsync(submission, environment, request, callResult.TransportError, cancellationToken);
            return D365OperationResult.Fail(
                $"Could not reach D365 ({callResult.TransportError}). Saved locally and archived for manual review -- the truck may proceed.", rawJson: null);
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
        Cancella