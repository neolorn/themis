using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Themis.Core;

/// <summary>
/// What a request is for, in the host's terms: a kind, a name (<c>00</c> section 1.5), and the
/// host's identifier of it, a reference. It prints neither.
/// </summary>
public sealed record Target
{
    /// <summary>The target's kind, a name such as <c>order</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>The host's identifier of the target, a reference.</summary>
    public required string Id { get; init; }

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "The compiler makes ToString call this instance member, which prints nothing of the target.")]
    private bool PrintMembers(StringBuilder builder)
    {
        return false;
    }
}