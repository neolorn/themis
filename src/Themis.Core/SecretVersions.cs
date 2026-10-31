using System.Collections.Generic;

namespace Themis.Core;

/// <summary>The versions of one secret a secret source holds.</summary>
public sealed record SecretVersions
{
    /// <summary>The versions, each with its number.</summary>
    public required IReadOnlyList<SecretVersion> Versions { get; init; }
}