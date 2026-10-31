using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Themis.Repository.Tests;

/// <summary>
/// The packages' table of <c>00</c> section 2.2, read from the specification: each package's
/// "What it holds" cell, without markup or references, which is its description (<c>05</c>
/// APR-ENG-011).
/// </summary>
internal static partial class PackageTable
{
    internal static FrozenDictionary<string, string> Descriptions { get; } = Read(RepositoryRoot.Combine("docs", "spec", "00-overview.md"));

    internal static FrozenDictionary<string, string> Read(string overview)
    {
        Dictionary<string, string> descriptions = new(StringComparer.Ordinal);
        bool inSection = false;
        foreach (string line in File.ReadLines(overview))
        {
            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                inSection = line.StartsWith("### 2.2 ", StringComparison.Ordinal);
                continue;
            }

            Match row = RowPattern().Match(line);
            if (inSection && row.Success)
            {
                descriptions[row.Groups["package"].Value] = Plain(row.Groups["holds"].Value);
            }
        }

        return descriptions.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>A cell's text without its references to other items and without markup.</summary>
    internal static string Plain(string cell)
    {
        string text = ReferencePattern().Replace(cell, string.Empty);
        return text.Replace("**", string.Empty, StringComparison.Ordinal).Replace("`", string.Empty, StringComparison.Ordinal).Trim();
    }

    [GeneratedRegex(@"^\| `(?<package>Themis(\.[A-Za-z]+)*)` \| (?<holds>[^|]+?) \| [^|]+ \|$")]
    private static partial Regex RowPattern();

    /// <summary>A parenthesised reference: a chapter's number, or an item's identifier.</summary>
    [GeneratedRegex(@"\s*\((`\d{2}`[^)]*|[^)]*\bAPR-[A-Z]+-\d{3}\b[^)]*)\)")]
    private static partial Regex ReferencePattern();
}