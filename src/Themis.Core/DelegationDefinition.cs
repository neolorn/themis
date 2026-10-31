using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Themis.Core;

/// <summary>
/// A delegation to record: a person lending their authority to decide to another for a period
/// and for some or all approval types (<c>01</c> APR-REQ-008). Formatting it writes none of its
/// persons.
/// </summary>
public sealed record DelegationDefinition
{
    /// <summary>The person lending.</summary>
    public required string From { get; init; }

    /// <summary>The delegate.</summary>
    public required string To { get; init; }

    /// <summary>When it starts.</summary>
    public required DateTimeOffset Starts { get; init; }

    /// <summary>When it ends, after <see cref="Starts"/> and after now, at most 365 days after <see cref="Starts"/>.</summary>
    public required DateTimeOffset Ends { get; init; }

    /// <summary>The keys of the types it covers, 0 to 64; none covers every type.</summary>
    public IReadOnlyList<string> Types { get; init; } = [];

    /// <summary>The person the host says recorded it.</summary>
    public required string CreatedBy { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Starts = {Starts:O}, Ends = {Ends:O}, Types = {Types.Count}");
        return true;
    }
}