using System;

namespace Themis.Types;

/// <summary>
/// A row of the version table: one published version of an approval type, written once and
/// never changed or deleted (<c>02</c> APR-TYPE-001). The definition's canonical form is kept
/// as text, so the bytes hashed are the bytes kept (<c>05</c> APR-ENG-005).
/// </summary>
internal sealed class StoredVersion
{
    public required string Key { get; set; }

    /// <summary>The version's number, from 1.</summary>
    public required int Version { get; set; }

    /// <summary>The definition's canonical form.</summary>
    public required string Definition { get; set; }

    /// <summary>The SHA-256 of the canonical form's UTF-8 bytes.</summary>
    public required byte[] Hash { get; set; }

    public required DateTimeOffset PublishedAt { get; set; }
}