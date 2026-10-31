using System.Text;

namespace Themis.Core;

/// <summary>
/// Whether a person may approve a step's assignment (<c>01</c> APR-REQ-004). It prints only its
/// query, which prints no person, target or value.
/// </summary>
public sealed record ApproverCheck
{
    /// <summary>The person who decides.</summary>
    public required string Person { get; init; }

    /// <summary>
    /// The reference that named the assignment's person: the first of the step's approver
    /// references, in order, that named them; the step's or the type's fallback for a fallback
    /// assignment; the deadline's <c>EscalateTo</c> for an escalation one.
    /// </summary>
    public required ApproverReference Via { get; init; }

    /// <summary>The lender, where <see cref="Person"/> decides through a delegation; otherwise none.</summary>
    public string? ActingFor { get; init; }

    /// <summary>The request the check is for.</summary>
    public required ApproverQuery Query { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Query = ").Append(Query);
        return true;
    }
}