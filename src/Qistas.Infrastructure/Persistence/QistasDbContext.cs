using Microsoft.EntityFrameworkCore;
using Qistas.Application.Logging;
using Qistas.Application.Outbox;

namespace Qistas.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for Qistas's own database (the "QistasLogDb" connection string) --
/// covers exactly two tables: BalanceOutbox (the durable failed-message archive for
/// Balance-originated writes) and IntegrationLog (per-call D365/token integration log).
/// This intentionally does NOT
/// include Serilog's own "Logs" table: that table is created/managed independently by the
/// Serilog.Sinks.MSSqlServer sink (see appsettings.json) and must stay outside EF Core's
/// migration model.
///
/// The existing Application-layer POCOs (<see cref="OutboxMessage"/>,
/// <see cref="IntegrationLogEntry"/>) are reused directly as EF Core entities rather than
/// introducing parallel Entity/DTO types -- they were already simple persistence-shaped
/// POCOs with no behavior, so mapping them straight through Fluent API keeps this file as
/// the single source of truth for the schema instead of splitting it across a mapping
/// layer.
/// </summary>
public sealed class QistasDbContext : DbContext
{
    public QistasDbContext(DbContextOptions<QistasDbContext> options)
        : base(options)
    {
    }

    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    // NOTE: the DbSet property name/CLR type stay as "OutboxMessage"/"Outbox" -- only the
    // physical table name changes (see ToTable("BalanceOutbox") below) so it's clear in the
    // database itself which project (Balance) this archive belongs to.

    public DbSet<IntegrationLogEntry> IntegrationLogs => Set<IntegrationLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("BalanceOutbox");

            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();

            builder.Property(x => x.ScaleSystemReferenceId).IsRequired().HasMaxLength(200);
            builder.Property(x => x.Operation).IsRequired().HasMaxLength(100);
            builder.Property(x => x.Environment).IsRequired().HasMaxLength(50);
            builder.Property(x => x.PayloadJson).IsRequired();
            builder.Property(x => x.LastError);
            builder.Property(x => x.LastResponseJson);

            // Stored as a string to match the pre-EF raw-SQL behavior, which persisted
            // OutboxStatus via .ToString()/Enum.Parse rather than as an integer.
            builder.Property(x => x.Status)
                .IsRequired()
                .HasMaxLength(50)
                .HasConversion<string>();

            builder.Property(x => x.Attempts).IsRequired().HasDefaultValue(0);
            builder.Property(x => x.CreatedUtc).IsRequired();
            builder.Property(x => x.UpdatedUtc).IsRequired();

            builder.HasIndex(x => x.ScaleSystemReferenceId).HasDatabaseName("IX_BalanceOutbox_ScaleSystemReferenceId");
            builder.HasIndex(x => x.Status).HasDatabaseName("IX_BalanceOutbox_Status");
        });

        modelBuilder.Entity<IntegrationLogEntry>(builder =>
        {
            builder.ToTable("IntegrationLog");

            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();

            builder.Property(x => x.TimestampUtc).IsRequired();
            builder.Property(x => x.Environment).IsRequired().HasMaxLength(50);
            builder.Property(x => x.Operation).IsRequired().HasMaxLength(100);
            builder.Property(x => x.Success).IsRequired();
            builder.Property(x => x.HttpStatusCode);
            builder.Property(x => x.RequestJson);
            builder.Property(x => x.ResponseJson);
            builder.Property(x => x.Error);
            builder.Property(x => x.DurationMs).IsRequired();

            builder.HasIndex(x => x.Operation).HasDatabaseName("IX_IntegrationLog_Operation");
        });
    }
}
