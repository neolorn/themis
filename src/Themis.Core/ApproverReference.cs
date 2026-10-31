using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Themis.Core;

/// <summary>
/// Who may approve: the holders of a permission, or one person, read by pattern matching
/// (<c>02</c> APR-TYPE-004).
/// </summary>
public abstract record ApproverReference
{
    private ApproverReference()
    {
    }

    /// <summary>The persons the directory answers as holding a permission.</summary>
    /// <param name="Name">The permission, a name (<c>00</c> section 1.5).</param>
    [SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "03 APR-LIB-002 names the case Permission.")]
    public sealed record Permission(string Name) : ApproverReference;

    /// <summary>One person; it never prints the reference.</summary>
    /// <param name="Reference">The person's reference (<c>00</c> section 1.5).</param>
    public sealed record Person(string Reference) : ApproverReference
    {
        /// <inheritdoc/>
        protected override bool PrintMembers(StringBuilder builder)
        {
            return false;
        }
    }
}