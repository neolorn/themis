using System.Collections.Generic;

namespace Themis.Core;

/// <summary>
/// One of a definition's alternative rule sets: a request takes the first policy whose start
/// condition holds and which has a step whose condition holds (<c>02</c> APR-TYPE-003).
/// </summary>
public sealed record PolicyDefinition
{
    /// <summary>The policy's name (<c>00</c> section 1.5).</summary>
    public required string Name { get; init; }

    /// <summary>The condition on which the policy holds, or none, where it always holds.</summary>
    public Condition? StartCondition { get; init; }

    /// <summary>The policy's steps, 1 to 10, no two named alike, in the order a request goes through them.</summary>
    public IReadOnlyList<StepDefinition> Steps { get; init; } = [];
}