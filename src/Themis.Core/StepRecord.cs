using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Themis.Core;

/// <summary>A step of a request's record (<c>01</c> APR-REQ-013). Formatting it writes none of its assignments' persons.</summary>
public sealed record StepRecord
{
    /// <summary>The step's number, from 1, in the policy's order.</summary>
    public required int Number { get; init; }

    /// <summary>The step's name.</summary>
    public required string Name { get; init; }

    /// <summary>How many of its assignments must approve.</summary>
    public required ApprovalMode Mode { get; init; }

    /// <summary>Its status.</summary>
    public required StepStatus Status { get; init; }

    /// <summary>When it started, or none.</summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>When its deadline falls, or none.</summary>
    public DateTimeOffset? DueAt { get; init; }

    /// <summary>When its deadline's action ran, or none.</summary>
    public DateTimeOffset? DeadlineRanAt { get; init; }

    /// <summary>Its assignments.</summary>
    public IReadOnlyList<AssignmentRecord> Assignments { get; init; } = [];

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Number = {Number}, Name = {Name}, Mode = {Mode}, Status = {Status}, StartedAt = {StartedAt:O}, DueAt = {DueAt:O}, DeadlineRanAt = {DeadlineRanAt:O}");
        return true;
    }
}