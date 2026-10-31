using System;
using System.Globalization;
using System.Text;

namespace Themis.Core;

/// <summary>One version of a secret; it prints its number and never its value.</summary>
public sealed record SecretVersion
{
    /// <summary>The version's number, from 1.</summary>
    public required int Version { get; init; }

    /// <summary>The version's value.</summary>
    public required ReadOnlyMemory<byte> Value { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Version = {Version}");
        return true;
    }
}