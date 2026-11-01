using System;
using System.Collections.Generic;

namespace Themis.Core;

/// <summary>
/// What a step does once it is undecided for a time: it escalates, rejects or cancels, once,
/// and never approves (<c>02</c> APR-TYPE-004; <c>01</c> APR-REQ-009).
/// </summary>
public sealed record Deadline
{
    /// <summary>The time from the step's start, 1 minute to 365 days in whole seconds.</summary>
    public required TimeSpan After { get; init; }

    /// <summary>What the deadline does.</summary>
    public required DeadlineAction Action { get; init; }

    /// <summary>Who the step escalates to, required for <see cref="DeadlineAction.Escalate"/> alone; otherwise none.</summary>
    public ApproverReference? EscalateTo { get; init; }

    /// <summary>Up to three durations before the deadline at which the step's open assignees are reminded, each at least an hour in whole seconds, shorter than <see cref="After"/> and distinct; empty where none is given.</summary>
    public IReadOnlyList<TimeSpan> RemindBefore { get; init; } = [];
}