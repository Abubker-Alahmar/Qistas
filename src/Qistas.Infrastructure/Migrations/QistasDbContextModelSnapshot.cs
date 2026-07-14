// HAND-AUTHORED SNAPSHOT: see 20250101000000_InitialCreate.cs header comment for full
// context and the exact commands to run to replace this with a real dotnet-ef-generated
// file. EF Core requires this snapshot to sit alongside migrations so `dotnet ef` can
// compute future diffs correctly -- if this drifts from QistasDbContext.OnModelCreating,
// EF will report a pending model change the next time `dotnet ef migrations add` runs.
// Regenerate on a machine with the SDK to get a byte-for-byte-correct version.

using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Qistas.Infrastructure.Persistence;

#nullable disable

namespace Qistas.Infrastructure.Migrations
{
    [DbContext(typeof(QistasDbContext))]
    partial class QistasDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.10");

            modelBuilder.Entity("Qistas.Application.Outbox.OutboxMessage", b =>
            {
                b.Property<long>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("bigint");

                b.Property<int>("Attempts")
                    .HasColumnType("int")
                    .HasDefaultValue(0);

                b.Property<DateTime>("CreatedUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("Environment")
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasColumnType("nvarchar(50)");

                b.Property<string>("LastError")
                    .HasColumnType("nvarchar(max)");

                b.Property<string>("LastResponseJson")
                    .HasColumnType("nvarchar(max)");

                b.Property<string>("Operation")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("nvarchar(100)");

                b.Property<string>("PayloadJson")
                    .IsRequired()
                    .HasColumnType("nvarchar(max)");

                b.Property<string>("ScaleSystemReferenceId")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<string>("Status")
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasColumnType("nvarchar(50)");

                b.Property<DateTime>("UpdatedUtc")
                    .HasColumnType("datetime2");

                b.HasKey("Id");

                b.HasIndex("ScaleSystemReferenceId")
                    .HasDatabaseName("IX_Outbox_ScaleSystemReferenceId");

                b.HasIndex("Status")
                    .HasDatabaseName("IX_Outbox_Status");

                b.ToTable("Outbox", (string)null);
            });

            modelBuilder.Entity("Qistas.Application.Logging.IntegrationLogEntry", b =>
            {
                b.Property<long>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("bigint");

                b.Property<long>("DurationMs")
                    .HasColumnType("bigint");

                b.Property<string>("Environment")
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasColumnType("nvarchar(50)");

                b.Property<string>("Error")
                    .HasColumnType("nvarchar(max)");

                b.Property<int?>("HttpStatusCode")
                    .HasColumnType("int");

                b.Property<string>("Operation")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("nvarchar(100)");

                b.Property<string>("RequestJson")
                    .HasColumnType("nvarchar(max)");

                b.Property<string>("ResponseJson")
                    .HasColumnType("nvarchar(max)");

                b.Property<bool>("Success")
                    .HasColumnType("bit");

                b.Property<DateTime>("TimestampUtc")
                    .HasColumnType("datetime2");

                b.HasKey("Id");

                b.HasIndex("Operation")
                    .HasDatabaseName("IX_IntegrationLog_Operation");

                b.ToTable("IntegrationLog", (string)null);
            });
        }
    }
}
