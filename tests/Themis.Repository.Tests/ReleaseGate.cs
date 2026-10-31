using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Themis.Repository.Tests;

/// <summary>
/// The release gate over a repository (<c>03</c> APR-LIB-006, <c>05</c> APR-ENG-011 and
/// APR-ENG-012): its surface and set at the working tree compared with those the tag of the
/// newest final release holds, on a release tag at HEAD by that tag's version and otherwise as
/// a pull request; and the baseline the last release commit wrote, which is the release's own
/// set while nothing outside <c>docs/</c> has changed since it.
/// </summary>
internal static partial class ReleaseGate
{
    private const string Baseline = "build/gates/release-baseline.json";

    /// <summary>The problems of the working tree's release compatibility, none before the first final release, whose baseline is empty.</summary>
    internal static async Task<IReadOnlyList<string>> CompatibilityViolationsAsync(string repository, ReleaseSet current, Func<string, bool> withoutDefault, CancellationToken cancellationToken)
    {
        ProcessResult previous = await Processes.GateAsync("previous-release-tag.ps1", repository, cancellationToken, "-Repository", repository, "-Head", "HEAD", "-Final");
        if (previous.ExitCode != 0)
        {
            throw new InvalidOperationException($"previous-release-tag.ps1 failed: {previous.Output}");
        }

        string final = previous.Output.Trim();
        if (final.Length == 0)
        {
            return [];
        }

        List<string> baselineSurface = [];
        foreach (string path in (await GitAsync(repository, cancellationToken, "ls-tree", "-r", "--name-only", final)).Where(IsShippedFile))
        {
            baselineSurface.AddRange(Entries(Package(path), await GitAsync(repository, cancellationToken, "show", $"{final}:{path}")));
        }

        string[] stored = await GitAsync(repository, cancellationToken, "show", $"{final}:{Baseline}");
        ReleaseComparison comparison = ReleaseRules.Compare(baselineSurface, Surface(repository), ReleaseSet.Parse(string.Join('\n', stored)), current, withoutDefault);

        string[] tags = [.. (await GitAsync(repository, cancellationToken, "tag", "--points-at", "HEAD")).Where(ReleaseRules.IsReleaseTag)];
        if (tags.Length > 0)
        {
            return [.. tags.SelectMany(tag => ReleaseRules.TagViolations(tag, final, comparison))];
        }

        string log = string.Join('\n', await GitAsync(repository, cancellationToken, "log", "--format=%B%x00", $"{final}..HEAD"));
        bool marked = log.Split('\0').Any(message => ReleaseRules.MarkedBreaking(message.Trim('\n')));
        return ReleaseRules.PullRequestViolations(marked, final, comparison);
    }

    /// <summary>
    /// The problems of the baseline the last release commit wrote: while no file outside
    /// <c>docs/</c> has changed since it, the working tree's baseline is the release's own set,
    /// written as <see cref="ReleaseSet.Write"/> writes it.
    /// </summary>
    internal static async Task<IReadOnlyList<string>> BaselineViolationsAsync(string repository, ReleaseSet current, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(current);
        string? release = null;
        string? version = null;
        foreach (string line in await GitAsync(repository, cancellationToken, "log", "--format=%H%x09%s", "HEAD"))
        {
            Match subject = ReleaseSubject().Match(line[(line.IndexOf('\t', StringComparison.Ordinal) + 1)..]);
            if (subject.Success)
            {
                release = line[..line.IndexOf('\t', StringComparison.Ordinal)];
                version = subject.Groups["version"].Value;
                break;
            }
        }

        if (release is null)
        {
            return [];
        }

        string[] changed = await GitAsync(repository, cancellationToken, "diff", "--name-only", "--no-renames", release, "HEAD");
        if (changed.Any(path => !path.StartsWith("docs/", StringComparison.Ordinal)))
        {
            return [];
        }

        string file = Path.Combine(repository, Baseline);
        string written = File.Exists(file) ? (await File.ReadAllTextAsync(file, cancellationToken)).ReplaceLineEndings("\n") : string.Empty;
        return string.Equals(written, current.Write(), StringComparison.Ordinal)
            ? []
            : [$"{Baseline} is not the release's own set, which the release commit of {version} writes"];
    }

    /// <summary>
    /// Whether the entry added to a seam is a member the built seam declares without a default
    /// implementation: an abstract method of the interface by the member's name, or one the
    /// built assembly does not hold, so a member it cannot find is counted as a break.
    /// </summary>
    internal static bool WithoutDefault(string entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        string member = entry[(entry.IndexOf(": ", StringComparison.Ordinal) + 2)..];
        string? seam = ReleaseRules.Seams.FirstOrDefault(name => member.StartsWith(name + ".", StringComparison.Ordinal));
        Type? type = seam is null ? null : typeof(Core.IApproverDirectory).Assembly.GetType(seam);
        if (type is null)
        {
            return true;
        }

        string name = MemberName().Match(member[(seam!.Length + 1)..]).Value;
        string[] accessor = name.Split('.');
        string method = accessor.Length == 2 ? $"{accessor[1]}_{accessor[0]}" : name;
        MethodInfo[] declared = [.. type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public).Where(found => found.Name == method)];
        return declared.Length == 0 || declared.Any(found => found.IsAbstract);
    }

    /// <summary>The working tree's public surface: each package's shipped entries and unshipped ones, less those the unshipped file marks <c>*REMOVED*</c>.</summary>
    internal static IReadOnlyCollection<string> Surface(string repository)
    {
        HashSet<string> surface = new(StringComparer.Ordinal);
        string source = Path.Combine(repository, "src");
        if (!Directory.Exists(source))
        {
            return surface;
        }

        foreach (string project in Directory.GetDirectories(source))
        {
            string package = Path.GetFileName(project);
            string shipped = Path.Combine(project, "PublicAPI.Shipped.txt");
            string unshipped = Path.Combine(project, "PublicAPI.Unshipped.txt");
            string[] added = File.Exists(unshipped) ? File.ReadAllLines(unshipped) : [];
            surface.UnionWith(Entries(package, File.Exists(shipped) ? File.ReadAllLines(shipped) : []));
            surface.UnionWith(Entries(package, added.Where(line => !line.StartsWith("*REMOVED*", StringComparison.Ordinal))));
            surface.ExceptWith(Entries(package, added.Where(line => line.StartsWith("*REMOVED*", StringComparison.Ordinal)).Select(line => line["*REMOVED*".Length..])));
        }

        return surface;
    }

    private static IEnumerable<string> Entries(string package, IEnumerable<string> lines)
    {
        return lines.Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0 && line != "#nullable enable").Select(line => $"{package}: {line}");
    }

    private static bool IsShippedFile(string path)
    {
        string[] parts = path.Split('/');
        return parts.Length == 3 && parts[0] == "src" && parts[2] == "PublicAPI.Shipped.txt";
    }

    private static string Package(string path)
    {
        return path.Split('/')[1];
    }

    private static async Task<string[]> GitAsync(string repository, CancellationToken cancellationToken, params string[] arguments)
    {
        ProcessResult result = await Processes.GitAsync(repository, cancellationToken, arguments);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {result.Output}");
        }

        return result.Output.ReplaceLineEndings("\n").TrimEnd('\n') is { Length: > 0 } output ? output.Split('\n') : [];
    }

    [GeneratedRegex(@"^chore: release (?<version>(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-rc\.[1-9]\d*)?)$", RegexOptions.CultureInvariant)]
    private static partial Regex ReleaseSubject();

    [GeneratedRegex(@"^[^(\s]+", RegexOptions.CultureInvariant)]
    private static partial Regex MemberName();
}