using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Themis.Repository.Tests;

/// <summary>
/// What each project may reference (<c>03</c> APR-LIB-001) and the grants of
/// <c>InternalsVisibleTo</c> (<c>05</c> APR-ENG-001).
/// </summary>
internal static class ReferenceRules
{
    internal const string PublicApiAnalyzers = "Microsoft.CodeAnalysis.PublicApiAnalyzers";

    internal const string MinVer = "MinVer";

    internal const string EntityFrameworkCoreDesign = "Microsoft.EntityFrameworkCore.Design";

    /// <summary>EF Core, its relational layer and the Npgsql provider (<c>05</c> APR-ENG-009).</summary>
    internal static IReadOnlyList<string> DataAccess { get; } =
        ["Microsoft.EntityFrameworkCore", "Microsoft.EntityFrameworkCore.Relational", "Npgsql.EntityFrameworkCore.PostgreSQL"];

    /// <summary>The Microsoft.Extensions abstractions of <c>05</c> APR-ENG-009.</summary>
    internal static IReadOnlyList<string> HostingAbstractions { get; } =
    [
        "Microsoft.Extensions.DependencyInjection.Abstractions",
        "Microsoft.Extensions.Configuration.Abstractions",
        "Microsoft.Extensions.Configuration.Binder",
        "Microsoft.Extensions.Options",
        "Microsoft.Extensions.Options.ConfigurationExtensions",
        "Microsoft.Extensions.Logging.Abstractions",
        "Microsoft.Extensions.Hosting.Abstractions",
        "Microsoft.Extensions.Diagnostics.HealthChecks",
    ];

    /// <summary>The test packages of <c>05</c> APR-ENG-009.</summary>
    internal static IReadOnlyList<string> TestPackages { get; } = ["xunit.v3", "Testcontainers.PostgreSql", "Microsoft.AspNetCore.Mvc.Testing"];

    /// <summary>ASP.NET Core's shared framework, which the sample host's row alone allows.</summary>
    internal const string AspNetCore = "Microsoft.AspNetCore.App";

    /// <summary>The library's own projects each package's project may reference.</summary>
    private static readonly FrozenDictionary<string, IReadOnlyList<string>> AllowedProjects = new Dictionary<string, IReadOnlyList<string>>
    {
        [Packages.Core] = [],
        [Packages.Engine] = [Packages.Core],
        [Packages.Testing] = [Packages.Core, Packages.Engine],
        [Packages.Cli] = [Packages.Engine],
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The packages each package's project may reference at run time.</summary>
    private static readonly FrozenDictionary<string, IReadOnlyList<string>> AllowedPackages = new Dictionary<string, IReadOnlyList<string>>
    {
        [Packages.Core] = [],
        [Packages.Engine] = [.. DataAccess, .. HostingAbstractions],
        [Packages.Testing] = [],
        [Packages.Cli] = [],
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Answers each reference of a project of the repository at <paramref name="root"/> that APR-LIB-001 does not allow.</summary>
    internal static IReadOnlyList<string> ReferenceViolations(string root)
    {
        List<string> violations = [];

        foreach (ProjectFile project in ProjectFile.ReadAll(root))
        {
            bool shipped = project.RelativePath.StartsWith("src/", StringComparison.Ordinal);
            bool test = project.RelativePath.StartsWith("tests/", StringComparison.Ordinal);
            bool sample = IsSample(project);
            if (!shipped && !test && !sample)
            {
                continue;
            }

            IReadOnlyList<string> allowedProjects = sample ? AllowedSampleProjects(project.Name) : shipped ? AllowedProjects.GetValueOrDefault(project.Name, []) : AllowedTestProjects(project.Name);
            IReadOnlyList<string> allowedPackages = sample ? AllowedSamplePackages(project.Name) : shipped ? AllowedPackages.GetValueOrDefault(project.Name, []) : TestPackages;

            foreach (string reference in project.ProjectReferences.Where(reference => !allowedProjects.Contains(reference, StringComparer.Ordinal)))
            {
                violations.Add($"{project.RelativePath} references the project {reference}, which APR-LIB-001 does not allow it.");
            }

            List<string> buildOnly = [];
            foreach (XElement reference in project.PackageReferences)
            {
                string package = ProjectFile.Include(reference);
                if (IsBuildOnly(project, package))
                {
                    buildOnly.Add(package);
                    if (!string.Equals(reference.Attribute("PrivateAssets")?.Value, "all", StringComparison.Ordinal))
                    {
                        violations.Add($"{project.RelativePath} references {package} without PrivateAssets=\"all\", though it is build-only.");
                    }
                }
                else if (!allowedPackages.Contains(package, StringComparer.Ordinal))
                {
                    violations.Add($"{project.RelativePath} references the package {package}, which APR-LIB-001 does not allow it.");
                }
            }

            HashSet<string> referenced = [.. project.PackageReferences.Select(ProjectFile.Include)];
            foreach (string package in project.LockedDirectPackages.Where(package => !referenced.Contains(package)))
            {
                violations.Add($"{project.RelativePath} takes the package {package} by a reference outside its project file, which APR-LIB-001 does not allow it.");
            }

            if (shipped)
            {
                foreach (string required in RequiredBuildOnly(project.Name).Where(required => !buildOnly.Contains(required, StringComparer.Ordinal)))
                {
                    violations.Add($"{project.RelativePath} does not reference {required} as a build-only reference.");
                }
            }

            // No row of the table gives a project of src/ or tests/ a shared framework, since no
            // shipped project references ASP.NET Core; the sample host's row gives its two
            // projects ASP.NET Core's alone.
            foreach (string framework in project.FrameworkReferences.Where(framework => !sample || !string.Equals(framework, AspNetCore, StringComparison.Ordinal)))
            {
                violations.Add($"{project.RelativePath} references the framework {framework}, which APR-LIB-001 does not allow it.");
            }
        }

        violations.AddRange(SharedItemViolations(root));
        return violations;
    }

    /// <summary>
    /// Answers each item of <c>Directory.Build.props</c> but the CLSCompliant attribute and the
    /// exact-version target's update (<c>05</c> APR-ENG-011), and each item of
    /// <c>Directory.Packages.props</c> but a <c>PackageVersion</c>, since such an item would reach
    /// every project unseen.
    /// </summary>
    private static IEnumerable<string> SharedItemViolations(string root)
    {
        string buildProps = Path.Combine(root, "Directory.Build.props");
        if (File.Exists(buildProps))
        {
            foreach (XElement item in XDocument.Load(buildProps).Descendants("ItemGroup").Elements())
            {
                string name = item.Name.LocalName;
                XElement? owner = item.Parent!.Parent;
                bool attribute = owner == item.Document!.Root && string.Equals(name, "AssemblyAttribute", StringComparison.Ordinal)
                    && string.Equals(item.Attribute("Include")?.Value, "System.CLSCompliantAttribute", StringComparison.Ordinal);
                bool exactVersion = owner is { Name.LocalName: "Target" } && string.Equals(owner.Attribute("Name")?.Value, "DependOnTheExactVersionPacked", StringComparison.Ordinal)
                    && string.Equals(name, "_ProjectReferencesWithVersions", StringComparison.Ordinal);
                if (!attribute && !exactVersion)
                {
                    yield return $"Directory.Build.props adds a {name} item {Target(item)}; it adds no item but the CLSCompliant attribute.";
                }
            }
        }

        string packagesProps = Path.Combine(root, "Directory.Packages.props");
        if (File.Exists(packagesProps))
        {
            foreach (XElement item in XDocument.Load(packagesProps).Descendants("ItemGroup").Elements().Where(item => item.Name.LocalName is not "PackageVersion"))
            {
                yield return $"Directory.Packages.props adds a {item.Name.LocalName} item {Target(item)}; it adds no item but PackageVersion.";
            }
        }
    }

    private static string Target(XElement item)
    {
        return item.Attribute("Include")?.Value ?? item.Attribute("Update")?.Value ?? string.Empty;
    }

    /// <summary>
    /// Answers each grant of <c>InternalsVisibleTo</c> outside <c>05</c> APR-ENG-001's rule that
    /// the built assembly of a project of the repository at <paramref name="root"/> carries,
    /// whatever form declared it: an item, an attribute item or an attribute in a source file.
    /// The projects of the sample host's solution, which <c>Themis.sln</c> does not build and
    /// the rule lets grant nothing, are read from their project files and sources instead.
    /// </summary>
    internal static IReadOnlyList<string> GrantViolations(string root)
    {
        List<string> violations = [];
        foreach (ProjectFile project in ProjectFile.ReadAll(root))
        {
            if (IsSample(project))
            {
                if (DeclaresGrant(project))
                {
                    violations.Add($"{project.RelativePath} grants its internals, which APR-ENG-001 allows no project of the sample host's solution.");
                }

                continue;
            }

            string assembly = Path.Combine(project.Folder, "bin", "Release", "net10.0", project.Name + ".dll");
            if (!File.Exists(assembly))
            {
                violations.Add($"{project.RelativePath} has no built assembly at bin/Release/net10.0/, so its grants cannot be read.");
                continue;
            }

            violations.AddRange(GrantViolations(project.RelativePath, project.Name, AssemblySurface.Grants(assembly)));
        }

        return violations;
    }

    /// <summary>Answers each of <paramref name="grantees"/> the project at <paramref name="relativePath"/> may not grant its internals to.</summary>
    internal static IEnumerable<string> GrantViolations(string relativePath, string name, IEnumerable<string> grantees)
    {
        return grantees.Where(grantee => !IsAllowedGrant(relativePath, name, grantee))
            .Select(grantee => $"{relativePath} grants its internals to {grantee}, which APR-ENG-001 does not allow.");
    }

    /// <summary>
    /// A source project grants its own test project, and <c>Themis</c> grants <c>Themis.Cli</c>,
    /// which applies the migrations it holds, and <c>Themis.Testing</c>, which removes the hosted
    /// jobs by their types and applies the migrations for a test.
    /// </summary>
    private static bool IsAllowedGrant(string relativePath, string name, string grantee)
    {
        if (!relativePath.StartsWith("src/", StringComparison.Ordinal))
        {
            return false;
        }

        if (string.Equals(grantee, Packages.TestProject(name), StringComparison.Ordinal))
        {
            return true;
        }

        string[] allowed = string.Equals(name, Packages.Engine, StringComparison.Ordinal) ? [Packages.Cli, Packages.Testing] : [];
        return allowed.Contains(grantee, StringComparer.Ordinal);
    }

    private static bool IsSample(ProjectFile project)
    {
        return project.RelativePath.StartsWith("samples/", StringComparison.Ordinal);
    }

    /// <summary>The sample host references no project, and its test project the sample host alone, the project it tests.</summary>
    private static IReadOnlyList<string> AllowedSampleProjects(string name)
    {
        return name.EndsWith(".Tests", StringComparison.Ordinal) ? [name[..^".Tests".Length]] : [];
    }

    /// <summary>The library's packed packages, and for the sample host's test project the test packages too.</summary>
    private static IReadOnlyList<string> AllowedSamplePackages(string name)
    {
        return name.EndsWith(".Tests", StringComparison.Ordinal) ? [.. Packages.All, .. TestPackages] : Packages.All;
    }

    /// <summary>Whether the project file, or a source file of the project outside its build output, declares <c>InternalsVisibleTo</c> in any form.</summary>
    private static bool DeclaresGrant(ProjectFile project)
    {
        IEnumerable<string> sources = Directory.EnumerateFiles(project.Folder, "*.cs", SearchOption.AllDirectories)
            .Where(file => Path.GetRelativePath(project.Folder, file).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0] is not ("bin" or "obj"));
        return sources.Append(project.Path).Any(file => File.ReadAllText(file).Contains("InternalsVisibleTo", StringComparison.Ordinal));
    }

    private static IReadOnlyList<string> AllowedTestProjects(string testProject)
    {
        // The repository's tests read every shipped project's built assembly (05 APR-ENG-004).
        if (string.Equals(testProject, Packages.RepositoryTests, StringComparison.Ordinal))
        {
            return Packages.All;
        }

        string source = testProject.EndsWith(".Tests", StringComparison.Ordinal) ? testProject[..^".Tests".Length] : testProject;
        return [source, Packages.Testing];
    }

    /// <summary>
    /// The build-only references of a shipped project: the public API analysers and MinVer for
    /// every one, since each is packed, and EF Core's design package for <c>Themis</c>, which
    /// makes the migrations.
    /// </summary>
    private static IReadOnlyList<string> RequiredBuildOnly(string name)
    {
        return string.Equals(name, Packages.Engine, StringComparison.Ordinal) ? [PublicApiAnalyzers, MinVer, EntityFrameworkCoreDesign] : [PublicApiAnalyzers, MinVer];
    }

    private static bool IsBuildOnly(ProjectFile project, string package)
    {
        bool shipped = project.RelativePath.StartsWith("src/", StringComparison.Ordinal);
        return shipped && RequiredBuildOnly(project.Name).Contains(package, StringComparer.Ordinal);
    }
}