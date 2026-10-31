using System;
using System.Collections.Generic;
using System.Linq;

using Themis.Core;
using Themis.Types;

namespace Themis.Tests;

/// <summary>
/// A definition within every rule of <c>02</c>, of two policies, conditions of each case and a
/// deadline of each kind, and the ways of changing one of its members. Its person references
/// and roles are distinctive, so a test can find any of them in a log line.
/// </summary>
internal static class Definitions
{
    internal const string Approver = "person-approver-5c1e";

    internal const string StepFallback = "person-stepfallback-0b7d";

    internal const string Watcher = "person-watcher-9a42";

    internal const string TypeFallback = "person-typefallback-71f3";

    internal const string BuyerRole = "role-buyer-3d60";

    internal const string AuditorRole = "role-auditor-c28b";

    internal const string Region = "value-region-e604";

    private const int LongestReference = 128;

    /// <summary>The person references, roles and values the definition holds, none of which a log line may hold (<c>04</c> APR-OPS-005).</summary>
    internal static string[] Unloggable => [Approver, StepFallback, Watcher, TypeFallback, BuyerRole, AuditorRole, Region];

    internal static ApprovalTypeDefinition Valid { get; } = new()
    {
        Key = "purchase.order",
        ProtectedFields =
        [
            new ProtectedField { Name = "amount", Kind = FieldKind.DecimalNumber },
            new ProtectedField { Name = "count", Kind = FieldKind.WholeNumber },
            new ProtectedField { Name = "region", Kind = FieldKind.Text },
            new ProtectedField { Name = "urgent", Kind = FieldKind.Flag },
        ],
        Policies =
        [
            new PolicyDefinition
            {
                Name = "large",
                StartCondition = new Condition.AllOf(
                [
                    new Condition.Compare("amount", CompareOperator.Greater, new FieldValue.DecimalNumber(1000m)),
                    new Condition.Negate(new Condition.RequesterHas(BuyerRole)),
                ]),
                Steps =
                [
                    new StepDefinition
                    {
                        Name = "review",
                        Approvers = [new ApproverReference.Permission("orders.approve"), new ApproverReference.Person(Approver)],
                        Mode = ApprovalMode.AnyOne,
                        Condition = new Condition.OneOf("region", [new FieldValue.PlainText(Region)]),
                        Fallback = StepFallback,
                        Deadline = new Deadline { After = TimeSpan.FromHours(1), Action = DeadlineAction.Escalate, EscalateTo = new ApproverReference.Permission("orders.escalate") },
                    },
                    new StepDefinition
                    {
                        Name = "finance",
                        Approvers = [new ApproverReference.Person(Approver)],
                        Mode = ApprovalMode.All,
                        Condition = new Condition.AnyOf([new Condition.SubjectHas(AuditorRole), new Condition.Compare("urgent", CompareOperator.Equal, new FieldValue.Flag(true))]),
                        Fallback = StepFallback,
                        Deadline = new Deadline { After = TimeSpan.FromDays(2), Action = DeadlineAction.Reject },
                    },
                ],
            },
            new PolicyDefinition
            {
                Name = "small",
                Steps =
                [
                    new StepDefinition
                    {
                        Name = "lead",
                        Approvers = [new ApproverReference.Permission("orders.lead")],
                        Mode = ApprovalMode.AnyOne,
                        Condition = new Condition.Compare("count", CompareOperator.Less, new FieldValue.WholeNumber(10)),
                        Fallback = StepFallback,
                    },
                ],
            },
        ],
        Duplicates = DuplicateRule.Skip,
        Watchers = [new ApproverReference.Person(Watcher)],
        Fallback = TypeFallback,
    };

    /// <summary>
    /// <see cref="Valid"/> with policies added whose steps' approvers are persons of long
    /// references, so its canonical form is exactly <paramref name="bytes"/> bytes, each member
    /// within its rule.
    /// </summary>
    internal static ApprovalTypeDefinition OfSize(int bytes)
    {
        List<int> lengths = [];
        ApprovalTypeDefinition sized = Valid;
        while (CanonicalForm.Write(sized).Length <= bytes)
        {
            lengths.Add(LongestReference);
            sized = Padded(lengths);
        }

        for (int last = lengths.Count - 1; CanonicalForm.Write(sized).Length > bytes; last--)
        {
            lengths[last] -= Math.Min(CanonicalForm.Write(sized).Length - bytes, LongestReference - 1);
            sized = Padded(lengths);
        }

        return sized;
    }

    /// <summary><see cref="Valid"/> with its key <paramref name="key"/>.</summary>
    internal static ApprovalTypeDefinition Keyed(string key)
    {
        return Valid with { Key = key };
    }

    /// <summary><paramref name="definition"/> with the policy at <paramref name="policy"/> changed by <paramref name="change"/>.</summary>
    internal static ApprovalTypeDefinition WithPolicy(this ApprovalTypeDefinition definition, int policy, Func<PolicyDefinition, PolicyDefinition> change)
    {
        PolicyDefinition[] policies = [.. definition.Policies];
        policies[policy] = change(policies[policy]);
        return definition with { Policies = policies };
    }

    /// <summary><paramref name="definition"/> with the step at <paramref name="step"/> of the policy at <paramref name="policy"/> changed by <paramref name="change"/>.</summary>
    internal static ApprovalTypeDefinition WithStep(this ApprovalTypeDefinition definition, int policy, int step, Func<StepDefinition, StepDefinition> change)
    {
        return definition.WithPolicy(policy, changed =>
        {
            StepDefinition[] steps = [.. changed.Steps];
            steps[step] = change(steps[step]);
            return changed with { Steps = steps };
        });
    }

    /// <summary><see cref="Valid"/> with an approver of each length of <paramref name="lengths"/>, 16 to a step and 10 steps to an added policy.</summary>
    private static ApprovalTypeDefinition Padded(List<int> lengths)
    {
        List<PolicyDefinition> policies = [.. Valid.Policies];
        IEnumerable<int[]> steps = lengths.Chunk(16);
        int policy = 0;
        foreach (int[][] padding in steps.Chunk(10))
        {
            policies.Add(new PolicyDefinition
            {
                Name = $"padding{policy++}",
                Steps =
                [
                    .. padding.Select((approvers, step) => new StepDefinition
                    {
                        Name = $"step{step}",
                        Approvers = [.. approvers.Select(length => new ApproverReference.Person(new string('r', length)))],
                        Mode = ApprovalMode.AnyOne,
                        Fallback = StepFallback,
                    }),
                ],
            });
        }

        return Valid with { Policies = policies };
    }

    /// <summary><paramref name="definition"/> with the first policy's start condition <paramref name="condition"/>.</summary>
    internal static ApprovalTypeDefinition WithStartCondition(this ApprovalTypeDefinition definition, Condition? condition)
    {
        return definition.WithPolicy(0, policy => policy with { StartCondition = condition });
    }
}