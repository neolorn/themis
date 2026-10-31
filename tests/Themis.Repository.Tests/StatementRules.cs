using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Themis.Repository.Tests;

/// <summary>
/// The hand-written statements of <c>04</c> APR-OPS-003: none reads the database's clock
/// and none takes a session-level advisory lock.
/// </summary>
/// <remarks>
/// A statement is the contents of a string literal of a C# file under <c>src/</c>, or a SQL
/// file there without its comments, so a comment that quotes a call is no statement. The
/// migrations are read too, hand-written SQL among them, save the files EF Core generates in
/// full: each migration's <c>.Designer.cs</c> and the model snapshot.
/// </remarks>
internal static partial class StatementRules
{
    /// <summary>
    /// Answers each hand-written statement under <c>src/</c> of the repository at
    /// <paramref name="root"/> that reads the database's clock.
    /// </summary>
    internal static IReadOnlyList<string> ClockViolations(string root)
    {
        return [.. Statements(root).SelectMany(statement => ClockViolations(statement.Where, statement.Text))];
    }

    internal static IEnumerable<string> ClockViolations(string where, string text)
    {
        return ClockPattern().Matches(text).Select(clock => $"{where} calls {clock.Value.Trim()}; a statement takes the current instant as @now.");
    }

    /// <summary>
    /// Answers each hand-written statement under <c>src/</c> of the repository at
    /// <paramref name="root"/> that takes a session-level advisory lock.
    /// </summary>
    internal static IReadOnlyList<string> SessionLockViolations(string root)
    {
        return [.. Statements(root).SelectMany(statement => SessionLockViolations(statement.Where, statement.Text))];
    }

    internal static IEnumerable<string> SessionLockViolations(string where, string text)
    {
        return SessionLockPattern().Matches(text).Select(sessionLock => $"{where} calls {sessionLock.Value.Trim()}; every advisory lock is transaction-scoped.");
    }

    /// <summary>The statements of the files under <c>src/</c>, each with the file that holds it.</summary>
    internal static IEnumerable<(string Where, string Text)> Statements(string root)
    {
        string source = Path.Combine(root, "src");
        if (!Directory.Exists(source))
        {
            yield break;
        }

        foreach (string file in ProjectFile.Enumerate(source, "*.cs").Where(file => !IsGenerated(file)).Order(StringComparer.Ordinal))
        {
            string where = Path.GetRelativePath(root, file).Replace('\\', '/');
            foreach (string literal in CSharpSource.Read(File.ReadAllText(file)).Literals)
            {
                yield return (where, literal);
            }
        }

        foreach (string file in ProjectFile.Enumerate(source, "*.sql").Order(StringComparer.Ordinal))
        {
            yield return (Path.GetRelativePath(root, file).Replace('\\', '/'), SqlCommentPattern().Replace(File.ReadAllText(file), " "));
        }
    }

    private static bool IsGenerated(string file)
    {
        string name = Path.GetFileName(file);
        return name.EndsWith(".g.cs", StringComparison.Ordinal)
            || name.EndsWith(".Designer.cs", StringComparison.Ordinal)
            || name.EndsWith("ModelSnapshot.cs", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"\b(now\s*\(\s*\)|current_timestamp\b|clock_timestamp\s*\()", RegexOptions.IgnoreCase)]
    private static partial Regex ClockPattern();

    [GeneratedRegex(@"\bpg_(try_)?advisory_lock(_shared)?\s*\(", RegexOptions.IgnoreCase)]
    private static partial Regex SessionLockPattern();

    /// <summary>A SQL comment: a line's rest after <c>--</c>, or a block between <c>/*</c> and <c>*/</c>.</summary>
    [GeneratedRegex(@"--[^\n]*|/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex SqlCommentPattern();
}