using System;
using System.Globalization;
using System.Text;

namespace Themis.Core;

/// <summary>A decision of a request's record (<c>01</c> APR-REQ-013). Formatting it writes neither its persons nor its reason.</summary>
public sealed record DecisionRecord
{
    /// <summary>The decision's identifier.</summary>
    public required DecisionId Decision { get; init; }

    /// <summary>The number of the step decided.</summary>
    public required int Step { get; init; }

    /// <summary>The person who decided.</summary>
    public required string Decider { get; init; }

    /// <summary>The person the decider acted for through a delegation, or none.</summary>
    public string? ActingFor { get; init; }

    /// <summary>The outcome.</summary>
    public required DecisionOutcome Outcome { get; init; }

    /// <summary>The reason, or none.</summary>
    public string? Reason { get; init; }

    /// <summary>When it was recorded.</summary>
    public required DateTimeOffset At { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Decision = {Decision}, Step = {Step}, Outcome = {Outcome}, At = {At:O}");
        return true;
    }
}