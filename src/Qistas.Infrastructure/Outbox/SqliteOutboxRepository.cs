using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Qistas.Application.Outbox;
using Qistas.Infrastructure.Options;

namespace Qistas.Infrastructure.Outbox;

/// <summary>
/// SQLite-backed Outbox repository (Microsoft.Data.Sqlite + Dapper). Creates the table on
/// first use if it doesn't exist. This is the durable "never drop a message" store
/// referenced throughout AGENT_INSTRUCTION.md section 5 and PLAN.md 1.3/1.6.
/// </summary>
public sealed class SqliteOutboxRepository : IOutboxRepository
{
    private readonly string _connectionString;
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);
    private static volatile bool _schemaEnsured;

    public SqliteOutboxRepository(IOptionsMonitor<QistasOptions> options)
    {
        string path = options.CurrentValue.Outbox.SqlitePath;
        _connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        if (_schemaEnsured)
        {
            return;
        }

        await SchemaLock.WaitAsync(cancellationToken);
        try
        {
            if (_schemaEnsured)
            {
                return;
            }

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string createTableSql = """
                CREATE TABLE IF NOT EXISTS Outbox (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ScaleSystemReferenceId TEXT NOT NULL,
                    Operation TEXT NOT NULL,
                    Environment TEXT NOT NULL,
                    PayloadJson TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    Attempts INTEGER NOT NULL DEFAULT 0,
                    LastError TEXT NULL,
                    LastResponseJson TEXT NULL,
                    CreatedUtc TEXT NOT NULL,
                    UpdatedUtc TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS IX_Outbox_ScaleSystemReferenceId ON Outbox(ScaleSystemReferenceId);
                CREATE INDEX IF NOT EXISTS IX_Outbox_Status ON Outbox(Status);
                """;

            await connection.ExecuteAsync(createTableSql);
            _schemaEnsured = true;
        }
        finally
        {
            SchemaLock.Release();
        }
    }

    public async Task<long> AddAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        const string sql = """
            INSERT INTO Outbox
                (ScaleSystemReferenceId, Operation, Environment, PayloadJson, Status, Attempts, LastError, LastResponseJson, CreatedUtc, UpdatedUtc)
            VALUES
                (@ScaleSystemReferenceId, @Operation, @Environment, @PayloadJson, @Status, @Attempts, @LastError, @LastResponseJson, @CreatedUtc, @UpdatedUtc);
            SELECT last_insert_rowid();
            """;

        var id = await connection.ExecuteScalarAsync<long>(sql, new
        {
            message.ScaleSystemReferenceId,
            message.Operation,
            message.Environment,
            message.PayloadJson,
            Status = message.Status.ToString(),
            message.Attempts,
            message.LastError,
            message.LastResponseJson,
            CreatedUtc = message.CreatedUtc.ToString("O"),
            UpdatedUtc = message.UpdatedUtc.ToString("O"),
        });

        return id;
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetRetryableAsync(int maxAttempts, int take, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        const string sql = """
            SELECT * FROM Outbox
            WHERE Status IN ('Pending', 'Failed') AND Attempts < @MaxAttempts
            ORDER BY CreatedUtc ASC
            LIMIT @Take;
            """;

        var rows = await connection.QueryAsync<OutboxRow>(sql, new { MaxAttempts = maxAttempts, Take = take });
        return rows.Select(MapRow).ToList();
    }

    public async Task<OutboxMessage?> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<OutboxRow>("SELECT * FROM Outbox WHERE Id = @Id;", new { Id = id });
        return row is null ? null : MapRow(row);
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetByReferenceIdAsync(string scaleSystemReferenceId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<OutboxRow>(
            "SELECT * FROM Outbox WHERE ScaleSystemReferenceId = @Ref ORDER BY CreatedUtc DESC;",
            new { Ref = scaleSystemReferenceId });
        return rows.Select(MapRow).ToList();
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetAllAsync(OutboxStatus? statusFilter, int take, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        string sql = statusFilter is null
            ? "SELECT * FROM Outbox ORDER BY CreatedUtc DESC LIMIT @Take;"
            : "SELECT * FROM Outbox WHERE Status = @Status ORDER BY CreatedUtc DESC LIMIT @Take;";

        var rows = await connection.QueryAsync<OutboxRow>(sql, new { Status = statusFilter?.ToString(), Take = take });
        return rows.Select(MapRow).ToList();
    }

    public async Task MarkSentAsync(long id, string? responseJson, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(
            "UPDATE Outbox SET Status = 'Sent', LastResponseJson = @ResponseJson, UpdatedUtc = @Now WHERE Id = @Id;",
            new { Id = id, ResponseJson = responseJson, Now = DateTime.UtcNow.ToString("O") });
    }

    public async Task MarkFailedAttemptAsync(long id, string error, string? responseJson, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(
            """
            UPDATE Outbox
            SET Status = 'Failed', Attempts = Attempts + 1, LastError = @Error, LastResponseJson = @ResponseJson, UpdatedUtc = @Now
            WHERE Id = @Id;
            """,
            new { Id = id, Error = error, ResponseJson = responseJson, Now = DateTime.UtcNow.ToString("O") });
    }

    public async Task MarkManualAsync(long id, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(
            "UPDATE Outbox SET Status = 'Manual', UpdatedUtc = @Now WHERE Id = @Id;",
            new { Id = id, Now = DateTime.UtcNow.ToString("O") });
    }

    private static OutboxMessage MapRow(OutboxRow row) => new()
    {
        Id = row.Id,
        ScaleSystemReferenceId = row.ScaleSystemReferenceId,
        Operation = row.Operation,
        Environment = row.Environment,
        PayloadJson = row.PayloadJson,
        Status = Enum.Parse<OutboxStatus>(row.Status),
        Attempts = row.Attempts,
        LastError = row.LastError,
        LastResponseJson = row.LastResponseJson,
        CreatedUtc = DateTime.Parse(row.CreatedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind),
        UpdatedUtc = DateTime.Parse(row.UpdatedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind),
    };

    /// <summary>Flat Dapper projection matching the Outbox table columns 1:1.</summary>
    private sealed class OutboxRow
    {
        public long Id { get; set; }
        public string ScaleSystemReferenceId { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public string Environment { get; set; } = string.Empty;
        public string PayloadJson { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int Attempts { get; set; }
        public string? LastError { get; set; }
        public string? LastResponseJson { get; set; }
        public string CreatedUtc { get; set; } = string.Empty;
        public string UpdatedUtc { get; set; } = string.Empty;
    }
}
