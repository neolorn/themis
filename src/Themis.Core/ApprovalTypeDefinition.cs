using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Themis.Core;

/// <summary>
/// What a host publishes for one approval type: who must agree, and under which rules
/// (<c>02</c>). Its members are in the order of its canonical form, whose SHA-256 decides
/// whether two definitions are equal (<c>02</c> APR-TYPE-001). It prints its key and settings
/// alone, never a person reference, role or value.
/// </summary>
public sealed record ApprovalTypeDefinition
{
    /// <summary>The type's key, a name (<c>00</c> section 1.5), such as <c>order.approve</c>.</summary>
    public required string Key { get; init; }

    /// <summary>The fields of a target the approval covers, 0 to 32, no two named alike.</summary>
    public IReadOnlyList<ProtectedField> ProtectedFields { get; init; } = [];

    /// <summary>The alternative rule sets, 1 to 16, no two named alike, tried in this order (<c>02</c> APR-TYPE-003).</summary>
    public IReadOnlyList<PolicyDefinition> Policies { get; init; } = [];

    /// <summary>Whether a person who approved one step stands for a later step they are assigned to (<c>01</c> APR-REQ-006).</summary>
    public required DuplicateRule Duplicates { get; init; }

    /// <summary>The persons told of each request and its outcome, 0 to 16 approver references, who never decide (<c>02</c> APR-TYPE-006).</summary>
    public IReadOnlyList<ApproverReference> Watchers { get; init; } = [];

    /// <summary>The type's fallback approver, a person reference (<c>00</c> section 1.5).</summary>
    public required string Fallback { get; init; }

    /// <summary>Whether a request no policy holds for is approved at once rather than sent to <see cref="Fallback"/>; false by default.</summary>
    public bool AllowAutoApprove { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Key = {Key}, Duplicates = {Duplicates}, AllowAutoApprove = {AllowAutoApprove}");
        return true;
    }
}