using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Themis.Repository.Tests;

/// <summary>
/// The shared build settings of <c>05</c> APR-ENG-002: one <c>Directory.Build.props</c> that
/// no project overrides, package versions in <c>Directory.Packages.props</c> alone, the SDK
/// in <c>global.json</c>, and no warning turned off or kept from failing the build but by the
/// root <c>.editorconfig</c>.
/// </summary>
internal static partial class SettingRules
{
    /// <summary>The properties <c>Directory.Build.props</c> sets once and no project overrides.</summary>
    internal static FrozenDictionary<string, string> Settings { get; } = new Dictionary<string, string>
    {
        ["Nullable"] = "enable",
        ["ImplicitUsings"] = "disable",
        ["TreatWarningsAsErrors"] = "true",
        ["MSBuildTreatWarningsAsErrors"] = "true",
        ["AnalysisLevel"] = "latest-all",
        ["EnforceCodeStyleInBuild"] = "true",
        ["Deterministic"] = "true",
        ["GenerateDocumentationFile"] = "true",
        ["EnableConfigurationBindingGenerator"] = "true",
        ["NuGetAudit"] = "true",
        ["NuGetAuditMode"] = "all",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The properties that turn a warning off or keep it from failing the build, whatever their value.</summary>
    internal static FrozenSet<string> WarningProperties { get; } = FrozenSet.Create(
        StringComparer.Ordinal,
        "NoWarn",
        "WarningsNotAsErrors",
        "WarningLevel",
        "MSBuildWarningsAsMessages",
        "MSBuildWarningsNotAsErrors",
        "RunAnalyzers",
        "RunAnalyzersDuringBuild",
        "EnableNETAnalyzers",
        "CodeAnalysisRuleSet",
        "NuGetAuditLevel",
        "AnalysisMode");

    /// <summary>The items that do so: an audit suppression and an analyser configuration file.</summary>
    internal static FrozenSet<string> WarningItems { get; } =
        FrozenSet.Create(StringComparer.Ordinal, "NuGetAuditSuppress", "EditorConfigFiles", "GlobalAnalyzerConfigFiles");

    private const string BuildProps = "Directory.Build.props";

    private const string PackagesProps = "Directory.Packages.props";

    /// <summary>Answers each setting of the repository at <paramref name="root"/> outside APR-ENG-002.</summary>
    internal static IReadOnlyList<string> SettingViolations(string root)
    {
        List<string> violations = [];
        foreach (ProjectFile project in ProjectFile.ReadAll(root))
        {
            foreach (string property in project.Properties.Where(property => property is "TargetFramework" or "TargetFrameworks" || Settings.ContainsKey(property)))
            {
                violations.Add($"{project.RelativePath} sets {property}, which only Directory.Build.props sets.");
            }

            foreach (XElement reference in project.PackageReferences.Where(reference => reference.Attribute("Version") is not null || reference.Attribute("VersionOverride") is not null || reference.Element("Version") is not null))
            {
                violations.Add($"{project.RelativePath} sets the version of {ProjectFile.Include(reference)}, which only Directory.Packages.props sets.");
            }

            violations.AddRange(WarningViolations(project.RelativePath, project.Document));

            bool samples = project.RelativePath.StartsWith("samples/", StringComparison.Ordinal);
            if (!samples && !File.Exists(Path.Combine(project.Folder, "packages.lock.json")))
            {
                violations.Add($"{project.RelativePath} has no packages.lock.json.");
            }
        }

        violations.AddRange(BuildPropsViolations(root));
        violations.AddRange(PackagesPropsViolations(root));
        violations.AddRange(SharedFileViolations(root));
        violations.AddRange(SdkViolations(root));
        violations.AddRange(ConfigurationFileViolations(root));
        return violations;
    }

    /// <summary>
    /// Answers each property, item or item metadata of a project or shared build file that
    /// turns a warning off or keeps it from failing the build, wherever in the file it is set.
    /// </summary>
    internal static IEnumerable<string> WarningViolations(string relativePath, XDocument document)
    {
        foreach (XElement property in document.Descendants("PropertyGroup").Elements())
        {
            string name = property.Name.LocalName;
            if (WarningProperties.Contains(name) || CategoryPattern().IsMatch(name))
            {
                yield return $"{relativePath} sets {name}, which turns a warning off or keeps it from failing the build.";
            }
        }

        foreach (XElement item in document.Descendants("ItemGroup").Elements())
        {
            string name = item.Name.LocalName;
            if (WarningItems.Contains(name))
            {
                yield return $"{relativePath} adds a {name} item, which turns a warning off or keeps it from failing the build.";
            }

            if (item.Attribute("NoWarn") is not null || item.Element("NoWarn") is not null)
            {
                string target = item.Attribute("Include")?.Value ?? item.Attribute("Update")?.Value ?? string.Empty;
                yield return $"{relativePath} sets NoWarn on its {name} item {target}, which turns a warning off.";
            }
        }
    }

    private static IEnumerable<string> BuildPropsViolations(string root)
    {
        string path = Path.Combine(root, BuildProps);
        if (!File.Exists(path))
        {
            yield return "Directory.Build.props is missing.";
            yield break;
        }

        XDocument document = XDocument.Load(path);
        Dictionary<string, string> set = document.Root!.Elements("PropertyGroup").Elements()
            .GroupBy(element => element.Name.LocalName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.Ordinal);
        foreach ((string property, string value) in Settings.Append(new KeyValuePair<string, string>("TargetFramework", "net10.0")))
        {
            if (!set.TryGetValue(property, out string? actual) || !string.Equals(actual, value, StringComparison.Ordinal))
            {
                yield return $"Directory.Build.props does not set {property} to {value}.";
            }
        }

        foreach (string violation in WarningViolations(BuildProps, document))
        {
            yield return violation;
        }
    }

    private static IEnumerable<string> PackagesPropsViolations(string root)
    {
        string path = Path.Combine(root, PackagesProps);
        if (!File.Exists(path))
        {
            yield return "Directory.Packages.props is missing.";
            yield break;
        }

        XDocument document = XDocument.Load(path);
        foreach (XElement property in document.Descendants("PropertyGroup").Elements().Where(property => property.Name.LocalName is not "ManagePackageVersionsCentrally"))
        {
            yield return $"Directory.Packages.props sets {property.Name.LocalName}; it sets no property but ManagePackageVersionsCentrally.";
        }

        foreach (string violation in WarningViolations(PackagesProps, document))
        {
            yield return violation;
        }
    }

    private static IEnumerable<string> SharedFileViolations(string root)
    {
        foreach (string other in ProjectFile.Enumerate(root, "Directory.*.props").Concat(ProjectFile.Enumerate(root, "Directory.*.targets")))
        {
            string relative = Path.GetRelativePath(root, other).Replace('\\', '/');
            if (relative is not BuildProps and not PackagesProps)
            {
                yield return $"{relative} sets what only the root's Directory.Build.props and Directory.Packages.props set.";
            }
        }
    }

    private static IEnumerable<string> SdkViolations(string root)
    {
        string global = Path.Combine(root, "global.json");
        if (!File.Exists(global))
        {
            yield return "global.json is missing.";
            yield break;
        }

        using JsonDocument sdk = JsonDocument.Parse(File.ReadAllText(global));
        JsonElement settings = sdk.RootElement.GetProperty("sdk");
        if (!settings.TryGetProperty("version", out JsonElement version) || !(version.GetString() ?? string.Empty).StartsWith("10.0.", StringComparison.Ordinal))
        {
            yield return "global.json does not name a .NET 10 SDK.";
        }

        if (!settings.TryGetProperty("rollForward", out JsonElement rollForward) || !string.Equals(rollForward.GetString(), "latestPatch", StringComparison.Ordinal))
        {
            yield return "global.json does not set rollForward to latestPatch.";
        }
    }

    /// <summary>
    /// Answers each <c>.editorconfig</c> or <c>.globalconfig</c> of the working tree but the
    /// root <c>.editorconfig</c>, outside <c>tmp/</c> and build output.
    /// </summary>
    private static IEnumerable<string> ConfigurationFileViolations(string root)
    {
        IEnumerable<string> files = ProjectFile.EnumerateWorkingTree(root, ".editorconfig").Concat(ProjectFile.EnumerateWorkingTree(root, "*.globalconfig"));
        foreach (string file in files)
        {
            string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (relative is not ".editorconfig")
            {
                yield return $"{relative} configures the analysers; only the root .editorconfig does.";
            }
        }
    }

    /// <summary>The <c>AnalysisLevel</c> or <c>AnalysisMode</c> of one category of rules, as <c>AnalysisModeSecurity</c>.</summary>
    [GeneratedRegex(@"^Analysis(Level|Mode)\p{Lu}\w*$")]
    private static partial Regex CategoryPattern();
}