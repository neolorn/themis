using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

using Themis.Core;
using Themis.Requests;
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

    internal DbSet<RequestRow> Requests => Set<RequestRow>();

    internal DbSet<StepRow> Steps => Set<StepRow>();

    internal DbSet<AssignmentRow> Assignments => Set<AssignmentRow>();

    internal DbSet<DecisionRow> Decisions => Set<DecisionRow>();

    internal DbSet<DelegationRow> Delegations => Set<DelegationRow>();

    internal DbSet<EventRow> Events => Set<EventRow>();

    internal DbSet<JobRow> Jobs => Set<JobRow>();

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

    /// <summary>A context on the connection of a host's transaction, which the context never opens, closes or commits (<c>03</c> APR-LIB-008).</summary>
    internal static ApprovalsContext OnConnection(DbConnection connection)
    {
        return new ApprovalsContext(new DbContextOptionsBuilder<ApprovalsContext>().UseNpgsql(connection, Provider).Options);
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

        modelBuilder.Entity<RequestRow>(request =>
        {
            request.ToTable("requests", table =>
            {
                table.HasCheckConstraint("ck_requests_status", $"status IN ({InList(Spelling<RequestStatus>.All)})");
                table.HasCheckConstraint("ck_requests_reason_code", $"reason_code IS NULL OR reason_code IN ({InList(Spelling<ReasonCode>.All)})");
                table.HasCheckConstraint("ck_requests_step", "step IS NULL OR step >= 1");
            });
            request.HasKey(row => row.Id).HasName("pk_requests");
            request.HasOne<StoredVersion>().WithMany().HasForeignKey(row => new { row.Type, row.Version }).HasConstraintName("fk_requests_versions").OnDelete(DeleteBehavior.Restrict);
            request.HasOne<RequestRow>().WithMany().HasForeignKey(row => row.Previous).HasConstraintName("fk_requests_previous").OnDelete(DeleteBehavior.Restrict);
            request.HasIndex(row => new { row.Type, row.TargetKind, row.TargetId }).HasDatabaseName("ux_requests_open").IsUnique().HasFilter("status IN ('pending', 'approved')");
            request.HasIndex(row => new { row.TargetKind, row.TargetId }).HasDatabaseName("ix_requests_target");
            request.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
            request.Property(row => row.Type).HasColumnName("type");
            request.Property(row => row.Version).HasColumnName("version");
            request.Property(row => row.Policy).HasColumnName("policy");
            request.Property(row => row.TargetKind).HasColumnName("target_kind");
            request.Property(row => row.TargetId).HasColumnName("target_id");
            request.Property(row => row.Snapshot).HasColumnName("snapshot").HasColumnType("text");
            request.Property(row => row.Requester).HasColumnName("requester");
            request.Property(row => row.Subject).HasColumnName("subject");
            request.Property(row => row.RequesterRoles).HasColumnName("requester_roles");
            request.Property(row => row.SubjectRoles).HasColumnName("subject_roles");
            request.Property(row => row.Previous).HasColumnName("previous");
            request.Property(row => row.Status).HasColumnName("status").HasConversion(Spelt<RequestStatus>());
            request.Property(row => row.Step).HasColumnName("step");
            request.Property(row => row.CreatedAt).HasColumnName("created_at");
            request.Property(row => row.ApprovedAt).HasColumnName("approved_at");
            request.Property(row => row.ClosedAt).HasColumnName("closed_at");
            request.Property(row => row.ClosedBy).HasColumnName("closed_by");
            request.Property(row => row.ClosedFor).HasColumnName("closed_for");
            request.Property(row => row.Reason).HasColumnName("reason");
            request.Property(row => row.ReasonCode).HasColumnName("reason_code").HasConversion(Spelt<ReasonCode>());
            request.Property(row => row.ChangedField).HasColumnName("changed_field");
            request.Property(row => row.Sequence).HasColumnName("sequence");
        });

        modelBuilder.Entity<StepRow>(step =>
        {
            step.ToTable("steps", table =>
            {
                table.HasCheckConstraint("ck_steps_number", "number >= 1");
                table.HasCheckConstraint("ck_steps_mode", $"mode IN ({InList(Spelling<ApprovalMode>.All)})");
                table.HasCheckConstraint("ck_steps_status", $"status IN ({InList(Spelling<StepStatus>.All)})");
                table.HasCheckConstraint("ck_steps_required", "(mode = 'atleast' AND required BETWEEN 2 AND 16) OR (mode <> 'atleast' AND required IS NULL)");
            });
            step.HasKey(row => new { row.Request, row.Number }).HasName("pk_steps");
            step.HasOne<RequestRow>().WithMany().HasForeignKey(row => row.Request).HasConstraintName("fk_steps_requests").OnDelete(DeleteBehavior.Restrict);
            step.HasIndex(row => row.DueAt).HasDatabaseName("ix_steps_due").HasFilter("status = 'current' AND deadline_ran_at IS NULL");
            step.HasIndex(row => row.RemindAt).HasDatabaseName("ix_steps_remind").HasFilter("status = 'current' AND deadline_ran_at IS NULL AND remind_at IS NOT NULL");
            step.Property(row => row.Request).HasColumnName("request");
            step.Property(row => row.Number).HasColumnName("number").ValueGeneratedNever();
            step.Property(row => row.Name).HasColumnName("name");
            step.Property(row => row.Mode).HasColumnName("mode").HasConversion(Spelt<ApprovalMode>());
            step.Property(row => row.Required).HasColumnName("required");
            step.Property(row => row.Status).HasColumnName("status").HasConversion(Spelt<StepStatus>());
            step.Property(row => row.StartedAt).HasColumnName("started_at");
            step.Property(row => row.DueAt).HasColumnName("due_at");
            step.Property(row => row.DeadlineRanAt).HasColumnName("deadline_ran_at");
            step.Property(row => row.RemindersSent).HasColumnName("reminders_sent");
            step.Property(row => row.RemindAt).HasColumnName("remind_at");
        });

        modelBuilder.Entity<AssignmentRow>(assignment =>
        {
            assignment.ToTable("assignments", table => table.HasCheckConstraint("ck_assignments_origin", $"origin IN ({InList(Spelling<AssignmentOrigin>.All)})"));
            assignment.HasKey(row => new { row.Request, row.Step, row.Person }).HasName("pk_assignments");
            assignment.HasOne<StepRow>().WithMany().HasForeignKey(row => new { row.Request, row.Step }).HasConstraintName("fk_assignments_steps").OnDelete(DeleteBehavior.Restrict);
            assignment.HasOne<DecisionRow>().WithMany().HasForeignKey(row => row.Decision).HasConstraintName("fk_assignments_decisions").OnDelete(DeleteBehavior.Restrict);
            assignment.HasIndex(row => row.Person).HasDatabaseName("ix_assignments_person").HasFilter("decision IS NULL");
            assignment.Property(row => row.Request).HasColumnName("request");
            assignment.Property(row => row.Step).HasColumnName("step");
            assignment.Property(row => row.Person).HasColumnName("person");
            assignment.Property(row => row.Origin).HasColumnName("origin").HasConversion(Spelt<AssignmentOrigin>());
            assignment.Property(row => row.Via).HasColumnName("via").HasColumnType("text");
            assignment.Property(row => row.Decision).HasColumnName("decision");
        });

        modelBuilder.Entity<DecisionRow>(decision =>
        {
            decision.ToTable("decisions", table => table.HasCheckConstraint("ck_decisions_outcome", $"outcome IN ({InList(Spelling<DecisionOutcome>.All)})"));
            decision.HasKey(row => row.Id).HasName("pk_decisions");
            decision.HasOne<StepRow>().WithMany().HasForeignKey(row => new { row.Request, row.Step }).HasConstraintName("fk_decisions_steps").OnDelete(DeleteBehavior.Restrict);
            decision.HasIndex(row => new { row.Request, row.Step, row.Decider }).HasDatabaseName("ux_decisions_decider").IsUnique();
            decision.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
            decision.Property(row => row.Request).HasColumnName("request");
            decision.Property(row => row.Step).HasColumnName("step");
            decision.Property(row => row.Decider).HasColumnName("decider");
            decision.Property(row => row.ActingFor).HasColumnName("acting_for");
            decision.Property(row => row.Outcome).HasColumnName("outcome").HasConversion(Spelt<DecisionOutcome>());
            decision.Property(row => row.Reason).HasColumnName("reason");
            decision.Property(row => row.At).HasColumnName("at");
        });

        modelBuilder.Entity<DelegationRow>(delegation =>
        {
            delegation.ToTable("delegations", table =>
            {
                table.HasCheckConstraint("ck_delegations_period", "ends > starts");
                table.HasCheckConstraint("ck_delegations_persons", "to_person <> from_person");
            });
            delegation.HasKey(row => row.Id).HasName("pk_delegations");
            delegation.HasIndex(row => row.From).HasDatabaseName("ix_delegations_from").HasFilter("ended_at IS NULL");
            delegation.HasIndex(row => row.To).HasDatabaseName("ix_delegations_to").HasFilter("ended_at IS NULL");
            delegation.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
            delegation.Property(row => row.From).HasColumnName("from_person");
            delegation.Property(row => row.To).HasColumnName("to_person");
            delegation.Property(row => row.Starts).HasColumnName("starts");
            delegation.Property(row => row.Ends).HasColumnName("ends");
            delegation.Property(row => row.Types).HasColumnName("types");
            delegation.Property(row => row.CreatedBy).HasColumnName("created_by");
            delegation.Property(row => row.CreatedAt).HasColumnName("created_at");
            delegation.Property(row => row.EndedAt).HasColumnName("ended_at");
            delegation.Property(row => row.EndedBy).HasColumnName("ended_by");
        });

        modelBuilder.Entity<EventRow>(approvalEvent =>
        {
            approvalEvent.ToTable("events", table =>
            {
                table.HasCheckConstraint("ck_events_kind", $"kind IN ({InList(Spelling<EventKind>.All)})");
                table.HasCheckConstraint("ck_events_reason_code", $"reason_code IS NULL OR reason_code IN ({InList(Spelling<ReasonCode>.All)})");
                table.HasCheckConstraint("ck_events_sequence", "sequence >= 1");
            });
            approvalEvent.HasKey(row => row.Id).HasName("pk_events");
            approvalEvent.HasOne<RequestRow>().WithMany().HasForeignKey(row => row.Request).HasConstraintName("fk_events_requests").OnDelete(DeleteBehavior.Restrict);
            approvalEvent.HasOne<DelegationRow>().WithMany().HasForeignKey(row => row.Delegation).HasConstraintName("fk_events_delegations").OnDelete(DeleteBehavior.Restrict);
            approvalEvent.HasIndex(row => new { row.Request, row.Sequence }).HasDatabaseName("ux_events_sequence").IsUnique();
            approvalEvent.HasIndex(row => row.At).HasDatabaseName("ix_events_untaken").HasFilter("taken_at IS NULL");
            approvalEvent.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
            approvalEvent.Property(row => row.Request).HasColumnName("request");
            approvalEvent.Property(row => row.Sequence).HasColumnName("sequence");
            approvalEvent.Property(row => row.Kind).HasColumnName("kind").HasConversion(Spelt<EventKind>());
            approvalEvent.Property(row => row.At).HasColumnName("at");
            approvalEvent.Property(row => row.Type).HasColumnName("type");
            approvalEvent.Property(row => row.Version).HasColumnName("version");
            approvalEvent.Property(row => row.TargetKind).HasColumnName("target_kind");
            approvalEvent.Property(row => row.TargetId).HasColumnName("target_id");
            approvalEvent.Property(row => row.Step).HasColumnName("step");
            approvalEvent.Property(row => row.Actor).HasColumnName("actor");
            approvalEvent.Property(row => row.ActingFor).HasColumnName("acting_for");
            approvalEvent.Property(row => row.ReasonCode).HasColumnName("reason_code").HasConversion(Spelt<ReasonCode>());
            approvalEvent.Property(row => row.Delegation).HasColumnName("delegation");
            approvalEvent.Property(row => row.Notify).HasColumnName("notify");
            approvalEvent.Property(row => row.TakenAt).HasColumnName("taken_at");
        });

        modelBuilder.Entity<JobRow>(job =>
        {
            job.ToTable("jobs", table => table.HasCheckConstraint("ck_jobs_name", $"name IN ({InList(Spelling<ApprovalJob>.All)})"));
            job.HasKey(row => row.Name).HasName("pk_jobs");
            job.Property(row => row.Name).HasColumnName("name").HasConversion(Spelt<ApprovalJob>());
            job.Property(row => row.Holder).HasColumnName("holder");
            job.Property(row => row.ClaimExpiresAt).HasColumnName("claim_expires_at");
            job.Property(row => row.LastStartedAt).HasColumnName("last_started_at");
            job.Property(row => row.LastRenewedAt).HasColumnName("last_renewed_at");
            job.Property(row => row.LastCompletedAt).HasColumnName("last_completed_at");
            job.HasData(Enum.GetValues<ApprovalJob>().Select(name => new JobRow { Name = name }));
        });
    }

    private static void Provider(NpgsqlDbContextOptionsBuilder provider)
    {
        provider.MigrationsHistoryTable(HistoryTable, Schema);
    }

    /// <summary>Writes a member of <typeparamref name="TEnum"/> as its spelling, so a column holding it is text with a <c>CHECK</c> constraint.</summary>
    private static ValueConverter<TEnum, string> Spelt<TEnum>()
        where TEnum : struct, Enum
    {
        return new ValueConverter<TEnum, string>(member => Spelling<TEnum>.Write(member), spelling => Spelling<TEnum>.Read(spelling));
    }

    private static string InList(IEnumerable<string> values)
    {
        return string.Join(", ", values.Select(value => $"'{value}'"));
    }
}