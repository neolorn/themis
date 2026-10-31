using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Themis.Repository.Tests;

/// <summary>
/// The layout of <c>05</c> APR-ENG-001: one project per package under <c>src/</c>, one test
/// project per source project under <c>tests/</c>, the solution holding exactly those, flat
/// projects but for the named folders, and namespaces that follow the folders.
/// </summary>
internal static partial class LayoutRules
{
    private const string NuGetOrg = "https://api.nuget.org/v3/index.json";

    /// <summary>The files at the repository's root (05 APR-ENG-001).</summary>
    private static readonly IReadOnlyList<string> RootFiles =
        ["Themis.sln", "Directory.Build.props", "Directory.Packages.props", "global.json", ".editorconfig", ".gitignore", "nuget.config", "CHANGELOG.md", "RELEASE-NOTES.md"];

    /// <summary>The files the root holds from phase 0: all of them but the release notes, and the tool manifest.</summary>
    private static readonly IReadOnlyList<string> RequiredRootFiles =
        [.. RootFiles.Where(file => file is not "RELEASE-NOTES.md"), ".config/dotnet-tools.json"];

    /// <summary>The folders at the root: the projects, the sample host, the documents, the gates, the pipeline and the tool manifest.</summary>
    private static readonly IReadOnlyList<string> RootFolders = ["src", "tests", "samples", "docs", "build", ".github", ".config"];

    /// <summary>Answers each project of the repository at <paramref name="root"/> that is outside the layout.</summary>
    internal static IReadOnlyList<string> ProjectViolations(string root)
    {
        List<string> violations = [];
        IReadOnlyList<ProjectFile> projects = ProjectFile.ReadAll(root);
        List<string> sources = [];
        List<string> tests = [];

        foreach (ProjectFile project in projects)
        {
            string[] parts = project.RelativePath.Split('/');
            bool placed = parts.Length == 3 && string.Equals(parts[1], project.Name, StringComparison.Ordinal);
            if (placed && string.Equals(parts[0], "src", StringComparison.Ordinal) && Packages.All.Contains(project.Name, StringComparer.Ordinal))
            {
                sources.Add(project.Name);
            }
            else if (placed && string.Equals(parts[0], "tests", StringComparison.Ordinal))
            {
                tests.Add(project.Name);
            }
            else if (!(parts.Length == 3 && string.Equals(parts[0], "samples", StringComparison.Ordinal)))
            {
                violations.Add($"{project.RelativePath} is not a package's project at src/<Package>/ or a test project at tests/<Project>/.");
            }
        }

        foreach (string test in tests)
        {
            bool known = string.Equals(test, Packages.RepositoryTests, StringComparison.Ordinal)
                || sources.Any(source => string.Equals(test, Packages.TestProject(source), StringComparison.Ordinal));
            if (!known)
            {
                violations.Add($"tests/{test} is the test project of no source project.");
            }
        }

        foreach (string source in sources.Where(source => !tests.Contains(Packages.TestProject(source), StringComparer.Ordinal)))
        {
            violations.Add($"src/{source} has no test project tests/{Packages.TestProject(source)}.");
        }

        if (!tests.Contains(Packages.RepositoryTests, StringComparer.Ordinal))
        {
            violations.Add($"tests/{Packages.RepositoryTests} is missing.");
        }

        foreach (string package in Packages.Present.Where(package => !sources.Contains(package, StringComparer.Ordinal)))
        {
            violations.Add($"src/{package} is missing, though {package} is a package of 00 section 2.2.");
        }

        violations.AddRange(SolutionViolations(root, [.. sources.Select(name => $"src/{name}/{name}.csproj"), .. tests.Select(name => $"tests/{name}/{name}.csproj")]));
        violations.AddRange(SolutionFileViolations(root));
        return violations;
    }

    /// <summary>Answers each folder inside a project, and each file's namespace or public type, outside the layout.</summary>
    internal static IReadOnlyList<string> FolderViolations(string root)
    {
        List<string> violations = [];
        foreach (ProjectFile project in ProjectFile.ReadAll(root))
        {
            IReadOnlyList<string> allowed = Packages.Folders.TryGetValue(project.Name, out IReadOnlyList<string>? folders) ? folders : [];
            foreach (string folder in Directory.EnumerateDirectories(project.Folder))
            {
                string name = Path.GetFileName(folder);
                if (name is "bin" or "obj")
                {
                    continue;
                }

                if (!allowed.Contains(name, StringComparer.Ordinal))
                {
                    violations.Add($"{project.RelativePath}: the folder {name} is not one the project may hold.");
                }
                else if (Directory.EnumerateDirectories(folder).Any())
                {
                    violations.Add($"{project.RelativePath}: the folder {name} holds a folder of its own.");
                }
            }

            violations.AddRange(SourceFileViolations(root, project));
        }

        return violations;
    }

    private static IEnumerable<string> SourceFileViolations(string root, ProjectFile project)
    {
        bool shipped = project.RelativePath.StartsWith("src/", StringComparison.Ordinal);
        foreach (string file in ProjectFile.Enumerate(project.Folder, "*.cs"))
        {
            string relative = Path.GetRelativePath(project.Folder, file).Replace('\\', '/');
            string folder = Path.GetDirectoryName(relative)!.Replace('\\', '/');
            string expected = folder.Length == 0 ? project.Name : project.Name + "." + folder.Replace('/', '.');
            string code = CSharpSource.Read(File.ReadAllText(file)).Code;
            string where = Path.GetRelativePath(root, file).Replace('\\', '/');

            // The sample host's entry point is a file of top-level statements, to which C# gives
            // no namespace, and whose generated Program class its tests start the host by.
            bool entryPoint = project.RelativePath.StartsWith("samples/", StringComparison.Ordinal)
                && !project.Name.EndsWith(".Tests", StringComparison.Ordinal)
                && string.Equals(relative, "Program.cs", StringComparison.Ordinal);
            Match declared = NamespacePattern().Match(code);
            if (!declared.Success && !entryPoint)
            {
                yield return $"{where} declares no namespace.";
            }
            else if (declared.Success && !string.Equals(declared.Groups["name"].Value, expected, StringComparison.Ordinal))
            {
                yield return $"{where} declares the namespace {declared.Groups["name"].Value}, not {expected}.";
            }

            int publicTypes = PublicTopLevelTypes(code);
            if (publicTypes > 1)
            {
                yield return $"{where} declares more than one public top-level type.";
            }

            if (shipped && folder.Length > 0 && publicTypes > 0)
            {
                yield return $"{where} declares a public type outside the project's root folder.";
            }
        }
    }

    private static IEnumerable<string> SolutionViolations(string root, IReadOnlyList<string> expected)
    {
        string solution = Path.Combine(root, "Themis.sln");
        if (!File.Exists(solution))
        {
            yield return "Themis.sln is missing.";
            yield break;
        }

        HashSet<string> listed = [.. SolutionProjectPattern().Matches(File.ReadAllText(solution)).Select(match => match.Groups["path"].Value.Replace('\\', '/'))];
        foreach (string project in expected.Where(project => !listed.Contains(project)))
        {
            yield return $"Themis.sln does not list {project}.";
        }

        foreach (string project in listed.Where(project => !expected.Contains(project, StringComparer.Ordinal)))
        {
            yield return $"Themis.sln lists {project}, which is not a package's project or its test project.";
        }
    }

    /// <summary>
    /// Counts the public types declared at the namespace's own level, in a file-scoped
    /// namespace or inside the braces of a block-scoped one, never inside a type.
    /// </summary>
    private static int PublicTopLevelTypes(string code)
    {
        Stack<bool> blocks = new();
        int count = 0;
        foreach (Match token in StructurePattern().Matches(code))
        {
            if (token.Groups["namespace"].Success)
            {
                if (token.Value.EndsWith('{'))
                {
                    blocks.Push(true);
                }
            }
            else if (token.Value == "{")
            {
                blocks.Push(false);
            }
            else if (token.Value == "}")
            {
                _ = blocks.TryPop(out _);
            }
            else if (blocks.All(block => block))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Answers each file of <paramref name="tracked"/>, the paths Git tracks, outside the root
    /// APR-ENG-001 lists, and each listed file missing; <c>RELEASE-NOTES.md</c> comes with the
    /// first release commit.
    /// </summary>
    internal static IEnumerable<string> RootFileViolations(IReadOnlyCollection<string> tracked)
    {
        foreach (string file in RequiredRootFiles.Where(file => !tracked.Contains(file, StringComparer.Ordinal)))
        {
            yield return $"{file} is missing from the repository's root.";
        }

        foreach (string path in tracked.Order(StringComparer.Ordinal))
        {
            string[] parts = path.Split('/');
            if (parts.Length == 1 && !RootFiles.Contains(path, StringComparer.Ordinal))
            {
                yield return $"{path} sits at the repository's root, which holds only the files APR-ENG-001 lists.";
            }
            else if (string.Equals(parts[0], ".config", StringComparison.Ordinal) && !string.Equals(path, ".config/dotnet-tools.json", StringComparison.Ordinal))
            {
                yield return $"{path} sits in .config/, which holds only the tool manifest.";
            }
        }

        foreach (string folder in tracked.Where(path => path.Contains('/', StringComparison.Ordinal)).Select(path => path.Split('/')[0]).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (!RootFolders.Contains(folder, StringComparer.Ordinal))
            {
                yield return $"{folder}/ is a folder at the repository's root that APR-ENG-001 does not list.";
            }
        }
    }

    /// <summary>
    /// Answers each part of the root <c>nuget.config</c> outside APR-ENG-001: the sources
    /// cleared, nuget.org the one source, and every package identifier mapped to it.
    /// </summary>
    internal static IEnumerable<string> PackageSourceViolations(string root)
    {
        string path = Path.Combine(root, "nuget.config");
        if (!File.Exists(path))
        {
            yield return "nuget.config is missing.";
            yield break;
        }

        XElement configuration = XDocument.Load(path).Root!;
        XElement? sources = configuration.Element("packageSources");
        List<XElement> entries = sources is null ? [] : [.. sources.Elements()];
        if (entries.Count == 0 || entries[0].Name.LocalName is not "clear")
        {
            yield return "nuget.config does not clear the package sources first.";
        }

        List<XElement> added = [.. entries.Skip(1)];
        bool nugetOrgAlone = added.Count == 1 && added[0].Name.LocalName is "add"
            && string.Equals(added[0].Attribute("key")?.Value, "nuget.org", StringComparison.Ordinal)
            && string.Equals(added[0].Attribute("value")?.Value, NuGetOrg, StringComparison.Ordinal);
        if (!nugetOrgAlone)
        {
            yield return $"nuget.config does not add nuget.org, {NuGetOrg}, as its one source.";
        }

        List<XElement> mapped = [.. configuration.Element("packageSourceMapping")?.Elements() ?? []];
        bool everyIdentifier = mapped.Count == 1 && mapped[0].Name.LocalName is "packageSource"
            && string.Equals(mapped[0].Attribute("key")?.Value, "nuget.org", StringComparison.Ordinal)
            && mapped[0].Elements().Count() == 1
            && mapped[0].Elements().All(package => package.Name.LocalName is "package" && string.Equals(package.Attribute("pattern")?.Value, "*", StringComparison.Ordinal));
        if (!everyIdentifier)
        {
            yield return "nuget.config does not map every package identifier, \"*\", to nuget.org alone.";
        }

        foreach (XElement section in configuration.Elements().Where(section => section.Name.LocalName is not ("packageSources" or "packageSourceMapping")))
        {
            yield return $"nuget.config holds the section {section.Name.LocalName}, which APR-ENG-001 does not give it.";
        }
    }

    /// <summary>
    /// Answers each part of <c>samples/nuget.config</c> outside APR-ENG-001: the sources
    /// cleared, nuget.org and <c>../artifacts/packages/</c> the two sources, the library's own
    /// package identifiers mapped to the second alone and every other identifier to nuget.org
    /// alone.
    /// </summary>
    internal static IEnumerable<string> SamplePackageSourceViolations(string root)
    {
        const string Config = "samples/nuget.config";
        string path = Path.Combine(root, "samples", "nuget.config");
        if (!File.Exists(path))
        {
            yield return $"{Config} is missing.";
            yield break;
        }

        XElement configuration = XDocument.Load(path).Root!;
        List<XElement> entries = [.. configuration.Element("packageSources")?.Elements() ?? []];
        if (entries.Count == 0 || entries[0].Name.LocalName is not "clear")
        {
            yield return $"{Config} does not clear the package sources first.";
        }

        List<XElement> added = [.. entries.SkipWhile(entry => entry.Name.LocalName is "clear")];
        string? nugetOrg = Source(added, NuGetOrg);
        string? artifacts = Source(added, "../artifacts/packages/");
        if (added.Count != 2 || nugetOrg is null || artifacts is null || added.Any(entry => entry.Name.LocalName is not "add"))
        {
            yield return $"{Config} does not add nuget.org, {NuGetOrg}, and ../artifacts/packages/ as its two sources.";
        }

        Dictionary<string, string[]> mapped = (configuration.Element("packageSourceMapping")?.Elements() ?? [])
            .GroupBy(source => source.Attribute("key")?.Value ?? string.Empty, StringComparer.Ordinal)
            .ToDictionary(source => source.Key, source => source.SelectMany(found => found.Elements()).Select(package => package.Attribute("pattern")?.Value ?? string.Empty).ToArray(), StringComparer.Ordinal);
        bool mappedWithinTheRule = mapped.Count == 2 && nugetOrg is not null && artifacts is not null
            && mapped.TryGetValue(nugetOrg, out string[]? everyOther) && everyOther is ["*"]
            && mapped.TryGetValue(artifacts, out string[]? own) && own.Length == Packages.All.Count && own.ToHashSet(StringComparer.Ordinal).SetEquals(Packages.All);
        if (!mappedWithinTheRule)
        {
            yield return $"{Config} does not map the library's own packages to ../artifacts/packages/ alone and every other identifier, \"*\", to nuget.org alone.";
        }

        foreach (XElement section in configuration.Elements().Where(section => section.Name.LocalName is not ("packageSources" or "packageSourceMapping")))
        {
            yield return $"{Config} holds the section {section.Name.LocalName}, which APR-ENG-001 does not give it.";
        }
    }

    /// <summary>The key of the source of <paramref name="added"/> whose address is <paramref name="address"/>, a folder's with or without its final slash.</summary>
    private static string? Source(IEnumerable<XElement> added, string address)
    {
        return added.FirstOrDefault(entry => string.Equals(entry.Attribute("value")?.Value.TrimEnd('/'), address.TrimEnd('/'), StringComparison.Ordinal))?.Attribute("key")?.Value;
    }

    /// <summary>Answers whether the root <c>.gitignore</c> lists <c>artifacts/packages/</c>, where the gate packs the packages.</summary>
    internal static IEnumerable<string> IgnoreViolations(string root)
    {
        string path = Path.Combine(root, ".gitignore");
        bool lists = File.Exists(path) && File.ReadLines(path).Select(line => line.Trim()).Any(line => line is "artifacts/packages/" or "/artifacts/packages/");
        if (!lists)
        {
            yield return ".gitignore does not list artifacts/packages/.";
        }
    }

    private static IEnumerable<string> SolutionFileViolations(string root)
    {
        IEnumerable<string> solutions = ProjectFile.Enumerate(root, "*.sln").Concat(ProjectFile.Enumerate(root, "*.slnx"));
        foreach (string solution in solutions)
        {
            string relative = Path.GetRelativePath(root, solution).Replace('\\', '/');
            if (relative is not "Themis.sln" and not "samples/Samples.sln")
            {
                yield return $"{relative} is a solution other than Themis.sln and samples/Samples.sln.";
            }
        }
    }

    [GeneratedRegex(@"^\s*namespace\s+(?<name>[\w.]+)", RegexOptions.Multiline)]
    private static partial Regex NamespacePattern();

    /// <summary>A namespace's declaration, a brace, or a public type's declaration.</summary>
    [GeneratedRegex(@"(?<namespace>\bnamespace\s+[\w.]+\s*[{;])|[{}]|\bpublic\s+(?:(?:abstract|sealed|static|partial|readonly|ref|unsafe|new)\s+)*(?:class|record|struct|interface|enum|delegate)\b")]
    private static partial Regex StructurePattern();

    [GeneratedRegex(@"^Project\(""\{[0-9A-F-]+\}""\)\s*=\s*""[^""]+"",\s*""(?<path>[^""]+\.csproj)""", RegexOptions.Multiline)]
    private static partial Regex SolutionProjectPattern();
}