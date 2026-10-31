using System;

namespace Themis.Operations;

/// <summary>
/// A key of <c>06</c> section 4: how its raw text reads, its own rule, and its default
/// (<c>04</c> APR-OPS-001).
/// </summary>
/// <param name="Path">The key under the section <c>Themis</c>, as <c>06</c> spells it without the section.</param>
/// <param name="Read">The value a raw text reads as, as the configuration binder reads it; none where it does not read.</param>
/// <param name="Within">Whether a value that reads is within the key's own rule.</param>
/// <param name="Default">The key's value where it is absent.</param>
internal sealed record ConfigurationKey(string Path, Func<string, object?> Read, Func<object, bool> Within, object Default)
{
    /// <summary>The key as <c>06</c> spells it, with the section, which <c>details.key</c> names.</summary>
    internal string Name => ConfigurationRules.Section + ":" + Path;
}