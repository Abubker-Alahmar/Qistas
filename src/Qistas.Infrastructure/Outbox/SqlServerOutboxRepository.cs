using Microsoft.EntityFrameworkCore;
using Qistas.Application.Outbox;
using Qistas.Infrastructure.Persistence;

namespace Qistas.Infrastructure.Outbox;

/// <summary>
/// EF Core-backed Outbox repository (Microsoft.EntityFrameworkCore.SqlServer, no raw SQL).
/// This is the durable "never drop a message" store referenced throughout
/// AGENT_INSTRUCTION.md section 5 and PLAN.md 1.3/1.6. Table creation/schema management is
/// handled entirely by EF Core migrations (see src/Qistas.Infrastructure/Migrations) --
/// there is no runtime "create table if not exists" logic here anymore.
/// </summary>
public sealed class SqlServerOutboxRepository : IOutboxRepository
{
    private readonly QistasDbContext _dbContext;

    public SqlServerOutboxRepository(QistasDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<long> AddAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        _dbContext.Outbox.Add(message);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return message.Id;
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetRetryableAsync(int maxAttempts, int take, CancellationToken cancellationToken)
    {
        return await _dbContext.Outbox
            .AsNoTracking()
            .Where(m => (m.Status == OutboxStatus.Pending || m.Status == OutboxStatus.Failed) && m.Attempts < maxAttempts)
            .OrderBy(m => m.CreatedUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<OutboxMessage?> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        return await _dbContext.Outbox
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetByReferenceIdAsync(string scaleSystemReferenceId, CancellationToken cancellationToken)
    {
        return await _dbContext.Outbox
            .AsNoTracking()
            .Where(m => m.ScaleSystemReferenceId == scaleSystemReferenceId)
            .OrderByDescending(m => m.CreatedUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetAllAsync(OutboxStatus? statusFilter, int take, CancellationToken cancellationToken)
    {
        var query = _dbContext.Outbox.AsNoTracking().AsQueryable();

        if (statusFilter is not null)
        {
            query = query.Where(m => m.Status == statusFilter);
        }

        return await query
            .OrderByDescending(m => m.CreatedUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task MarkSentAsync(long id, string? responseJson, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Outbox.FindAsync(new object[] { id }, cancellationToken);
        if (entity is null)
        {
            return;
        }

        entity.Status = OutboxStatus.Sent;
        entity.LastResponseJson = responseJson;
        entity.UpdatedUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAttemptAsync(long id, string error, string? responseJson, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Outbox.FindAsync(new object[] { id }, cancellationToken);
        if (entity is null)
        {
            return;
        }

        entity.Status = OutboxStatus.Failed;
        entity.Attempts += 1;
        entity.LastError = error;
        entity.LastResponseJson = responseJson;
        entity.UpdatedUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkManualAsync(long id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Outbox.FindAsync(new object[] { id }, cancellationToken);
        if (entity is null)
        {
            return;
        }

        entity.Status = OutboxStatus.Manual;
        entity.UpdatedUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
