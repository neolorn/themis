using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Themis.Core;

/// <summary>A recorded delegation (<c>01</c> APR-REQ-008). Formatting it writes none of its persons.</summary>
public sealed record DelegationRecord
{
    /// <summary>The delegation's identifier.</summary>
    public required DelegationId Delegation { get; init; }

    /// <summary>The person lending.</summary>
    public required string From { get; init; }

    /// <summary>The delegate.</summary>
    public required string To { get; init; }

    /// <summary>When it starts.</summary>
    public required DateTimeOffset Starts { get; init; }

    /// <summary>When it ends, unless ended earlier.</summary>
    public required DateTimeOffset Ends { get; init; }

    /// <summary>The keys of the types it covers; none covers every type.</summary>
    public required IReadOnlyList<string> Types { get; init; }

    /// <summary>The person who recorded it.</summary>
    public required string CreatedBy { get; init; }

    /// <summary>When it was recorded.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>When it was ended before <see cref="Ends"/>, or none.</summary>
    public DateTimeOffset? EndedAt { get; init; }

    /// <summary>The person who ended it, or none.</summary>
    public string? EndedBy { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Delegation = {Delegation}, Starts = {Starts:O}, Ends = {Ends:O}, Types = {Types.Count}, CreatedAt = {CreatedAt:O}, EndedAt = {EndedAt:O}");
        return true;
    }
}