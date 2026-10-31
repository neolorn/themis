namespace Themis.Core;

/// <summary>An assignment of a person's export (<c>01</c> APR-REQ-015). It names no person; the export is the person's.</summary>
public sealed record AssignmentExport
{
    /// <summary>The request.</summary>
    public required RequestId Request { get; init; }

    /// <summary>The number of the step.</summary>
    public required int Step { get; init; }

    /// <summary>Whether it named an approver, a fallback or an escalation assignee.</summary>
    public required AssignmentOrigin Origin { get; init; }

    /// <summary>The decision that satisfied it, or none while undecided.</summary>
    public DecisionId? Decision { get; init; }
}