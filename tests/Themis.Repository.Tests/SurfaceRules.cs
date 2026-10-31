using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Themis.Repository.Tests;

/// <summary>
/// Rules over the public types of a built assembly: <c>05</c> APR-ENG-004's sealing and
/// tokens, APR-ENG-007's type names, and the public API files of <c>03</c> APR-LIB-002.
/// </summary>
internal static partial class SurfaceRules
{
    private const string LibraryName = "Themis";

    /// <summary>Answers each public class neither sealed nor abstract.</summary>
    internal static IEnumerable<string> SealingViolations(IEnumerable<PublicType> types)
    {
        return types.Where(type => type.Kind == PublicTypeKind.Class && !type.IsSealed && !type.IsAbstract)
            .Select(type => $"{type.Name} is a public class neither sealed nor abstract.");
    }

    /// <summary>
    /// Answers each public asynchronous method that takes no <c>CancellationToken</c>, save the
    /// <c>DisposeAsync</c> of a type that implements <c>IAsyncDisposable</c>.
    /// </summary>
    internal static IEnumerable<string> TokenViolations(IEnumerable<PublicType> types)
    {
        foreach (PublicType type in types)
        {
            foreach (PublicMethod method in type.Methods.Where(IsAsynchronous))
            {
                bool disposal = string.Equals(method.Name, "DisposeAsync", StringComparison.Ordinal) && method.ParameterTypes.Count == 0
                    && type.Interfaces.Contains("System.IAsyncDisposable", StringComparer.Ordinal);
                if (!disposal && !method.ParameterTypes.Contains("System.Threading.CancellationToken", StringComparer.Ordinal))
                {
                    yield return $"{type.Name}.{method.Name} is a public asynchronous method without a CancellationToken.";
                }
            }
        }
    }

    /// <summary>Answers each public type whose name holds the library's name.</summary>
    internal static IEnumerable<string> NameViolations(IEnumerable<PublicType> types)
    {
        return types.Where(type => type.Name[(type.Name.LastIndexOf('.') + 1)..].Contains(LibraryName, StringComparison.OrdinalIgnoreCase))
            .Select(type => $"{type.Name} is a public type whose name holds the library's name.");
    }

    /// <summary>
    /// Answers each public type the assembly declares and the project's public API files do
    /// not list, and each type the files list and the assembly does not declare.
    /// </summary>
    internal static IEnumerable<string> ListingViolations(string project, IEnumerable<PublicType> types)
    {
        HashSet<string> listed = ListedTypes(project);
        HashSet<string> declared = [.. types.Select(type => type.Name)];
        string name = Path.GetFileName(project);
        foreach (string type in declared.Where(type => !listed.Contains(type)).Order(StringComparer.Ordinal))
        {
            yield return $"{name} declares the public type {type}, which its public API files do not list.";
        }

        foreach (string type in listed.Where(type => !declared.Contains(type)).Order(StringComparer.Ordinal))
        {
            yield return $"{name}'s public API files list {type}, which it does not declare.";
        }
    }

    /// <summary>
    /// The types a project's <c>PublicAPI.Shipped.txt</c> and <c>PublicAPI.Unshipped.txt</c>
    /// list: each entry that names neither a member (<c>-&gt;</c>, <c>(</c>) nor a removal.
    /// </summary>
    internal static HashSet<string> ListedTypes(string project)
    {
        HashSet<string> types = new(StringComparer.Ordinal);
        foreach (string file in new[] { "PublicAPI.Shipped.txt", "PublicAPI.Unshipped.txt" })
        {
            string path = Path.Combine(project, file);
            if (!File.Exists(path))
            {
                continue;
            }

            foreach (string raw in File.ReadLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }

                if (line.StartsWith("*REMOVED*", StringComparison.Ordinal))
                {
                    types.Remove(Entry(line["*REMOVED*".Length..]));
                    continue;
                }

                if (!line.Contains("->", StringComparison.Ordinal) && !line.Contains('(', StringComparison.Ordinal))
                {
                    types.Add(Entry(line));
                }
            }
        }

        return types;
    }

    private static string Entry(string line)
    {
        string name = ModifierPattern().Replace(line.TrimStart('~'), string.Empty);
        return GenericPattern().Replace(name, match => "`" + (match.Groups["parameters"].Value.Count(c => c == ',') + 1));
    }

    /// <summary>A method that answers exactly <c>Task</c>, <c>Task&lt;T&gt;</c>, <c>ValueTask</c>, <c>ValueTask&lt;T&gt;</c> or <c>IAsyncEnumerable&lt;T&gt;</c>.</summary>
    private static bool IsAsynchronous(PublicMethod method)
    {
        string type = method.ReturnType;
        return string.Equals(type, "System.Threading.Tasks.Task", StringComparison.Ordinal)
            || string.Equals(type, "System.Threading.Tasks.ValueTask", StringComparison.Ordinal)
            || type.StartsWith("System.Threading.Tasks.Task`1<", StringComparison.Ordinal)
            || type.StartsWith("System.Threading.Tasks.ValueTask`1<", StringComparison.Ordinal)
            || type.StartsWith("System.Collections.Generic.IAsyncEnumerable`1<", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"^((abstract|static|sealed|virtual|override|readonly|const|new)\s+)+")]
    private static partial Regex ModifierPattern();

    [GeneratedRegex(@"<(?<parameters>[^<>]+)>")]
    private static partial Regex GenericPattern();
}