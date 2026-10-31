using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Themis.Core;

/// <summary>What the record holds that names a person (<c>01</c> APR-REQ-015). Formatting it writes the number of each, and nothing they hold.</summary>
public sealed record PersonExport
{
    /// <summary>The requests the person made or is the subject of.</summary>
    public required IReadOnlyList<RequestRecord> Requests { get; init; }

    /// <summary>The person's assignments.</summary>
    public required IReadOnlyList<AssignmentExport> Assignments { get; init; }

    /// <summary>The decisions the person made or that were made for them.</summary>
    public required IReadOnlyList<DecisionExport> Decisions { get; init; }

    /// <summary>The delegations from and to the person.</summary>
    public required IReadOnlyList<DelegationRecord> Delegations { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Requests = {Requests.Count}, Assignments = {Assignments.Count}, Decisions = {Decisions.Count}, Delegations = {Delegations.Count}");
        return true;
    }
}