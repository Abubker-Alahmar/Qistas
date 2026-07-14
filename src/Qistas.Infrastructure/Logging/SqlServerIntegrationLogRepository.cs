using Microsoft.EntityFrameworkCore;
using Qistas.Application.Logging;
using Qistas.Infrastructure.Persistence;

namespace Qistas.Infrastructure.Logging;

/// <summary>
/// EF Core-backed integration log, stored in the same database as the failed-message
/// archive (Outbox table). Kept in Qistas's own DB rather than BalanceSAHEL_New on
/// purpose: large JSON payloads must not bloat the production weighbridge database or its
/// backups, and the schema stays decoupled from the Balance app. Payloads are redacted
/// via SecretRedactor BEFORE they reach this repository. Table creation/schema management
/// is handled entirely by EF Core migrations -- no raw SQL/runtime DDL here.
/// </summary>
public sealed class SqlServerIntegrationLogRepository : IIntegrationLogRepository
{
    private readonly QistasDbContext _dbContext;

    public SqlServerIntegrationLogRepository(QistasDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(IntegrationLogEntry entry, CancellationToken cancellationToken)
    {
        _dbContext.IntegrationLogs.Add(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IntegrationLogEntry>> GetRecentAsync(
        string? operation, bool? success, int take, CancellationToken cancellationToken)
    {
        var query = _dbContext.IntegrationLogs.AsNoTracking().AsQueryable();

        if (operation is not null)
        {
            query = query.Where(l => l.Operation == operation);
        }

        if (success is not null)
        {
            query = query.Where(l => l.Success == success);
        }

        return await query
            .OrderByDescending(l => l.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
    }
}
