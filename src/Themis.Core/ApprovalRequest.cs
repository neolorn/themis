using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace Themis.Core;

/// <summary>
/// A host's request for an approval (<c>01</c> APR-REQ-001). Formatting it writes neither its
/// target, its snapshot, its persons nor their roles.
/// </summary>
public sealed record ApprovalRequest
{
    /// <summary>A published type's key.</summary>
    public required string Type { get; init; }

    /// <summary>What the request is about.</summary>
    public required Target Target { get; init; }

    /// <summary>The values of the version's protected fields, by field name.</summary>
    public IReadOnlyDictionary<string, FieldValue> Snapshot { get; init; } = ReadOnlyDictionary<string, FieldValue>.Empty;

    /// <summary>The person reference of the person asking.</summary>
    public required string Requester { get; init; }

    /// <summary>The person reference of the person the request is about, or none.</summary>
    public string? Subject { get; init; }

    /// <summary>The requester's roles, 0 to 64 names, as the host holds them now; read by conditions alone.</summary>
    public IReadOnlyList<string> RequesterRoles { get; init; } = [];

    /// <summary>The subject's roles, 0 to 64 names; empty where there is no subject.</summary>
    public IReadOnlyList<string> SubjectRoles { get; init; } = [];

    /// <summary>The closed request of the type and target this one resubmits, or none (<c>01</c> APR-REQ-005).</summary>
    public RequestId? Previous { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Type = {Type}, Previous = {Previous}");
        return true;
    }
}