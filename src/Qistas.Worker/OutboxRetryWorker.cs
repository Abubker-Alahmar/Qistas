using Microsoft.Extensions.Options;
using Qistas.Application.Outbox;
using Qistas.Application.UseCases;
using Qistas.Infrastructure.Options;

namespace Qistas.Worker;

/// <summary>
/// Polls the Outbox for Pending/Failed messages below the configured max attempts and
/// retries them with backoff (the retry/backoff itself happens inside the D365 client's
/// resilience pipeline per call; this loop just controls how often we re-attempt queued
/// messages). On exhaustion the message stays "Failed" for the admin review screen
/// (AGENT_INSTRUCTION.md section 5; PLAN.md 1.6).
///
/// Also serves as the reconciliation point for edge case #17 (app/PC crash between entry
/// and exit calls): because every write is durably queued before/after the D365 call,
/// Balance can query GET /api/admin/outbox on startup to reconcile Table_StillInside
/// against outbox status, rather than the Worker needing to guess at Balance's own state.
/// </summary>
public sealed class OutboxRetryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<QistasOptions> _options;
    private readonly ILogger<OutboxRetryWorker> _logger;

    public OutboxRetryWorker(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<QistasOptions> options,
        ILogger<OutboxRetryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Qistas outbox retry worker starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var outboxOptions = _options.CurrentValue.Outbox;
            var retryOptions = _options.CurrentValue.Retry;

            try
            {
                await ProcessBatchAsync(retryOptions.MaxAttempts, outboxOptions.BatchSize, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Unhandled error while processing outbox batch.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(outboxOptions.PollIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown.
            }
        }

        _logger.LogInformation("Qistas outbox retry worker stopping.");
    }

    private async Task ProcessBatchAsync(int maxAttempts, int batchSize, CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var retryHandler = scope.ServiceProvider.GetRequiredService<RetryOutboxMessageHandler>();

        var retryable = await outbox.GetRetryableAsync(maxAttempts, batchSize, stoppingToken);
        if (retryable.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Retrying {Count} outbox message(s).", retryable.Count);

        foreach (var message in retryable)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            var result = await retryHandler.HandleAsync(message.Id, maxAttempts, stoppingToken);
            _logger.LogInformation(
                "Outbox message {Id} ({Operation}, ref {ScaleSystemReferenceId}) retry result: Success={Success} Message={Message}",
                message.Id, message.Operation, message.ScaleSystemReferenceId, result.Success, result.Message);
        }
    }
}
