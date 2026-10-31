using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

namespace Themis.Repository.Tests;

/// <summary>
/// The third-party packages of <c>05</c> APR-ENG-009, the local tools, the packages no
/// test may use (APR-ENG-008), and the packed packages' properties (APR-ENG-011).
/// </summary>
internal static class PackageRules
{
    /// <summary>The packages of APR-ENG-009's table.</summary>
    internal static IReadOnlyList<string> Allowed { get; } =
    [
        .. ReferenceRules.DataAccess,
        ReferenceRules.EntityFrameworkCoreDesign,
        .. ReferenceRules.HostingAbstractions,
        ReferenceRules.PublicApiAnalyzers,
        ReferenceRules.MinVer,
        .. ReferenceRules.TestPackages,
    ];

    /// <summary>Mocking packages, none of which a project may take, directly or not.</summary>
    internal static IReadOnlyList<string> Mocking { get; } =
        ["Moq", "NSubstitute", "FakeItEasy", "RhinoMocks", "Rhino.Mocks", "JustMock", "Telerik.JustMock", "NMock", "NMock3", "Typemock.Isolator", "Microsoft.QualityTools.Testing.Fakes"];

    /// <summary>The elements of a packed package's metadata: its identifier, version, authors, description, repository and dependencies.</summary>
    private static readonly FrozenSet<string> ManifestElements =
        FrozenSet.Create(StringComparer.Ordinal, "id", "version", "authors", "description", "repository", "dependencies");

    /// <summary>The element <c>Themis.Cli</c> alone carries, its package type as a .NET tool, which the SDK writes for a tool package.</summary>
    private const string ToolPackageType = "packageTypes";

    private static bool IsToolPackageType(PackageManifest manifest, string element)
    {
        return string.Equals(element, ToolPackageType, StringComparison.Ordinal) && string.Equals(manifest.Id, Packages.Cli, StringComparison.Ordinal);
    }

    /// <summary>
    /// Answers each dependency of a packed package on another of the library's packages that is
    /// not the exact version packed, <c>[v]</c> (<c>03</c> APR-LIB-001 criterion 2).
    /// </summary>
    internal static IEnumerable<string> DependencyViolations(IReadOnlyList<PackageManifest> manifests)
    {
        ArgumentNullException.ThrowIfNull(manifests);
        Dictionary<string, string> packed = manifests.ToDictionary(manifest => manifest.Id, manifest => manifest.Version, StringComparer.Ordinal);
        foreach (PackageManifest manifest in manifests)
        {
            foreach (PackageDependency dependency in manifest.Dependencies.Where(dependency => packed.ContainsKey(dependency.Id) || Packages.All.Contains(dependency.Id, StringComparer.Ordinal)))
            {
                if (!packed.TryGetValue(dependency.Id, out string? version))
                {
                    yield return $"{manifest.Id} depends on {dependency.Id}, which was not packed with it.";
                }
                else if (!string.Equals(dependency.Version, $"[{version}]", StringComparison.Ordinal))
                {
                    yield return $"{manifest.Id} depends on {dependency.Id} at {dependency.Version}, not the exact version packed, [{version}].";
                }
            }
        }
    }

    /// <summary>
    /// Answers each property of a packed package outside <c>05</c> APR-ENG-011: its description
    /// is its cell of <c>00</c> section 2.2, its authors and repository are those the lead's
    /// first message of phase 0 gives, and it carries no other package property.
    /// </summary>
    internal static IEnumerable<string> ManifestViolations(PackageManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        foreach (string element in manifest.Elements.Where(element => !ManifestElements.Contains(element) && !IsToolPackageType(manifest, element)))
        {
            yield return $"{manifest.Id} carries the package property {element}, which is left unset.";
        }

        if (!string.Equals(manifest.Authors, Packages.Authors, StringComparison.Ordinal))
        {
            yield return $"{manifest.Id}'s authors are {manifest.Authors}, not {Packages.Authors}.";
        }

        if (!PackageTable.Descriptions.TryGetValue(manifest.Id, out string? description) || !string.Equals(manifest.Description, description, StringComparison.Ordinal))
        {
            yield return $"{manifest.Id}'s description is not its cell of 00 section 2.2.";
        }

        if (!string.Equals(manifest.RepositoryUrl, Packages.RepositoryUrl, StringComparison.Ordinal))
        {
            yield return $"{manifest.Id}'s repository is {manifest.RepositoryUrl}, not {Packages.RepositoryUrl}.";
        }
    }

    /// <summary>Answers each package or tool of the repository at <paramref name="root"/> outside APR-ENG-009.</summary>
    internal static IReadOnlyList<string> AllowListViolations(string root)
    {
        List<string> violations = [];
        foreach (ProjectFile project in ProjectFile.ReadAll(root))
        {
            // A reference made outside the project file reaches its lockfile's direct entries.
            IEnumerable<string> direct = project.PackageReferences.Select(ProjectFile.Include).Union(project.LockedDirectPackages, StringComparer.Ordinal);
            foreach (string package in direct.Where(package => !IsAllowed(package)))
            {
                violations.Add($"{project.RelativePath} references {package}, which is not in APR-ENG-009's table.");
            }
        }

        Dictionary<string, string> versions = PackageVersions(root);
        foreach (string package in versions.Keys.Where(package => !IsAllowed(package)))
        {
            violations.Add($"Directory.Packages.props names {package}, which is not in APR-ENG-009's table.");
        }

        violations.AddRange(ToolViolations(root, versions));
        return violations;
    }

    /// <summary>
    /// A package of APR-ENG-009's table, or one of the library's own, which are no third-party
    /// packages; which project may take which is <c>03</c> APR-LIB-001's table.
    /// </summary>
    private static bool IsAllowed(string package)
    {
        return Allowed.Contains(package, StringComparer.Ordinal) || Packages.All.Contains(package, StringComparer.Ordinal);
    }

    /// <summary>Answers each mocking package a project of the repository at <paramref name="root"/> takes, directly or through another.</summary>
    internal static IReadOnlyList<string> MockingViolations(string root)
    {
        List<string> violations = [];
        foreach (ProjectFile project in ProjectFile.ReadAll(root))
        {
            IEnumerable<string> taken = project.PackageReferences.Select(ProjectFile.Include);
            string lockFile = Path.Combine(project.Folder, "packages.lock.json");
            if (File.Exists(lockFile))
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(lockFile));
                taken = taken.Concat([.. document.RootElement.GetProperty("dependencies").EnumerateObject().SelectMany(framework => framework.Value.EnumerateObject().Select(package => package.Name))]);
            }

            foreach (string package in taken.Distinct(StringComparer.OrdinalIgnoreCase).Where(package => Mocking.Contains(package, StringComparer.OrdinalIgnoreCase)))
            {
                violations.Add($"{project.RelativePath} takes the mocking package {package}.");
            }
        }

        return violations;
    }

    /// <summary>
    /// Answers each tool of the manifest but <c>dotnet-ef</c>, and <c>dotnet-ef</c> at a version
    /// other than that of each EF Core package <c>Directory.Packages.props</c> names.
    /// </summary>
    private static IEnumerable<string> ToolViolations(string root, Dictionary<string, string> versions)
    {
        string manifest = Path.Combine(root, ".config", "dotnet-tools.json");
        if (!File.Exists(manifest))
        {
            yield return ".config/dotnet-tools.json is missing.";
            yield break;
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifest));
        List<JsonProperty> tools = [.. document.RootElement.GetProperty("tools").EnumerateObject()];
        foreach (JsonProperty tool in tools.Where(tool => !string.Equals(tool.Name, "dotnet-ef", StringComparison.Ordinal)))
        {
            yield return $"The tool manifest names {tool.Name}; it names dotnet-ef alone.";
        }

        JsonProperty? entityFramework = tools.Cast<JsonProperty?>().FirstOrDefault(tool => string.Equals(tool!.Value.Name, "dotnet-ef", StringComparison.Ordinal));
        if (entityFramework is null)
        {
            yield return "The tool manifest does not name dotnet-ef.";
            yield break;
        }

        string? toolVersion = entityFramework.Value.Value.GetProperty("version").GetString();
        IEnumerable<KeyValuePair<string, string>> entityFrameworkCore = versions
            .Where(version => version.Key.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal))
            .OrderBy(version => version.Key, StringComparer.Ordinal);
        foreach ((string package, string version) in entityFrameworkCore.Where(version => !string.Equals(version.Value, toolVersion, StringComparison.Ordinal)))
        {
            yield return $"The tool manifest takes dotnet-ef {toolVersion}, not the EF Core version {version} of {package}.";
        }
    }

    private static Dictionary<string, string> PackageVersions(string root)
    {
        string props = Path.Combine(root, "Directory.Packages.props");
        if (!File.Exists(props))
        {
            return [];
        }

        return XDocument.Load(props).Root!.Elements("ItemGroup").Elements("PackageVersion")
            .ToDictionary(ProjectFile.Include, version => version.Attribute("Version")?.Value ?? string.Empty, StringComparer.Ordinal);
    }
}