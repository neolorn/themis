using System;

namespace Themis.Core;

/// <summary>One published version of an approval type, as a read answers it (<c>02</c> APR-TYPE-001).</summary>
public sealed record ApprovalTypeVersion
{
    /// <summary>The type's key.</summary>
    public required string Key { get; init; }

    /// <summary>The version's number, from 1.</summary>
    public required int Version { get; init; }

    /// <summary>The version's definition, as its canonical form gives it.</summary>
    public required ApprovalTypeDefinition Definition { get; init; }

    /// <summary>When the version was published.</summary>
    public required DateTimeOffset PublishedAt { get; init; }

    /// <summary>Whether the type is retired now.</summary>
    public required bool Retired { get; init; }
}