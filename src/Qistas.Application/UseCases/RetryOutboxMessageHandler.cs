using System.Text.Json;
using Qistas.Application.Abstractions;
using Qistas.Application.Mapping;
using Qistas.Application.Outbox;
using Qistas.Domain.Contracts;
using Qistas.Domain.Json;
using Qistas.Domain.Models;

namespace Qistas.Application.UseCases;

/// <summary>
/// Re-sends a single outbox message (manual "Retry now" from the admin screen, or the
/// Worker's periodic sweep). Deserializes the stored request payload by
/// <see cref="OutboxMessage.Operation"/> and replays it against the same environment it
/// was originally queued for (AGENT_INSTRUCTION.md section 4: never cross environments).
/// </summary>
public sealed class RetryOutboxMessageHandler
{
    private readonly IOutboxRepository _outbox;
    private readonly ID365Client _client;
    private readonly IClock _clock;

    public RetryOutboxMessageHandler(IOutboxRepository outbox, ID365Client client, IClock clock)
    {
        _outbox = outbox;
        _client = client;
        _clock = clock;
    }

    public async Task<D365OperationResult> HandleAsync(long outboxId, int maxAttempts, CancellationToken cancellationToken)
    {
        var message = await _outbox.GetByIdAsync(outboxId, cancellationToken);
        if (message is null)
        {
            return D365OperationResult.Fail("Outbox message not found.", rawJson: null);
        }

        if (!Enum.TryParse<D365Environment>(message.Environment, out var environment))
        {
            return D365OperationResult.Fail($"Unknown environment '{message.Environment}' on outbox message.", rawJson: null);
        }

        D365OperationResult result;

        try
        {
            result = message.Operation switch
            {
                "setEntryWeightDetails" => await RetryEntryAsync(message, environment, cancellationToken),
                "setExitWeightDetails" => await RetryExitAsync(message, environment, cancellationToken),
                _ => D365OperationResult.Fail($"Unsupported outbox operation '{message.Operation}'.", rawJson: null),
            };
        }
        catch (Exception ex)
        {
            result = D365OperationResult.Fail(ex.Message, rawJson: null);
        }

        if (result.Success)
        {
            await _outbox.MarkSentAsync(message.Id, result.RawResponseJson, cancellationToken);
        }
        else if (message.Attempts + 1 >= maxAttempts)
        {
            await _outbox.MarkFailedAttemptAsync(message.Id, result.Message ?? "Retry failed.", result.RawResponseJson, cancellationToken);
        }
        else
        {
            await _outbox.MarkFailedAttemptAsync(message.Id, result.Message ?? "Retry failed, will retry again.", result.RawResponseJson, cancellationToken);
        }

        return result;
    }

    private async Task<D365OperationResult> RetryEntryAsync(OutboxMessage message, D365Environment environment, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<SetEntryWeightDetailsRequest>(message.PayloadJson, QistasJson.Options)
            ?? throw new InvalidOperationException("Could not deserialize entry-weight payload.");

        var callResult = await _client.SetEntryWeightDetailsAsync(request, environment, cancellationToken);
        if (!callResult.TransportSucceeded)
        {
            return D365OperationResult.Fail(callResult.TransportError, rawJson: null);
        }

        return EntryWeightMapper.ToDomainResult(callResult.Response, callResult.RawResponseJson, callResult.TransportError);
    }

    private async Task<D365OperationResult> RetryExitAsync(OutboxMessage message, D365Environment environment, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<SetExitWeightDetailsRequest>(message.PayloadJson, QistasJson.Options)
            ?? throw new InvalidOperationException("Could not deserialize exit-weight payload.");

        var callResult = await _client.SetExitWeightDetailsAsync(request, environment, cancellationToken);
        if (!callResult.TransportSucceeded)
        {
            return D365OperationResult.Fail(callResult.TransportError, rawJson: null);
        }

        // Ghost-success / duplicate handling applies on retries too -- see
        // ExitWeightMapper.ToDomainResult and AGENT_INSTRUCTION.md section 6.
        return ExitWeightMapper.ToDomainResult(callResult.Response, callResult.RawResponseJson, callResult.TransportError);
    }
}
