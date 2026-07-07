using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Qistas.Application.Logging;
using Qistas.Infrastructure.Options;

namespace Qistas.Infrastructure.Logging;

/// <summary>
/// SQLite-backed integration log, stored in the same database file as the failed-message
/// archive (Outbox table). Kept in Qistas's own DB rather than BalanceSAHEL_New on
/// purpose: large JSON payloads must not bloat the production weighbridge database or its
/// backups, and the schema stays decoupled from the Balance app. Payloads are redacted
/// via SecretRedactor BEFORE they reach this repository.
/// </summary>
public sealed class SqliteIntegrationLogRepository : IIntegrationLogRepository
{
    private readonly string _connectionString;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public SqliteIntegrationLogRepository(IOptionsMonitor<QistasOptions> options)
    {
        _connectionString = $"Data Source={options.CurrentValue.Outbox.SqlitePath}";
    }

    public async Task AddAsync(IntegrationLogEntry entry, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(
            """
            INSERT INTO IntegrationLog
                (TimestampUtc, Environment, Operation, Success, HttpStatusCode, RequestJson, ResponseJson, Error, DurationMs)
            VALUES
                (@TimestampUtc, @Environment, @Operation, @Success, @HttpStatusCode, @RequestJson, @ResponseJson, @Error, @DurationMs)
            """,
            new
            {
                TimestampUtc = entry.TimestampUtc.ToString("O"),
                entry.Environment,
                entry.Operation,
                Success = entry.Success ? 1 : 0,
                entry.HttpStatusCode,
                entry.RequestJson,
                entry.ResponseJson,
                entry.Error,
                entry.DurationMs,
            });
    }

    public async Task<IReadOnlyList<IntegrationLogEntry>> GetRecentAsync(
        string? operation, bool? success, int take, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync(
            """
            SELECT Id, TimestampUtc, Environment, Operation, Success, HttpStatusCode,
                   RequestJson, ResponseJson, Error, DurationMs
            FROM IntegrationLog
            WHERE (@Operation IS NULL OR Operation = @Operation)
              AND (@Success IS NULL OR Success = @Success)
            ORDER BY Id DESC
            LIMIT @Take
            """,
            new
            {
                Operation = operation,
                Success = success is null ? (int?)null : (success.Value ? 1 : 0),
                Take = take,
            });

        return rows.Select(MapRow).ToList();
    }

    private static IntegrationLogEntry MapRow(dynamic row) => new()
    {
        Id = (long)row.Id,
        TimestampUtc = DateTime.Parse((string)row.TimestampUtc, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind),
        Environment = (string)row.Environment,
        Operation = (string)row.Operation,
        Success = (long)row.Success == 1,
        HttpStatusCode = row.HttpStatusCode is null ? null : (int?)(long)row.HttpStatusCode,
        RequestJson = (string?)row.RequestJson,
        ResponseJson = (string?)row.ResponseJson,
        Error = (string?)row.Error,
        DurationMs = (long)row.DurationMs,
    };

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await connection.ExecuteAsync(
                """
                CREATE TABLE IF NOT EXISTS IntegrationLog (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    TimestampUtc TEXT NOT NULL,
                    Environment TEXT NOT NULL,
                    Operation TEXT NOT NULL,
                    Success INTEGER NOT NULL,
                    HttpStatusCode INTEGER NULL,
                    RequestJson TEXT NULL,
                    ResponseJson TEXT NULL,
                    Error TEXT NULL,
                    DurationMs INTEGER NOT NULL
                );
                CREATE INDEX IF NOT EXISTS IX_IntegrationLog_Operation ON IntegrationLog (Operation);
                """);

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }
}
