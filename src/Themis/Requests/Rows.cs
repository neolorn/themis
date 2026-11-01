using System;
using System.Collections.Generic;

using Themis.Core;

namespace Themis.Requests;

/// <summary>
/// A row of the request table (<c>01</c> APR-REQ-001). Only the values that record a later move
/// change: its status, current step, approval and closing instants, closer, the person acted
/// for, its reason and the last sequence number of its events (<c>01</c> APR-REQ-015).
/// </summary>
internal sealed class RequestRow
{
    public required Guid Id { get; set; }

    public required string Type { get; set; }

    public required int Version { get; set; }

    /// <summary>The policy taken, or none for the type's fallback step or an approval at once.</summary>
    public string? Policy { get; set; }

    public required string TargetKind { get; set; }

    public required string TargetId { get; set; }

    /// <summary>The snapshot, as <see cref="Types.CanonicalForm.WriteSnapshot"/> writes it.</summary>
    public required string Snapshot { get; set; }

    public required string Requester { get; set; }

    public string? Subject { get; set; }

    public required List<string> RequesterRoles { get; set; }

    public required List<string> SubjectRoles { get; set; }

    public Guid? Previous { get; set; }

    public required RequestStatus Status { get; set; }

    /// <summary>The number of the current step, or none where no step is current.</summary>
    public int? Step { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public string? ClosedBy { get; set; }

    public string? ClosedFor { get; set; }

    public string? Reason { get; set; }

    public ReasonCode? ReasonCode { get; set; }

    public string? ChangedField { get; set; }

    /// <summary>The sequence number of the request's last event.</summary>
    public long Sequence { get; set; }
}

/// <summary>A row of the step table: one step of a request's policy, from 1 in the policy's order.</summary>
internal sealed class StepRow
{
    public required Guid Request { get; set; }

    public required int Number { get; set; }

    public required string Name { get; set; }

    public required ApprovalMode Mode { get; set; }

    /// <summary>How many approvals complete the step where its mode is <c>AtLeast</c>, or none.</summary>
    public int? Required { get; set; }

    public required StepStatus Status { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? DueAt { get; set; }

    public DateTimeOffset? DeadlineRanAt { get; set; }

    /// <summary>How many of the deadline's reminders, earliest first, have been sent or passed (<c>01</c> APR-REQ-009).</summary>
    public int RemindersSent { get; set; }

    /// <summary>When the next reminder falls due, or none where no reminder is left.</summary>
    public DateTimeOffset? RemindAt { get; set; }
}

/// <summary>A row of the assignment table: a person fixed to a step when it started (<c>01</c> APR-REQ-003).</summary>
internal sealed class AssignmentRow
{
    public required Guid Request { get; set; }

    public required int Step { get; set; }

    public required string Person { get; set; }

    public required AssignmentOrigin Origin { get; set; }

    /// <summary>The approver reference that named the person, as <see cref="Types.CanonicalForm.WriteApprover"/> writes it, which the directory's check is asked through.</summary>
    public required string Via { get; set; }

    /// <summary>The decision that satisfied it, or none while undecided.</summary>
    public Guid? Decision { get; set; }
}

/// <summary>A row of the decision table, written once and never changed (<c>01</c> APR-REQ-004).</summary>
internal sealed class DecisionRow
{
    public required Guid Id { get; set; }

    public required Guid Request { get; set; }

    public required int Step { get; set; }

    public required string Decider { get; set; }

    public string? ActingFor { get; set; }

    public required DecisionOutcome Outcome { get; set; }

    public string? Reason { get; set; }

    public required DateTimeOffset At { get; set; }
}

/// <summary>A row of the event table, written by the move it records and marked once taken (<c>01</c> APR-REQ-014).</summary>
internal sealed class EventRow
{
    public required Guid Id { get; set; }

    public Guid? Request { get; set; }

    public required long Sequence { get; set; }

    public required EventKind Kind { get; set; }

    public required DateTimeOffset At { get; set; }

    public string? Type { get; set; }

    public int? Version { get; set; }

    public string? TargetKind { get; set; }

    public string? TargetId { get; set; }

    public int? Step { get; set; }

    public string? Actor { get; set; }

    public string? ActingFor { get; set; }

    public ReasonCode? ReasonCode { get; set; }

    public Guid? Delegation { get; set; }

    public required List<string> Notify { get; set; }

    /// <summary>When the observer took it, or none while undelivered.</summary>
    public DateTimeOffset? TakenAt { get; set; }
}

/// <summary>A row of the delegation table: a lending of authority, ended by recording its actor and instant, never deleted (<c>01</c> APR-REQ-008).</summary>
internal sealed class DelegationRow
{
    public required Guid Id { get; set; }

    public required string From { get; set; }

    public required string To { get; set; }

    public required DateTimeOffset Starts { get; set; }

    public required DateTimeOffset Ends { get; set; }

    /// <summary>The keys of the types it covers; none covers every type.</summary>
    public required List<string> Types { get; set; }

    public required string CreatedBy { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public string? EndedBy { get; set; }
}

/// <summary>A row of the job table: a job's claim and its runs (<c>04</c> APR-OPS-004).</summary>
internal sealed class JobRow
{
    public required ApprovalJob Name { get; set; }

    public string? Holder { get; set; }

    public DateTimeOffset? ClaimExpiresAt { get; set; }

    public DateTimeOffset? LastStartedAt { get; set; }

    public DateTimeOffset? LastRenewedAt { get; set; }

    public DateTimeOffset? LastCompletedAt { get; set; }
}