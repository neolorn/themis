using System.Text;

namespace Themis.Core;

/// <summary>
/// A protected field's value, one case for each <c>FieldKind</c>, read by pattern matching. No
/// case prints its value.
/// </summary>
public abstract record FieldValue
{
    private FieldValue()
    {
    }

    /// <summary>
    /// A value of the kind <c>text</c>; named so, since <c>Text</c> would match the namespace
    /// <c>System.Text</c>.
    /// </summary>
    /// <param name="Value">The text.</param>
    public sealed record PlainText(string Value) : FieldValue
    {
        /// <inheritdoc/>
        protected override bool PrintMembers(StringBuilder builder)
        {
            return false;
        }
    }

    /// <summary>A value of the kind <c>wholenumber</c>.</summary>
    /// <param name="Value">The number.</param>
    public sealed record WholeNumber(long Value) : FieldValue
    {
        /// <inheritdoc/>
        protected override bool PrintMembers(StringBuilder builder)
        {
            return false;
        }
    }

    /// <summary>A value of the kind <c>decimalnumber</c>.</summary>
    /// <param name="Value">The number.</param>
    public sealed record DecimalNumber(decimal Value) : FieldValue
    {
        /// <inheritdoc/>
        protected override bool PrintMembers(StringBuilder builder)
        {
            return false;
        }
    }

    /// <summary>A value of the kind <c>flag</c>.</summary>
    /// <param name="Value">The flag.</param>
    public sealed record Flag(bool Value) : FieldValue
    {
        /// <inheritdoc/>
        protected override bool PrintMembers(StringBuilder builder)
        {
            return false;
        }
    }
}