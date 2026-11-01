using System;
using System.Collections.Generic;
using System.Linq;

using Themis.Core;

namespace Themis.Tests;

/// <summary>
/// Definitions each outside one rule of <c>02</c>, by the rule they break, each with the path of
/// the first member outside its rule: the type's rules (APR-TYPE-002), a step's (APR-TYPE-004)
/// and a condition's (APR-TYPE-005).
/// </summary>
internal static class InvalidDefinitions
{
    private static readonly string LoneSurrogate = "person-" + (char)0xD800;

    private static readonly string ControlCharacter = "person-" + (char)7;

    /// <summary>A definition outside a rule of <c>02</c> APR-TYPE-002, by the rule.</summary>
    internal static IReadOnlyDictionary<string, (ApprovalTypeDefinition Definition, string Member)> TypeRules { get; } = new Dictionary<string, (ApprovalTypeDefinition, string)>(StringComparer.Ordinal)
    {
        ["key upper case"] = (Definitions.Keyed("Purchase"), "key"),
        ["key empty"] = (Definitions.Keyed(string.Empty), "key"),
        ["key none"] = (Definitions.Keyed(null!), "key"),
        ["key starting with a digit"] = (Definitions.Keyed("1purchase"), "key"),
        ["key with two separators in a row"] = (Definitions.Keyed("purchase..order"), "key"),
        ["key ending with a separator"] = (Definitions.Keyed("purchase-"), "key"),
        ["key with a space"] = (Definitions.Keyed("purchase order"), "key"),
        ["key of 65 characters"] = (Definitions.Keyed(new string('k', 65)), "key"),
        ["protected fields 33"] = (Definitions.Valid with { ProtectedFields = [.. Enumerable.Range(0, 33).Select(index => new ProtectedField { Name = $"field{index}", Kind = FieldKind.Text })] }, "protectedFields"),
        ["protected fields none"] = (Definitions.Valid with { ProtectedFields = null! }, "protectedFields"),
        ["protected field none"] = (Definitions.Valid with { ProtectedFields = [null!] }, "protectedFields[0]"),
        ["protected field not a name"] = (Definitions.Valid with { ProtectedFields = [new ProtectedField { Name = "Amount", Kind = FieldKind.DecimalNumber }] }, "protectedFields[0].name"),
        ["protected fields named alike"] = (Definitions.Valid with { ProtectedFields = [.. Definitions.Valid.ProtectedFields, new ProtectedField { Name = "count", Kind = FieldKind.Text }] }, "protectedFields[4].name"),
        ["protected field of no kind"] = (Definitions.Valid with { ProtectedFields = [new ProtectedField { Name = "amount", Kind = (FieldKind)4 }] }, "protectedFields[0].kind"),
        ["policies 0"] = (Definitions.Valid with { Policies = [] }, "policies"),
        ["policies 17"] = (Definitions.Valid with { Policies = [.. Enumerable.Range(0, 17).Select(index => Definitions.Valid.Policies[1] with { Name = $"policy{index}" })] }, "policies"),
        ["policies none"] = (Definitions.Valid with { Policies = null! }, "policies"),
        ["policy none"] = (Definitions.Valid with { Policies = [Definitions.Valid.Policies[0], null!] }, "policies[1]"),
        ["policy not a name"] = (Definitions.Valid.WithPolicy(1, policy => policy with { Name = "small one" }), "policies[1].name"),
        ["policies named alike"] = (Definitions.Valid.WithPolicy(1, policy => policy with { Name = "large" }), "policies[1].name"),
        ["steps 0"] = (Definitions.Valid.WithPolicy(1, policy => policy with { Steps = [] }), "policies[1].steps"),
        ["steps 11"] = (Definitions.Valid.WithPolicy(1, policy => policy with { Steps = [.. Enumerable.Range(0, 11).Select(index => policy.Steps[0] with { Name = $"step{index}" })] }), "policies[1].steps"),
        ["steps none"] = (Definitions.Valid.WithPolicy(1, policy => policy with { Steps = null! }), "policies[1].steps"),
        ["step none"] = (Definitions.Valid.WithPolicy(1, policy => policy with { Steps = [null!] }), "policies[1].steps[0]"),
        ["step not a name"] = (Definitions.Valid.WithStep(0, 1, step => step with { Name = "_finance" }), "policies[0].steps[1].name"),
        ["steps of one policy named alike"] = (Definitions.Valid.WithStep(0, 1, step => step with { Name = "review" }), "policies[0].steps[1].name"),
        ["duplicates of no rule"] = (Definitions.Valid with { Duplicates = (DuplicateRule)2 }, "duplicates"),
        ["watchers 17"] = (Definitions.Valid with { Watchers = [.. Enumerable.Range(0, 17).Select(index => new ApproverReference.Person($"person-{index}"))] }, "watchers"),
        ["watchers none"] = (Definitions.Valid with { Watchers = null! }, "watchers"),
        ["watcher none"] = (Definitions.Valid with { Watchers = [null!] }, "watchers[0]"),
        ["watcher not a person reference"] = (Definitions.Valid with { Watchers = [new ApproverReference.Person(string.Empty)] }, "watchers[0].reference"),
        ["watcher not a permission"] = (Definitions.Valid with { Watchers = [new ApproverReference.Permission("Orders")] }, "watchers[0].name"),
        ["fallback empty"] = (Definitions.Valid with { Fallback = string.Empty }, "fallback"),
        ["fallback none"] = (Definitions.Valid with { Fallback = null! }, "fallback"),
        ["fallback of 129 characters"] = (Definitions.Valid with { Fallback = new string('p', 129) }, "fallback"),
        ["fallback holding a control character"] = (Definitions.Valid with { Fallback = ControlCharacter }, "fallback"),
        ["fallback holding a lone surrogate"] = (Definitions.Valid with { Fallback = LoneSurrogate }, "fallback"),
        ["canonical form of 64 KiB and a byte"] = (Definitions.OfSize(65_537), string.Empty),
    };

    /// <summary>A definition outside a rule of a step, <c>02</c> APR-TYPE-004, by the rule.</summary>
    internal static IReadOnlyDictionary<string, (ApprovalTypeDefinition Definition, string Member)> StepRules { get; } = new Dictionary<string, (ApprovalTypeDefinition, string)>(StringComparer.Ordinal)
    {
        ["no approver"] = (Definitions.Valid.WithStep(0, 1, step => step with { Approvers = [] }), "policies[0].steps[1].approvers"),
        ["approvers none"] = (Definitions.Valid.WithStep(0, 1, step => step with { Approvers = null! }), "policies[0].steps[1].approvers"),
        ["approvers 17"] = (Definitions.Valid.WithStep(0, 1, step => step with { Approvers = [.. Enumerable.Range(0, 17).Select(index => new ApproverReference.Person($"person-{index}"))] }), "policies[0].steps[1].approvers"),
        ["approver none"] = (Definitions.Valid.WithStep(0, 0, step => step with { Approvers = [step.Approvers[0], null!] }), "policies[0].steps[0].approvers[1]"),
        ["approver not a permission"] = (Definitions.Valid.WithStep(0, 0, step => step with { Approvers = [new ApproverReference.Permission("orders approve")] }), "policies[0].steps[0].approvers[0].name"),
        ["approver not a person reference"] = (Definitions.Valid.WithStep(0, 0, step => step with { Approvers = [new ApproverReference.Person(new string('p', 129))] }), "policies[0].steps[0].approvers[0].reference"),
        ["mode of no rule"] = (Definitions.Valid.WithStep(1, 0, step => step with { Mode = (ApprovalMode)3 }), "policies[1].steps[0].mode"),
        ["fallback not a person reference"] = (Definitions.Valid.WithStep(1, 0, step => step with { Fallback = string.Empty }), "policies[1].steps[0].fallback"),
        ["fallback none"] = (Definitions.Valid.WithStep(1, 0, step => step with { Fallback = null! }), "policies[1].steps[0].fallback"),
        ["fallback holding a lone surrogate"] = (Definitions.Valid.WithStep(1, 0, step => step with { Fallback = LoneSurrogate }), "policies[1].steps[0].fallback"),
        ["deadline under a minute"] = (Definitions.Valid.WithStep(0, 1, step => step with { Deadline = step.Deadline! with { After = TimeSpan.FromSeconds(59) } }), "policies[0].steps[1].deadline.after"),
        ["deadline over 365 days"] = (Definitions.Valid.WithStep(0, 1, step => step with { Deadline = step.Deadline! with { After = TimeSpan.FromDays(365) + TimeSpan.FromSeconds(1) } }), "policies[0].steps[1].deadline.after"),
        ["deadline of no whole seconds"] = (Definitions.Valid.WithStep(0, 1, step => step with { Deadline = step.Deadline! with { After = TimeSpan.FromMilliseconds(90_500) } }), "policies[0].steps[1].deadline.after"),
        ["deadline of no action"] = (Definitions.Valid.WithStep(0, 1, step => step with { Deadline = step.Deadline! with { Action = (DeadlineAction)3 } }), "policies[0].steps[1].deadline.action"),
        ["escalate without a target"] = (Definitions.Valid.WithStep(0, 0, step => step with { Deadline = step.Deadline! with { EscalateTo = null } }), "policies[0].steps[0].deadline.escalateTo"),
        ["target without escalate"] = (Definitions.Valid.WithStep(0, 1, step => step with { Deadline = step.Deadline! with { EscalateTo = new ApproverReference.Permission("orders.escalate") } }), "policies[0].steps[1].deadline.escalateTo"),
        ["cancel with a target"] = (Definitions.Valid.WithStep(0, 1, step => step with { Deadline = new Deadline { After = TimeSpan.FromHours(1), Action = DeadlineAction.Cancel, EscalateTo = new ApproverReference.Person(Definitions.Approver) } }), "policies[0].steps[1].deadline.escalateTo"),
        ["target not a permission"] = (Definitions.Valid.WithStep(0, 0, step => step with { Deadline = step.Deadline! with { EscalateTo = new ApproverReference.Permission("-orders") } }), "policies[0].steps[0].deadline.escalateTo.name"),
        ["target not a person reference"] = (Definitions.Valid.WithStep(0, 0, step => step with { Deadline = step.Deadline! with { EscalateTo = new ApproverReference.Person(ControlCharacter) } }), "policies[0].steps[0].deadline.escalateTo.reference"),
    };

    /// <summary>A definition outside a rule of a step's <c>Required</c>, <c>02</c> APR-TYPE-004, by the rule.</summary>
    internal static IReadOnlyDictionary<string, (ApprovalTypeDefinition Definition, string Member)> RequiredRules { get; } = new Dictionary<string, (ApprovalTypeDefinition, string)>(StringComparer.Ordinal)
    {
        ["at least without required"] = (Definitions.Valid.WithStep(1, 0, step => step with { Mode = ApprovalMode.AtLeast }), "policies[1].steps[0].required"),
        ["required with any one"] = (Definitions.Valid.WithStep(1, 0, step => step with { Required = 2 }), "policies[1].steps[0].required"),
        ["required with all"] = (Definitions.Valid.WithStep(0, 1, step => step with { Required = 2 }), "policies[0].steps[1].required"),
        ["required of one"] = (Definitions.Valid.WithStep(1, 0, step => step with { Mode = ApprovalMode.AtLeast, Required = 1 }), "policies[1].steps[0].required"),
        ["required of none"] = (Definitions.Valid.WithStep(1, 0, step => step with { Mode = ApprovalMode.AtLeast, Required = 0 }), "policies[1].steps[0].required"),
        ["required of 17"] = (Definitions.Valid.WithStep(1, 0, step => step with { Mode = ApprovalMode.AtLeast, Required = 17 }), "policies[1].steps[0].required"),
        ["required above the distinct persons named"] = (Definitions.Valid.WithStep(0, 1, step => step with { Approvers = [new ApproverReference.Person(Definitions.Approver), new ApproverReference.Person(Definitions.StepFallback), new ApproverReference.Person(Definitions.Approver)], Mode = ApprovalMode.AtLeast, Required = 3 }), "policies[0].steps[1].required"),
    };

    /// <summary>A definition outside a rule of a deadline's <c>RemindBefore</c>, <c>02</c> APR-TYPE-004, by the rule.</summary>
    internal static IReadOnlyDictionary<string, (ApprovalTypeDefinition Definition, string Member)> ReminderRules { get; } = new Dictionary<string, (ApprovalTypeDefinition, string)>(StringComparer.Ordinal)
    {
        ["reminder under an hour"] = (Reminding(TimeSpan.FromHours(1) - TimeSpan.FromSeconds(1)), "policies[0].steps[1].deadline.remindBefore[0]"),
        ["reminder as long as the deadline"] = (Reminding(TimeSpan.FromHours(2), TimeSpan.FromDays(2)), "policies[0].steps[1].deadline.remindBefore[1]"),
        ["reminder of no whole seconds"] = (Reminding(TimeSpan.FromMilliseconds(3_600_500)), "policies[0].steps[1].deadline.remindBefore[0]"),
        ["a fourth reminder"] = (Reminding(TimeSpan.FromHours(1), TimeSpan.FromHours(2), TimeSpan.FromHours(3), TimeSpan.FromHours(4)), "policies[0].steps[1].deadline.remindBefore"),
        ["two reminders alike"] = (Reminding(TimeSpan.FromHours(3), TimeSpan.FromHours(3)), "policies[0].steps[1].deadline.remindBefore[1]"),
        ["reminders none"] = (Definitions.Valid.WithStep(0, 1, step => step with { Deadline = step.Deadline! with { RemindBefore = null! } }), "policies[0].steps[1].deadline.remindBefore"),
    };

    /// <summary>A definition outside a rule of a condition, <c>02</c> APR-TYPE-005, by the rule.</summary>
    internal static IReadOnlyDictionary<string, (ApprovalTypeDefinition Definition, string Member)> ConditionRules { get; } = new Dictionary<string, (ApprovalTypeDefinition, string)>(StringComparer.Ordinal)
    {
        ["field the type does not protect"] = (Definitions.Valid.WithStartCondition(new Condition.Compare("price", CompareOperator.Equal, new FieldValue.DecimalNumber(1m))), "policies[0].startCondition.field"),
        ["field none"] = (Definitions.Valid.WithStartCondition(new Condition.Compare(null!, CompareOperator.Equal, new FieldValue.DecimalNumber(1m))), "policies[0].startCondition.field"),
        ["text field compared with greater"] = (Definitions.Valid.WithStartCondition(new Condition.Compare("region", CompareOperator.Greater, new FieldValue.PlainText("north"))), "policies[0].startCondition.operator"),
        ["flag field compared with less or equal"] = (Definitions.Valid.WithStartCondition(new Condition.Compare("urgent", CompareOperator.LessOrEqual, new FieldValue.Flag(true))), "policies[0].startCondition.operator"),
        ["operator of no rule"] = (Definitions.Valid.WithStartCondition(new Condition.Compare("count", (CompareOperator)6, new FieldValue.WholeNumber(1))), "policies[0].startCondition.operator"),
        ["value not of the field's kind"] = (Definitions.Valid.WithStartCondition(new Condition.Compare("amount", CompareOperator.Equal, new FieldValue.PlainText("1000"))), "policies[0].startCondition.value"),
        ["flag compared with a number"] = (Definitions.Valid.WithStartCondition(new Condition.Compare("urgent", CompareOperator.Equal, new FieldValue.WholeNumber(1))), "policies[0].startCondition.value"),
        ["value none"] = (Definitions.Valid.WithStartCondition(new Condition.Compare("amount", CompareOperator.Equal, null!)), "policies[0].startCondition.value"),
        ["text value holding a lone surrogate"] = (Definitions.Valid.WithStartCondition(new Condition.Compare("region", CompareOperator.Equal, new FieldValue.PlainText("north" + (char)0xDC00))), "policies[0].startCondition.value"),
        ["nested nine levels deep"] = (Definitions.Valid.WithStartCondition(Nested(9)), "policies[0].startCondition" + string.Concat(Enumerable.Repeat(".operand", 8))),
        ["65 nodes"] = (Definitions.Valid.WithStartCondition(new Condition.AnyOf([.. Enumerable.Range(0, 4).Select(_ => new Condition.AllOf([.. Enumerable.Repeat<Condition>(new Condition.RequesterHas("buyer"), 15)]))])), "policies[0].startCondition.conditions[3].conditions[14]"),
        ["all of no condition"] = (Definitions.Valid.WithStartCondition(new Condition.AllOf([])), "policies[0].startCondition.conditions"),
        ["any of 17 conditions"] = (Definitions.Valid.WithStartCondition(new Condition.AnyOf([.. Enumerable.Repeat<Condition>(new Condition.RequesterHas("buyer"), 17)])), "policies[0].startCondition.conditions"),
        ["all of conditions none"] = (Definitions.Valid.WithStartCondition(new Condition.AllOf(null!)), "policies[0].startCondition.conditions"),
        ["condition none"] = (Definitions.Valid.WithStartCondition(new Condition.AnyOf([new Condition.RequesterHas("buyer"), null!])), "policies[0].startCondition.conditions[1]"),
        ["negate of none"] = (Definitions.Valid.WithStartCondition(new Condition.Negate(null!)), "policies[0].startCondition.operand"),
        ["one of a field the type does not protect"] = (Definitions.Valid.WithStartCondition(new Condition.OneOf("country", [new FieldValue.PlainText("north")])), "policies[0].startCondition.field"),
        ["one of no value"] = (Definitions.Valid.WithStartCondition(new Condition.OneOf("region", [])), "policies[0].startCondition.values"),
        ["one of 33 values"] = (Definitions.Valid.WithStartCondition(new Condition.OneOf("region", [.. Enumerable.Range(0, 33).Select(index => new FieldValue.PlainText($"region{index}"))])), "policies[0].startCondition.values"),
        ["one of values none"] = (Definitions.Valid.WithStartCondition(new Condition.OneOf("region", null!)), "policies[0].startCondition.values"),
        ["one of a value not of the field's kind"] = (Definitions.Valid.WithStartCondition(new Condition.OneOf("amount", [new FieldValue.DecimalNumber(1m), new FieldValue.WholeNumber(2)])), "policies[0].startCondition.values[1]"),
        ["one of a value none"] = (Definitions.Valid.WithStartCondition(new Condition.OneOf("region", [null!])), "policies[0].startCondition.values[0]"),
        ["requester role not a name"] = (Definitions.Valid.WithStartCondition(new Condition.RequesterHas("Buyer")), "policies[0].startCondition.role"),
        ["subject role none"] = (Definitions.Valid.WithStartCondition(new Condition.SubjectHas(null!)), "policies[0].startCondition.role"),
        ["step condition outside its rule"] = (Definitions.Valid.WithStep(1, 0, step => step with { Condition = new Condition.Compare("region", CompareOperator.Less, new FieldValue.PlainText("north")) }), "policies[1].steps[0].condition.operator"),
    };

    /// <summary>Definitions at the edge of each rule, within it.</summary>
    internal static IReadOnlyDictionary<string, ApprovalTypeDefinition> AtTheEdge { get; } = new Dictionary<string, ApprovalTypeDefinition>(StringComparer.Ordinal)
    {
        ["key of 64 characters"] = Definitions.Keyed("k" + new string('0', 63)),
        ["key of every separator"] = Definitions.Keyed("a.b-c_d9.0"),
        ["no protected field and no watcher"] = Definitions.Valid.WithStartCondition(null).WithStep(0, 0, step => step with { Condition = null }).WithStep(0, 1, step => step with { Condition = null }).WithStep(1, 0, step => step with { Condition = null }) with { ProtectedFields = [], Watchers = [] },
        ["protected fields 32"] = Definitions.Valid with { ProtectedFields = [.. Definitions.Valid.ProtectedFields, .. Enumerable.Range(0, 28).Select(index => new ProtectedField { Name = $"field{index}", Kind = FieldKind.Flag })] },
        ["policies 16"] = Definitions.Valid with { Policies = [.. Enumerable.Range(0, 16).Select(index => Definitions.Valid.Policies[1] with { Name = $"policy{index}" })] },
        ["steps 10"] = Definitions.Valid.WithPolicy(1, policy => policy with { Steps = [.. Enumerable.Range(0, 10).Select(index => policy.Steps[0] with { Name = $"step{index}" })] }),
        ["steps of two policies named alike"] = Definitions.Valid.WithStep(1, 0, step => step with { Name = "review" }),
        ["watchers 16 of both cases"] = Definitions.Valid with { Watchers = [.. Enumerable.Range(0, 15).Select(index => new ApproverReference.Person($"person-{index}")), new ApproverReference.Permission("orders.watch")] },
        ["fallback of 128 characters beyond the basic plane"] = Definitions.Valid with { Fallback = string.Concat(Enumerable.Repeat(char.ConvertFromUtf32(0x1F600), 128)) },
        ["approvers 16"] = Definitions.Valid.WithStep(0, 1, step => step with { Approvers = [.. Enumerable.Range(0, 16).Select(index => new ApproverReference.Person($"person-{index}"))] }),
        ["deadline of a minute"] = Definitions.Valid.WithStep(0, 1, step => step with { Deadline = step.Deadline! with { After = TimeSpan.FromMinutes(1) } }),
        ["deadline of 365 days"] = Definitions.Valid.WithStep(0, 1, step => step with { Deadline = new Deadline { After = TimeSpan.FromDays(365), Action = DeadlineAction.Cancel } }),
        ["whole number compared with a decimal field"] = Definitions.Valid.WithStartCondition(new Condition.Compare("amount", CompareOperator.GreaterOrEqual, new FieldValue.WholeNumber(1000))),
        ["decimal compared with a whole number field"] = Definitions.Valid.WithStartCondition(new Condition.Compare("count", CompareOperator.LessOrEqual, new FieldValue.DecimalNumber(2.5m))),
        ["text and flag fields compared for equality"] = Definitions.Valid.WithStartCondition(new Condition.AllOf([new Condition.Compare("region", CompareOperator.NotEqual, new FieldValue.PlainText("north")), new Condition.Compare("urgent", CompareOperator.Equal, new FieldValue.Flag(false))])),
        ["nested eight levels deep"] = Definitions.Valid.WithStartCondition(Nested(8)),
        ["64 nodes"] = Definitions.Valid.WithStartCondition(new Condition.AnyOf([.. Enumerable.Range(0, 3).Select(_ => new Condition.AllOf([.. Enumerable.Repeat<Condition>(new Condition.SubjectHas("auditor"), 16)])), new Condition.AllOf([.. Enumerable.Repeat<Condition>(new Condition.SubjectHas("auditor"), 11)])])),
        ["one of 32 values"] = Definitions.Valid.WithStartCondition(new Condition.OneOf("count", [.. Enumerable.Range(0, 32).Select(index => new FieldValue.WholeNumber(index))])),
        ["canonical form of 64 KiB"] = Definitions.OfSize(65_536),
        ["required of 2"] = Definitions.Valid.WithStep(1, 0, step => step with { Mode = ApprovalMode.AtLeast, Required = 2 }),
        ["required of 16 of a permission"] = Definitions.Valid.WithStep(1, 0, step => step with { Mode = ApprovalMode.AtLeast, Required = 16 }),
        ["required of the distinct persons named"] = Definitions.Valid.WithStep(0, 1, step => step with { Approvers = [new ApproverReference.Person(Definitions.Approver), new ApproverReference.Person(Definitions.StepFallback), new ApproverReference.Person(Definitions.Approver)], Mode = ApprovalMode.AtLeast, Required = 2 }),
        ["required above the persons named beside a permission"] = Definitions.Valid.WithStep(0, 0, step => step with { Mode = ApprovalMode.AtLeast, Required = 16 }),
        ["three reminders, the shortest an hour"] = Reminding(TimeSpan.FromHours(1), TimeSpan.FromDays(1), TimeSpan.FromHours(2)),
        ["reminder a second shorter than the deadline"] = Reminding(TimeSpan.FromDays(2) - TimeSpan.FromSeconds(1)),
    };

    /// <summary><see cref="Definitions.Valid"/> with the reminders <paramref name="before"/> on its step <c>finance</c>, whose deadline is two days.</summary>
    private static ApprovalTypeDefinition Reminding(params TimeSpan[] before)
    {
        return Definitions.Valid.WithStep(0, 1, step => step with { Deadline = step.Deadline! with { RemindBefore = before } });
    }

    /// <summary>A chain of negations <paramref name="levels"/> levels deep, its last level a role condition.</summary>
    private static Condition Nested(int levels)
    {
        Condition condition = new Condition.RequesterHas("buyer");
        for (int level = 1; level < levels; level++)
        {
            condition = new Condition.Negate(condition);
        }

        return condition;
    }
}