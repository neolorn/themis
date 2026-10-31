namespace Themis.Core;

/// <summary>
/// A field of a target that the approval covers: locked while a request is pending, and voiding
/// the approval where it changes before the host has acted (<c>01</c> APR-REQ-011).
/// </summary>
public sealed record ProtectedField
{
    /// <summary>The field's name (<c>00</c> section 1.5).</summary>
    public required string Name { get; init; }

    /// <summary>The kind of the field's values.</summary>
    public required FieldKind Kind { get; init; }
}