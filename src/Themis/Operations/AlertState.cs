using System;
using System.Collections.Generic;

namespace Themis.Operations;

/// <summary>
/// A row of the alert table: one condition's state in one scope, so the deployment raises it
/// once however many processes see it (<c>04</c> APR-OPS-006).
/// </summary>
internal sealed class AlertState
{
    /// <summary>The conditions of <c>06</c> section 5.</summary>
    internal static IReadOnlyList<string> Conditions { get; } = ["observer-failing", "request-unassignable", "job-stalled"];

    public required string Condition { get; set; }

    /// <summary>The scope, empty for a condition with none.</summary>
    public required string Scope { get; set; }

    public bool Holding { get; set; }

    public DateTimeOffset? LastRaisedAt { get; set; }

    /// <summary>For <c>request-unassignable</c>, the instant of its last occurrence.</summary>
    public DateTimeOffset? LastOccurredAt { get; set; }

    /// <summary>For <c>request-unassignable</c>, the last occurrence's request.</summary>
    public Guid? Request { get; set; }

    /// <summary>For <c>request-unassignable</c>, the last occurrence's step number.</summary>
    public int? Step { get; set; }
}