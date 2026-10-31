using System.Globalization;
using System.Text;

namespace Themis.Core;

/// <summary>An assignment of a step (<c>01</c> APR-REQ-013). Formatting it writes no person.</summary>
public sealed record AssignmentRecord
{
    /// <summary>The person assigned.</summary>
    public required string Person { get; init; }

    /// <summary>Why the person is assigned.</summary>
    public required AssignmentOrigin Origin { get; init; }

    /// <summary>The decision that satisfied it: its own, or under <see cref="DuplicateRule.Skip"/> an earlier step's; none while undecided.</summary>
    public DecisionId? Decision { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Origin = {Origin}, Decision = {Decision}");
        return true;
    }
}