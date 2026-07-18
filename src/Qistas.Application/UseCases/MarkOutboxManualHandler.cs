using Qistas.Application.Outbox;

namespace Qistas.Application.UseCases;

/// <summary>
/// Marks an outbox message as "Manual" -- a human has decided to resolve it outside the
/// automated retry flow (e.g. correcting data directly in D365). Used by the admin
/// failed-message review screen (PLAN.md 1.5 / Balance/CLAUDE.md section 14 item 4).
/// </summary>
public sealed class MarkOutboxManualHandler
{
    private readonly IOutboxRepository _outbox;

    public MarkOutboxManualHandler(IOutboxRepository outbox)
    {
        _outbox = outbox;
    }

    public async Task<bool> HandleAsync(long outboxId, CancellationToken cancellationToken)
    {
        var message = await _outbox.GetByIdAsync(outboxId, cancellationToken);
        if (message is null)
        {
            return false;
        }

        await _outbox.MarkManualAsync(outboxId, cancellationToken);
        return true;
    }
}
