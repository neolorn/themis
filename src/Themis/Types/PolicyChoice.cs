using System;
using System.Collections.Generic;
using System.Linq;

using Themis.Core;

namespace Themis.Types;

/// <summary>
/// Chooses a request's policy (<c>02</c> APR-TYPE-003): the first of its version's policies, in
/// order, whose start condition holds and which has a step whose condition holds, so a policy
/// never approves by its conditions alone. Where none does, the request takes the fallback step
/// or, where the type allows it, is approved at once.
/// </summary>
internal static class PolicyChoice
{
    /// <summary>The name of the fallback step.</summary>
    internal const string FallbackStepName = "fallback";

    /// <summary>
    /// The policy a request of <paramref name="definition"/> takes, on its snapshot
    /// <paramref name="snapshot"/>, its requester's roles <paramref name="requesterRoles"/> and its
    /// subject's roles <paramref name="subjectRoles"/>, none where it has no subject; none where
    /// no policy holds.
    /// </summary>
    internal static PolicyDefinition? Choose(ApprovalTypeDefinition definition, IReadOnlyDictionary<string, FieldValue> snapshot, IReadOnlyList<string> requesterRoles, IReadOnlyList<string>? subjectRoles)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.Policies.FirstOrDefault(policy =>
            (policy.StartCondition is null || ConditionCheck.Holds(policy.StartCondition, snapshot, requesterRoles, subjectRoles))
            && policy.Steps.Any(step => step.Condition is null || ConditionCheck.Holds(step.Condition, snapshot, requesterRoles, subjectRoles)));
    }

    /// <summary>
    /// The fallback step of <paramref name="definition"/>: one step named <c>fallback</c>, whose
    /// sole approver is the type's fallback person, <c>AnyOne</c>, with no condition, no deadline
    /// and that person as its fallback too.
    /// </summary>
    internal static StepDefinition FallbackStep(ApprovalTypeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new StepDefinition
        {
            Name = FallbackStepName,
            Approvers = [new ApproverReference.Person(definition.Fallback)],
            Mode = ApprovalMode.AnyOne,
            Fallback = definition.Fallback,
        };
    }
}