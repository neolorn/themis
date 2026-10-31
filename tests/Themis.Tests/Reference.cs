using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Themis.Tests;

/// <summary>The names <c>docs/spec/06-reference.md</c> lists, read from the repository above the test run.</summary>
internal static partial class Reference
{
    private static readonly Lazy<string[]> Lines = new(() => File.ReadAllLines(Find()));

    /// <summary>The first column of each row of the tables of section <paramref name="number"/>, its backticks taken off.</summary>
    internal static IReadOnlyList<string> FirstColumn(int number)
    {
        return [.. Section(number).Select(line => FirstCell().Match(line)).Where(cell => cell.Success).Select(cell => cell.Groups["name"].Value)];
    }

    /// <summary>The cells of each row of the tables of section <paramref name="number"/> whose first cell is in backticks, each cell's backticks taken off.</summary>
    internal static IReadOnlyList<IReadOnlyList<string>> Rows(int number)
    {
        return [.. Section(number)
            .Where(line => FirstCell().IsMatch(line))
            .Select(line => (IReadOnlyList<string>)[.. line.Trim('|').Split('|').Select(cell => cell.Trim().Replace("`", string.Empty, StringComparison.Ordinal))])];
    }

    /// <summary>Every name in backticks in section <paramref name="number"/>'s text outside its tables.</summary>
    internal static IReadOnlyList<string> Named(int number)
    {
        return [.. Section(number).Where(line => !line.StartsWith('|')).SelectMany(line => Backticked().Matches(line)).Select(name => name.Groups["name"].Value)];
    }

    /// <summary>The first name in backticks of each row of the tables of section <paramref name="number"/> whose first cell starts with one, as <c>`name` (startup)</c>.</summary>
    internal static IReadOnlyList<string> LeadingNames(int number)
    {
        return [.. Section(number).Select(line => LeadingName().Match(line)).Where(name => name.Success).Select(name => name.Groups["name"].Value)];
    }

    /// <summary>
    /// The members and spellings, in order, of the enumeration <paramref name="enumeration"/>:
    /// section 6's rows for <c>EventKind</c>, as section 2 gives, and section 2's row otherwise.
    /// </summary>
    internal static IReadOnlyList<(string Member, string Spelling)> Spellings(string enumeration)
    {
        IEnumerable<string> lines = enumeration == "EventKind"
            ? Section(6).Where(line => LeadingName().IsMatch(line)).Select(line => line.Split('|')[1])
            : Section(2).Where(line => line.StartsWith($"| `{enumeration}` |", StringComparison.Ordinal)).Select(line => line.Split('|')[2]);
        return [.. lines.SelectMany(cell => Spelled().Matches(cell)).Select(member => (member.Groups["member"].Value, member.Groups["spelling"].Value))];
    }

    private static IEnumerable<string> Section(int number)
    {
        return Lines.Value
            .SkipWhile(line => !line.StartsWith($"## {number}.", StringComparison.Ordinal))
            .Skip(1)
            .TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal));
    }

    private static string Find()
    {
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            string reference = Path.Combine(folder.FullName, "docs", "spec", "06-reference.md");
            if (File.Exists(Path.Combine(folder.FullName, "Themis.sln")) && File.Exists(reference))
            {
                return reference;
            }
        }

        throw new InvalidOperationException("No folder above the test run holds Themis.sln and docs/spec/06-reference.md.");
    }

    [GeneratedRegex(@"^\| `(?<name>[^`]+)` \|", RegexOptions.CultureInvariant)]
    private static partial Regex FirstCell();

    [GeneratedRegex(@"`(?<name>[^`]+)`", RegexOptions.CultureInvariant)]
    private static partial Regex Backticked();

    [GeneratedRegex(@"^\| `(?<name>[^`]+)`", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingName();

    [GeneratedRegex(@"`(?<member>[A-Za-z]+)` \(`(?<spelling>[a-z]+)`", RegexOptions.CultureInvariant)]
    private static partial Regex Spelled();
}