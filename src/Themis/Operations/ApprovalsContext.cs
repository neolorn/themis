using System.Collections.Generic;
using System.Linq;

using Microsoft.EntityFrameworkCore;

using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

using Themis.Types;

namespace Themis.Operations;

/// <summary>
/// The library's own context over its schema, <c>approvals</c>, which only its migrations create
/// and change (<c>04</c> APR-OPS-002). A column holding a closed set is <c>text</c> with a
/// <c>CHECK</c> constraint (<c>04</c> APR-OPS-003).
/// </summary>
internal sealed class ApprovalsContext(DbContextOptions<ApprovalsContext> options) : DbContext(options)
{
    /// <summary>The schema, fixed whatever the library's name (<c>06</c> section 7).</summary>
    internal const string Schema = "approvals";

    /// <summary>The migration history table, in the schema, so the library's history never meets a host's.</summary>
    internal const string HistoryTable = "__migrations_history";

    internal DbSet<AlertState> Alerts => Set<AlertState>();

    internal DbSet<TypeState> Types => Set<TypeState>();

    internal DbSet<StoredVersion> Versions => Set<StoredVersion>();

    /// <summary>A context on its own connection to <paramref name="connection"/>, as the migrations run.</summary>
    internal static ApprovalsContext Create(string connection)
    {
        return new ApprovalsContext(new DbContextOptionsBuilder<ApprovalsContext>().UseNpgsql(connection, Provider).Options);
    }

    /// <summary>The options of a context on the library's data source.</summary>
    internal static DbContextOptions<ApprovalsContext> Options(NpgsqlDataSource source)
    {
        return new DbContextOptionsBuilder<ApprovalsContext>().UseNpgsql(source, Provider).Options;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<AlertState>(alert =>
        {
            alert.ToTable("alerts", table =>
            {
                table.HasCheckConstraint("ck_alerts_condition", $"condition IN ({InList(AlertState.Conditions)})");
                table.HasCheckConstraint("ck_alerts_step", "step IS NULL OR step >= 1");
            });
            alert.HasKey(row => new { row.Condition, row.Scope }).HasName("pk_alerts");
            alert.Property(row => row.Condition).HasColumnName("condition");
            alert.Property(row => row.Scope).HasColumnName("scope");
            alert.Property(row => row.Holding).HasColumnName("holding");
            alert.Property(row => row.LastRaisedAt).HasColumnName("last_raised_at");
            alert.Property(row => row.LastOccurredAt).HasColumnName("last_occurred_at");
            alert.Property(row => row.Request).HasColumnName("request");
            alert.Property(row => row.Step).HasColumnName("step");
        });

        modelBuilder.Entity<TypeState>(type =>
        {
            type.ToTable("types");
            type.HasKey(row => row.Key).HasName("pk_types");
            type.Property(row => row.Key).HasColumnName("key");
            type.Property(row => row.Retired).HasColumnName("retired");
        });

        modelBuilder.Entity<StoredVersion>(version =>
        {
            version.ToTable("versions", table =>
            {
                table.HasCheckConstraint("ck_versions_version", "version >= 1");
                table.HasCheckConstraint("ck_versions_hash", "octet_length(hash) = 32");
            });
            version.HasKey(row => new { row.Key, row.Version }).HasName("pk_versions");
            version.HasOne<TypeState>().WithMany().HasForeignKey(row => row.Key).HasConstraintName("fk_versions_types").OnDelete(DeleteBehavior.Restrict);
            version.Property(row => row.Key).HasColumnName("key");
            version.Property(row => row.Version).HasColumnName("version").ValueGeneratedNever();
            version.Property(row => row.Definition).HasColumnName("definition").HasColumnType("text");
            version.Property(row => row.Hash).HasColumnName("hash");
            version.Property(row => row.PublishedAt).HasColumnName("published_at");
        });
    }

    private static void Provider(NpgsqlDbContextOptionsBuilder provider)
    {
        provider.MigrationsHistoryTable(HistoryTable, Schema);
    }

    private static string InList(IEnumerable<string> values)
    {
        return string.Join(", ", values.Select(value => $"'{value}'"));
    }
}