using System.Collections.Generic;
using System.Text;

namespace Themis.Core;

/// <summary>
/// A condition on a request's snapshot and roles, data checked at publish against the type's
/// protected fields, read by pattern matching (<c>02</c> APR-TYPE-005). A tree is at most 8
/// levels deep and 64 nodes in all. No case prints a role or a value.
/// </summary>
public abstract record Condition
{
    private Condition()
    {
    }

    /// <summary>Holds where every one of its conditions holds.</summary>
    /// <param name="Conditions">The conditions, 1 to 16.</param>
    public sealed record AllOf(IReadOnlyList<Condition> Conditions) : Condition;

    /// <summary>Holds where any one of its conditions holds.</summary>
    /// <param name="Conditions">The conditions, 1 to 16.</param>
    public sealed record AnyOf(IReadOnlyList<Condition> Conditions) : Condition;

    /// <summary>Holds where its operand does not.</summary>
    /// <param name="Operand">The condition negated.</param>
    public sealed record Negate(Condition Operand) : Condition;

    /// <summary>
    /// Compares a protected field's value with a value of the field's kind; a whole number and
    /// a decimal compare as decimals, text ordinally, and a flag by equality alone.
    /// </summary>
    /// <param name="Field">The protected field.</param>
    /// <param name="Operator">The comparison; the ordering ones for a number field alone.</param>
    /// <param name="Value">The value compared with; it prints nothing.</param>
    public sealed record Compare(string Field, CompareOperator Operator, FieldValue Value) : Condition;

    /// <summary>Holds where a protected field's value is one of the values.</summary>
    /// <param name="Field">The protected field.</param>
    /// <param name="Values">The values, 1 to 32, of the field's kind.</param>
    public sealed record OneOf(string Field, IReadOnlyList<FieldValue> Values) : Condition;

    /// <summary>Holds where the request's requester roles hold the role; it never prints the role.</summary>
    /// <param name="Role">The role, a name (<c>00</c> section 1.5).</param>
    public sealed record RequesterHas(string Role) : Condition
    {
        /// <inheritdoc/>
        protected override bool PrintMembers(StringBuilder builder)
        {
            return false;
        }
    }

    /// <summary>Holds where the request has a subject whose roles hold the role; it never prints the role.</summary>
    /// <param name="Role">The role, a name (<c>00</c> section 1.5).</param>
    public sealed record SubjectHas(string Role) : Condition
    {
        /// <inheritdoc/>
        protected override bool PrintMembers(StringBuilder builder)
        {
            return false;
        }
    }
}