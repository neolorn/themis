namespace Themis.Core;

/// <summary>The version a publish answers (<c>02</c> APR-TYPE-001).</summary>
public sealed record PublishedVersion
{
    /// <summary>The type's key.</summary>
    public required string Key { get; init; }

    /// <summary>The version's number, from 1.</summary>
    public required int Version { get; init; }

    /// <summary>Whether this call made the version; false where the definition equals the type's latest version.</summary>
    public required bool Created { get; init; }
}