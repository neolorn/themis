using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Themis.Core;

namespace Themis.Types;

/// <summary>
/// The rules a definition is checked by at publish (<c>02</c> APR-TYPE-002, APR-TYPE-004,
/// APR-TYPE-005, APR-TYPE-006). The members are checked in the order of the canonical form,
/// depth first, and the first outside its rule is answered by its path, its members named as
/// the canonical form names them and its list items by their index from 0
/// (<c>policies[1].steps[0].fallback</c>). A definition is data a host may take from a person,
/// so a member that is none where it must be something is outside its rule, not a fault.
/// </summary>
internal static class DefinitionRules
{
    private const int MostProtectedFields = 32;

    private const int MostPolicies = 16;

    private const int MostSteps = 10;

    private const int MostApprovers = 16;

    private const int FewestRequired = 2;

    private const int MostRequired = 16;

    private const int MostReminders = 3;

    private const int MostWatchers = 16;

    private const int MostConditions = 16;

    private const int MostValues = 32;

    private const int MostLevels = 8;

    private const int MostNodes = 64;

    /// <summary>The most bytes of a canonical form, 64 KiB.</summary>
    private const int MostFormBytes = 64 * 1024;

    private static readonly TimeSpan ShortestDeadline = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan LongestDeadline = TimeSpan.FromDays(365);

    private static readonly TimeSpan ShortestReminder = TimeSpan.FromHours(1);

    /// <summary>The path of the first member of <paramref name="definition"/> outside its rule, or the empty path where its canonical form is over 64 KiB, or none where it is within every rule.</summary>
    internal static string? Invalid(ApprovalTypeDefinition definition)
    {
        return Invalid(definition, out _);
    }

    /// <summary>
    /// The path of the first member of <paramref name="definition"/> outside its rule; where
    /// every member is within it, the empty path, the definition itself, where its canonical
    /// form is over 64 KiB (T-021); or none. <paramref name="form"/> is the canonical form where it is within every rule, and empty otherwise.
    /// </summary>
    internal static string? Invalid(ApprovalTypeDefinition definition, out byte[] form)
    {
        ArgumentNullException.ThrowIfNull(definition);
        form = [];
        if (InvalidMember(definition) is { } member)
        {
            return member;
        }

        byte[] written = CanonicalForm.Write(definition);
        if (written.Length > MostFormBytes)
        {
            return string.Empty;
        }

        form = written;
        return null;
    }

    private static string? InvalidMember(ApprovalTypeDefinition definition)
    {
        if (!TextRules.IsName(definition.Key))
        {
            return "key";
        }

        Dictionary<string, FieldKind> fields = new(StringComparer.Ordinal);
        if (!Holds(definition.ProtectedFields, 0, MostProtectedFields))
        {
            return "protectedFields";
        }

        for (int index = 0; index < definition.ProtectedFields.Count; index++)
        {
            string path = Item("protectedFields", index);
            ProtectedField field = definition.ProtectedFields[index];
            if (field is null)
            {
                return path;
            }

            if (!TextRules.IsName(field.Name) || fields.ContainsKey(field.Name))
            {
                return path + ".name";
            }

            if (field.Kind is not (FieldKind.Text or FieldKind.WholeNumber or FieldKind.DecimalNumber or FieldKind.Flag))
            {
                return path + ".kind";
            }

            fields.Add(field.Name, field.Kind);
        }

        if (InvalidPolicies(definition.Policies, fields) is { } policy)
        {
            return policy;
        }

        if (definition.Duplicates is not (DuplicateRule.Skip or DuplicateRule.RequireOther))
        {
            return "duplicates";
        }

        if (!Holds(definition.Watchers, 0, MostWatchers))
        {
            return "watchers";
        }

        for (int index = 0; index < definition.Watchers.Count; index++)
        {
            if (InvalidApprover(definition.Watchers[index], Item("watchers", index)) is { } watcher)
            {
                return watcher;
            }
        }

        return TextRules.IsReference(definition.Fallback) ? null : "fallback";
    }

    private static string? InvalidPolicies(IReadOnlyList<PolicyDefinition> policies, Dictionary<string, FieldKind> fields)
    {
        if (!Holds(policies, 1, MostPolicies))
        {
            return "policies";
        }

        HashSet<string> names = new(StringComparer.Ordinal);
        for (int index = 0; index < policies.Count; index++)
        {
            string path = Item("policies", index);
            PolicyDefinition policy = policies[index];
            if (policy is null)
            {
                return path;
            }

            if (!TextRules.IsName(policy.Name) || !names.Add(policy.Name))
            {
                return path + ".name";
            }

            if (policy.StartCondition is not null && InvalidTree(policy.StartCondition, path + ".startCondition", fields) is { } condition)
            {
                return condition;
            }

            if (InvalidSteps(policy.Steps, path + ".steps", fields) is { } step)
            {
                return step;
            }
        }

        return null;
    }

    private static string? InvalidSteps(IReadOnlyList<StepDefinition> steps, string path, Dictionary<string, FieldKind> fields)
    {
        if (!Holds(steps, 1, MostSteps))
        {
            return path;
        }

        HashSet<string> names = new(StringComparer.Ordinal);
        for (int index = 0; index < steps.Count; index++)
        {
            string at = Item(path, index);
            StepDefinition step = steps[index];
            if (step is null)
            {
                return at;
            }

            if (!TextRules.IsName(step.Name) || !names.Add(step.Name))
            {
                return at + ".name";
            }

            if (!Holds(step.Approvers, 1, MostApprovers))
            {
                return at + ".approvers";
            }

            for (int approver = 0; approver < step.Approvers.Count; approver++)
            {
                if (InvalidApprover(step.Approvers[approver], Item(at + ".approvers", approver)) is { } invalid)
                {
                    return invalid;
                }
            }

            if (step.Mode is not (ApprovalMode.AnyOne or ApprovalMode.All or ApprovalMode.AtLeast))
            {
                return at + ".mode";
            }

            if (step.Condition is not null && InvalidTree(step.Condition, at + ".condition", fields) is { } condition)
            {
                return condition;
            }

            if (!TextRules.IsReference(step.Fallback))
            {
                return at + ".fallback";
            }

            if (step.Deadline is not null && InvalidDeadline(step.Deadline, at + ".deadline") is { } deadline)
            {
                return deadline;
            }

            if (!RequiredHolds(step))
            {
                return at + ".required";
            }
        }

        return null;
    }

    /// <summary>
    /// Whether the step's <c>Required</c> is within its rule: given for <c>AtLeast</c> alone, 2 to 16,
    /// and where every approver is a person, at most the distinct persons they name; where any is a
    /// permission, the number is checked when the step starts.
    /// </summary>
    private static bool RequiredHolds(StepDefinition step)
    {
        if (step.Mode != ApprovalMode.AtLeast)
        {
            return step.Required is null;
        }

        if (step.Required is not int required || required < FewestRequired || required > MostRequired)
        {
            return false;
        }

        return step.Approvers.Any(approver => approver is ApproverReference.Permission)
            || required <= step.Approvers.Select(approver => ((ApproverReference.Person)approver).Reference).Distinct(StringComparer.Ordinal).Count();
    }

    private static string? InvalidDeadline(Deadline deadline, string path)
    {
        if (deadline.After.Ticks % TimeSpan.TicksPerSecond != 0 || deadline.After < ShortestDeadline || deadline.After > LongestDeadline)
        {
            return path + ".after";
        }

        if (deadline.Action is not (DeadlineAction.Escalate or DeadlineAction.Reject or DeadlineAction.Cancel))
        {
            return path + ".action";
        }

        if ((deadline.Action == DeadlineAction.Escalate) != (deadline.EscalateTo is not null))
        {
            return path + ".escalateTo";
        }

        if (deadline.EscalateTo is not null && InvalidApprover(deadline.EscalateTo, path + ".escalateTo") is { } escalateTo)
        {
            return escalateTo;
        }

        return InvalidReminders(deadline, path + ".remindBefore");
    }

    /// <summary>The path of the first reminder outside its rule: up to three, each at least an hour in whole seconds, shorter than the deadline and unlike those before it.</summary>
    private static string? InvalidReminders(Deadline deadline, string path)
    {
        if (!Holds(deadline.RemindBefore, 0, MostReminders))
        {
            return path;
        }

        for (int index = 0; index < deadline.RemindBefore.Count; index++)
        {
            TimeSpan before = deadline.RemindBefore[index];
            if (before.Ticks % TimeSpan.TicksPerSecond != 0 || before < ShortestReminder || before >= deadline.After || deadline.RemindBefore.Take(index).Contains(before))
            {
                return Item(path, index);
            }
        }

        return null;
    }

    private static string? InvalidApprover(ApproverReference approver, string path)
    {
        return approver switch
        {
            null => path,
            ApproverReference.Permission permission => TextRules.IsName(permission.Name) ? null : path + ".name",
            ApproverReference.Person person => TextRules.IsReference(person.Reference) ? null : path + ".reference",
            _ => throw new InvalidOperationException("An approver reference is of a case the library does not define."),
        };
    }

    private static string? InvalidTree(Condition root, string path, Dictionary<string, FieldKind> fields)
    {
        int nodes = 0;
        return InvalidCondition(root, path, 1, ref nodes, fields);
    }

    private static string? InvalidCondition(Condition condition, string path, int level, ref int nodes, Dictionary<string, FieldKind> fields)
    {
        if (condition is null || level > MostLevels || ++nodes > MostNodes)
        {
            return path;
        }

        switch (condition)
        {
            case Condition.AllOf all:
                return InvalidConditions(all.Conditions, path + ".conditions", level, ref nodes, fields);
            case Condition.AnyOf any:
                return InvalidConditions(any.Conditions, path + ".conditions", level, ref nodes, fields);
            case Condition.Negate negate:
                return InvalidCondition(negate.Operand, path + ".operand", level + 1, ref nodes, fields);
            case Condition.Compare compare:
                if (compare.Field is null || !fields.TryGetValue(compare.Field, out FieldKind compared))
                {
                    return path + ".field";
                }

                if (!Orders(compared, compare.Operator))
                {
                    return path + ".operator";
                }

                return IsOfKind(compare.Value, compared) || (IsNumber(compare.Value) && IsNumber(compared)) ? null : path + ".value";
            case Condition.OneOf oneOf:
                if (oneOf.Field is null || !fields.TryGetValue(oneOf.Field, out FieldKind listed))
                {
                    return path + ".field";
                }

                if (!Holds(oneOf.Values, 1, MostValues))
                {
                    return path + ".values";
                }

                for (int index = 0; index < oneOf.Values.Count; index++)
                {
                    if (!IsOfKind(oneOf.Values[index], listed))
                    {
                        return Item(path + ".values", index);
                    }
                }

                return null;
            case Condition.RequesterHas requester:
                return TextRules.IsName(requester.Role) ? null : path + ".role";
            case Condition.SubjectHas subject:
                return TextRules.IsName(subject.Role) ? null : path + ".role";
            default:
                throw new InvalidOperationException("A condition is of a case the library does not define.");
        }
    }

    private static string? InvalidConditions(IReadOnlyList<Condition> conditions, string path, int level, ref int nodes, Dictionary<string, FieldKind> fields)
    {
        if (!Holds(conditions, 1, MostConditions))
        {
            return path;
        }

        for (int index = 0; index < conditions.Count; index++)
        {
            if (InvalidCondition(conditions[index], Item(path, index), level + 1, ref nodes, fields) is { } invalid)
            {
                return invalid;
            }
        }

        return null;
    }

    /// <summary>Whether <paramref name="comparison"/> applies to a field of <paramref name="kind"/>: equality to every kind, ordering to a number alone.</summary>
    private static bool Orders(FieldKind kind, CompareOperator comparison)
    {
        return comparison switch
        {
            CompareOperator.Equal or CompareOperator.NotEqual => true,
            CompareOperator.Greater or CompareOperator.GreaterOrEqual or CompareOperator.Less or CompareOperator.LessOrEqual => IsNumber(kind),
            _ => false,
        };
    }

    /// <summary>Whether <paramref name="value"/> is of <paramref name="kind"/>; a text value must be text (<c>00</c> section 1.5).</summary>
    private static bool IsOfKind(FieldValue value, FieldKind kind)
    {
        return (value, kind) switch
        {
            (FieldValue.PlainText text, FieldKind.Text) => text.Value is not null && TextRules.IsText(text.Value),
            (FieldValue.WholeNumber, FieldKind.WholeNumber) => true,
            (FieldValue.DecimalNumber, FieldKind.DecimalNumber) => true,
            (FieldValue.Flag, FieldKind.Flag) => true,
            _ => false,
        };
    }

    private static bool IsNumber(FieldValue value)
    {
        return value is FieldValue.WholeNumber or FieldValue.DecimalNumber;
    }

    private static bool IsNumber(FieldKind kind)
    {
        return kind is FieldKind.WholeNumber or FieldKind.DecimalNumber;
    }

    private static bool Holds<T>(IReadOnlyList<T> list, int fewest, int most)
    {
        return list is not null && list.Count >= fewest && list.Count <= most;
    }

    private static string Item(string path, int index)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{path}[{index}]");
    }
}