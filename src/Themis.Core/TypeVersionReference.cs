namespace Themis.Core;

/// <summary>One published version of an approval type (<c>02</c> APR-TYPE-001).</summary>
public sealed record TypeVersionReference
{
    /// <summary>The type's key.</summary>
    public required string Type { get; init; }

    /// <summary>The version's number, from 1.</summary>
    public required int Version { get; init; }
}