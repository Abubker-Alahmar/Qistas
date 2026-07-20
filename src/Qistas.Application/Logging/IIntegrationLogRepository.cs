namespace Qistas.Application.Logging;

/// <summary>
/// Database-backed integration log (per owner requirement: logs live in the database,
/// not only in files). One row per D365/token call attempt -- request, response, outcome.
/// Exposed to employees via GET /api/admin/logs and the Balance review screen.
/// </summary>
public interface IIntegrationLogRepository
{
    Task AddAsync(IntegrationLogEntry entry, CancellationToken cancellationToken);

    Task<IReadOnlyList<IntegrationLogEntry>> GetRecentAsync(
        string? operation, bool? success, int take, CancellationToken cancellationToken);
}

public sealed class IntegrationLogEntry
{
    public long Id { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string Environment { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int? HttpStatusCode { get; set; }
    public string? RequestJson { get; set; }
    public string? ResponseJson { get; set; }
    public string? Error { get; set; }
    public long DurationMs { get; set; }
}
