using System.Collections.Generic;

namespace Themis.Core;

/// <summary>What an erasure changed, and what it kept (<c>01</c> APR-REQ-015).</summary>
public sealed record ErasureReceipt
{
    /// <summary>The number of rows the erasure changed; 0 where nothing named the person.</summary>
    public required int Changed { get; init; }

    /// <summary>The versions, older than their type's newest, that still name the person, by type and version, as published versions are never changed.</summary>
    public required IReadOnlyList<TypeVersionReference> KeptVersions { get; init; }
}