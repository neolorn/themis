using System;
using System.Collections.Generic;
using System.Linq;

using Themis.Core;

namespace Themis.Types;

/// <summary>
/// Decides a condition on a request's snapshot and roles alone (<c>02</c> APR-TYPE-005). A
/// definition within its rule names only fields it protects, with values of their kinds, and a
/// request's snapshot holds each protected field at its kind (<c>01</c> APR-REQ-001), so a
/// condition never meets a value it cannot compare.
/// </summary>
internal static class ConditionCheck
{
    /// <summary>
    /// Whether <paramref name="condition"/> holds for a request whose snapshot is
    /// <paramref name="snapshot"/>, whose requester holds <paramref name="requesterRoles"/>, and
    /// whose subject holds <paramref name="subjectRoles"/>, none where it has no subject.
    /// </summary>
    /// <exception cref="InvalidOperationException">The snapshot lacks a field the condition names, or holds it at another kind: a broken invariant.</exception>
    internal static bool Holds(Condition condition, IReadOnlyDictionary<string, FieldValue> snapshot, IReadOnlyList<string> requesterRoles, IReadOnlyList<string>? subjectRoles)
    {
        return condition switch
        {
            Condition.AllOf all => all.Conditions.All(each => Holds(each, snapshot, requesterRoles, subjectRoles)),
            Condition.AnyOf any => any.Conditions.Any(each => Holds(each, snapshot, requesterRoles, subjectRoles)),
            Condition.Negate negate => !Holds(negate.Operand, snapshot, requesterRoles, subjectRoles),
            Condition.Compare compare => Compares(Value(snapshot, compare.Field), compare.Operator, compare.Value),
            Condition.OneOf oneOf => oneOf.Values.Any(value => Compares(Value(snapshot, oneOf.Field), CompareOperator.Equal, value)),
            Condition.RequesterHas requester => requesterRoles.Contains(requester.Role, StringComparer.Ordinal),
            Condition.SubjectHas subject => subjectRoles is not null && subjectRoles.Contains(subject.Role, StringComparer.Ordinal),
            _ => throw new InvalidOperationException("A condition is of a case the library does not define."),
        };
    }

    private static FieldValue Value(IReadOnlyDictionary<string, FieldValue> snapshot, string field)
    {
        return snapshot.TryGetValue(field, out FieldValue? value) ? value : throw new InvalidOperationException("A request's snapshot lacks a field its version's condition names.");
    }

    /// <summary>Compares a field's value with a value: a whole number and a decimal as decimals, text ordinally, a flag by equality alone.</summary>
    private static bool Compares(FieldValue field, CompareOperator comparison, FieldValue value)
    {
        int order = (field, value) switch
        {
            (FieldValue.WholeNumber left, FieldValue.WholeNumber right) => left.Value.CompareTo(right.Value),
            (FieldValue.WholeNumber or FieldValue.DecimalNumber, FieldValue.WholeNumber or FieldValue.DecimalNumber) => Number(field).CompareTo(Number(value)),
            (FieldValue.PlainText left, FieldValue.PlainText right) => string.CompareOrdinal(left.Value, right.Value),
            (FieldValue.Flag left, FieldValue.Flag right) => left.Value.CompareTo(right.Value),
            _ => throw new InvalidOperationException("A condition compares a field with a value of another kind."),
        };

        return comparison switch
        {
            CompareOperator.Equal => order == 0,
            CompareOperator.NotEqual => order != 0,
            CompareOperator.Greater => order > 0,
            CompareOperator.GreaterOrEqual => order >= 0,
            CompareOperator.Less => order < 0,
            CompareOperator.LessOrEqual => order <= 0,
            _ => throw new InvalidOperationException("A condition compares by an operator the library does not define."),
        };
    }

    private static decimal Number(FieldValue value)
    {
        return value switch
        {
            FieldValue.WholeNumber whole => whole.Value,
            FieldValue.DecimalNumber number => number.Value,
            _ => throw new InvalidOperationException("A number is read from a value of another kind."),
        };
    }
}