using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Themis.Core;

/// <summary>
/// One stage of a policy: who may approve it, how many must, a fallback approver, and an
/// optional condition and deadline (<c>02</c> APR-TYPE-004). It never prints its fallback.
/// </summary>
public sealed record StepDefinition
{
    /// <summary>The step's name (<c>00</c> section 1.5).</summary>
    public required string Name { get; init; }

    /// <summary>Who may approve the step, 1 to 16 approver references.</summary>
    public IReadOnlyList<ApproverReference> Approvers { get; init; } = [];

    /// <summary>How many of the step's assignments must approve.</summary>
    public required ApprovalMode Mode { get; init; }

    /// <summary>The condition on which the step is taken, or none; a step whose condition is false is skipped.</summary>
    public Condition? Condition { get; init; }

    /// <summary>The approver where the step's exclusions leave no one, a person reference (<c>01</c> APR-REQ-007).</summary>
    public required string Fallback { get; init; }

    /// <summary>What happens once the step is undecided for a time, or none.</summary>
    public Deadline? Deadline { get; init; }

    /// <summary>How many approvals, counted per distinct person, complete an <c>AtLeast</c> step, 2 to 16; none for any other mode.</summary>
    public int? Required { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Name = {Name}, Mode = {Mode}, Condition = {Condition}, Deadline = {Deadline}, Required = {Required}");
        return true;
    }
}