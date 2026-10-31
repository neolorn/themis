using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

namespace Themis.Repository.Tests;

/// <summary>
/// One project file of a repository, read as written.
/// </summary>
internal sealed class ProjectFile
{
    private static readonly FrozenSet<string> BuildOutputFolders = FrozenSet.Create(StringComparer.Ordinal, "bin", "obj");

    private static readonly FrozenSet<string> RootOnlyFolders = FrozenSet.Create(StringComparer.Ordinal, ".git", "tmp", "artifacts");

    private static readonly FrozenSet<string> WorkingTreeExclusions = FrozenSet.Create(StringComparer.Ordinal, ".git", "tmp");

    private ProjectFile(string root, string path)
    {
        Path = path;
        RelativePath = System.IO.Path.GetRelativePath(root, path).Replace('\\', '/');
        Name = System.IO.Path.GetFileNameWithoutExtension(path);
        Document = XDocument.Load(path);
    }

    /// <summary>The file's full path.</summary>
    internal string Path { get; }

    /// <summary>The file's path from the repository's root, with forward slashes.</summary>
    internal string RelativePath { get; }

    /// <summary>The project's name: its file name without the extension.</summary>
    internal string Name { get; }

    /// <summary>The folder holding the project file.</summary>
    internal string Folder => System.IO.Path.GetDirectoryName(Path)!;

    internal XDocument Document { get; }

    /// <summary>The names of the projects this one references.</summary>
    internal IReadOnlyList<string> ProjectReferences =>
        [.. Items("ProjectReference").Select(item => System.IO.Path.GetFileNameWithoutExtension(Include(item).Replace('\\', '/')))];

    internal IReadOnlyList<XElement> PackageReferences => [.. Items("PackageReference")];

    internal IReadOnlyList<string> FrameworkReferences => [.. Items("FrameworkReference").Select(Include)];

    /// <summary>
    /// The packages the project's lockfile records as direct, wherever their references were
    /// made: its own file, a shared build file or the central package file.
    /// </summary>
    internal IReadOnlyList<string> LockedDirectPackages
    {
        get
        {
            string lockFile = System.IO.Path.Combine(Folder, "packages.lock.json");
            if (!File.Exists(lockFile))
            {
                return [];
            }

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(lockFile));
            return [.. document.RootElement.GetProperty("dependencies").EnumerateObject()
                .SelectMany(framework => framework.Value.EnumerateObject())
                .Where(package => string.Equals(package.Value.GetProperty("type").GetString(), "Direct", StringComparison.Ordinal))
                .Select(package => package.Name)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)];
        }
    }

    /// <summary>The names of the properties the project file sets.</summary>
    internal IReadOnlyList<string> Properties =>
        [.. Document.Root!.Elements("PropertyGroup").Elements().Select(element => element.Name.LocalName)];

    internal string? Property(string name)
    {
        return Document.Root!.Elements("PropertyGroup").Elements(name).LastOrDefault()?.Value;
    }

    /// <summary>Reads every project file of the repository at <paramref name="root"/>.</summary>
    internal static IReadOnlyList<ProjectFile> ReadAll(string root)
    {
        return [.. Enumerate(root, "*.csproj").Order(StringComparer.Ordinal).Select(path => new ProjectFile(root, path))];
    }

    /// <summary>The project files of the repository's packages, <c>src/*/*.csproj</c>, by name.</summary>
    internal static IReadOnlyList<string> SourceProjects(string root)
    {
        string source = System.IO.Path.Combine(root, "src");
        if (!Directory.Exists(source))
        {
            return [];
        }

        return [.. Directory.EnumerateDirectories(source)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.csproj"))
            .OrderBy(System.IO.Path.GetFileNameWithoutExtension, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Lists the files of the repository at <paramref name="root"/> that match
    /// <paramref name="pattern"/>, leaving out build output, <c>tmp/</c> and Git's own folder.
    /// </summary>
    internal static IEnumerable<string> Enumerate(string root, string pattern)
    {
        return Walk(root, root, pattern, RootOnlyFolders);
    }

    /// <summary>
    /// Lists the files of the working tree at <paramref name="root"/> that match
    /// <paramref name="pattern"/>, leaving out only build output, <c>tmp/</c> and Git's own folder.
    /// </summary>
    internal static IEnumerable<string> EnumerateWorkingTree(string root, string pattern)
    {
        return Walk(root, root, pattern, WorkingTreeExclusions);
    }

    private static IEnumerable<string> Walk(string root, string folder, string pattern, FrozenSet<string> rootExclusions)
    {
        foreach (string file in Directory.EnumerateFiles(folder, pattern))
        {
            yield return file;
        }

        foreach (string child in Directory.EnumerateDirectories(folder))
        {
            string name = System.IO.Path.GetFileName(child);
            bool atRoot = string.Equals(folder, root, StringComparison.Ordinal);
            if (BuildOutputFolders.Contains(name) || (atRoot && rootExclusions.Contains(name)))
            {
                continue;
            }

            foreach (string file in Walk(root, child, pattern, rootExclusions))
            {
                yield return file;
            }
        }
    }

    internal static string Include(XElement item)
    {
        return item.Attribute("Include")?.Value ?? throw new InvalidOperationException($"An item {item.Name.LocalName} has no Include.");
    }

    private IEnumerable<XElement> Items(string kind)
    {
        return Document.Root!.Elements("ItemGroup").Elements(kind);
    }
}