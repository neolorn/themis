using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Themis.Repository.Tests;

/// <summary>
/// The secret scanner's configuration (<c>05</c> APR-ENG-001): gitleaks' default, extended by
/// no entry, since every secret a test needs is made at run time.
/// </summary>
/// <remarks>
/// The configuration is read as the small part of TOML it may use: a title and the
/// <c>[extend]</c> table, each of one-line keys.
/// </remarks>
internal static partial class ScannerConfigurationRules
{
    internal static string File(string root)
    {
        return Path.Combine(root, "build", "gates", "gitleaks.toml");
    }

    /// <summary>Answers each part of the configuration of the repository at <paramref name="root"/> outside the rule.</summary>
    internal static IReadOnlyList<string> Violations(string root)
    {
        string file = File(root);
        if (!System.IO.File.Exists(file))
        {
            return ["build/gates/gitleaks.toml is missing."];
        }

        List<string> violations = [];
        List<(string Table, Dictionary<string, string> Keys)> tables = [("", new(StringComparer.Ordinal))];
        int number = 0;
        foreach (string raw in System.IO.File.ReadLines(file))
        {
            number++;
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            Match header = TablePattern().Match(line);
            if (header.Success)
            {
                tables.Add((header.Groups["name"].Value, new(StringComparer.Ordinal)));
                continue;
            }

            Match key = KeyPattern().Match(line);
            if (!key.Success || !tables[^1].Keys.TryAdd(key.Groups["key"].Value, key.Groups["value"].Value.Trim()))
            {
                violations.Add($"gitleaks.toml line {number} is not a key the rule allows once in its table.");
            }
        }

        foreach ((string table, Dictionary<string, string> keys) in tables)
        {
            violations.AddRange(table switch
            {
                "" => TopViolations(keys),
                "[extend]" => ExtendViolations(keys),
                _ => [$"gitleaks.toml adds the table {table}, an entry the rule does not allow."],
            });
        }

        int extensions = tables.Count(table => string.Equals(table.Table, "[extend]", StringComparison.Ordinal));
        if (extensions == 0)
        {
            violations.Add("gitleaks.toml does not extend gitleaks' default.");
        }
        else if (extensions > 1)
        {
            violations.Add("gitleaks.toml holds more than one [extend] table.");
        }

        return violations;
    }

    private static IEnumerable<string> TopViolations(Dictionary<string, string> keys)
    {
        return keys.Keys.Where(key => !string.Equals(key, "title", StringComparison.Ordinal))
            .Select(key => $"gitleaks.toml sets {key} at its top, an entry the rule does not allow.");
    }

    private static IEnumerable<string> ExtendViolations(Dictionary<string, string> keys)
    {
        if (keys.Count != 1 || !keys.TryGetValue("useDefault", out string? value) || !string.Equals(value, "true", StringComparison.Ordinal))
        {
            yield return "gitleaks.toml's [extend] table holds more than useDefault = true.";
        }
    }

    [GeneratedRegex(@"^(?<name>\[\[?[^\]]*\]\]?)$")]
    private static partial Regex TablePattern();

    [GeneratedRegex(@"^(?<key>[A-Za-z]+)\s*=\s*(?<value>.+)$")]
    private static partial Regex KeyPattern();
}