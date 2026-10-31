using System.Globalization;
using System.Text;

namespace Themis.Core;

/// <summary>
/// A person's decision on a request (<c>01</c> APR-REQ-004). Formatting it writes neither its
/// persons nor its reason.
/// </summary>
public sealed record DecisionRequest
{
    /// <summary>The request.</summary>
    public required RequestId Request { get; init; }

    /// <summary>The person reference of the person deciding.</summary>
    public required string Decider { get; init; }

    /// <summary>The person whose assignment the decider holds through a delegation, or none.</summary>
    public string? ActingFor { get; init; }

    /// <summary>The outcome.</summary>
    public required DecisionOutcome Outcome { get; init; }

    /// <summary>The reason, at most 2,000 characters: optional for an approval, required for a rejection.</summary>
    public string? Reason { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Request = {Request}, Outcome = {Outcome}");
        return true;
    }
}