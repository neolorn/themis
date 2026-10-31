using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Xunit;

namespace Themis.Repository.Tests;

[Trait("Category", "unit")]
public sealed class RepositoryLayoutTests
{
    private static readonly IReadOnlyList<string> RootFiles =
        ["Themis.sln", "Directory.Build.props", "Directory.Packages.props", "global.json", ".editorconfig", ".gitignore", "nuget.config", "CHANGELOG.md", ".config/dotnet-tools.json"];

    [Fact]
    public void APR_ENG_001_AC1_ProjectsFollowTheLayout()
    {
        Assert.Empty(LayoutRules.ProjectViolations(RepositoryRoot.Path));
    }

    [Fact]
    public void APR_ENG_001_AC1_ProjectFoldersFollowTheLayout()
    {
        Assert.Empty(LayoutRules.FolderViolations(RepositoryRoot.Path));
    }

    [Fact]
    public void APR_ENG_001_AC1_ProjectOutsideTheLayoutIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject("src/Themis.Extras/Themis.Extras.csproj", string.Empty);

        Assert.Contains(LayoutRules.ProjectViolations(fixture.Root), violation => violation.StartsWith("src/Themis.Extras/Themis.Extras.csproj is not", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("tests/Themis.Extras.Tests/Themis.Extras.Tests.csproj", "tests/Themis.Extras.Tests is the test project of no source project.")]
    [InlineData("tools/Themis.Tools/Themis.Tools.csproj", "tools/Themis.Tools/Themis.Tools.csproj is not a package's project at src/<Package>/ or a test project at tests/<Project>/.")]
    [InlineData("src/Themis.Core/Nested/Nested.csproj", "src/Themis.Core/Nested/Nested.csproj is not a package's project at src/<Package>/ or a test project at tests/<Project>/.")]
    public void APR_ENG_001_AC1_ProjectOfNoPlaceIsFound(string path, string expected)
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject(path, string.Empty);

        Assert.Contains(expected, LayoutRules.ProjectViolations(fixture.Root));
    }

    [Fact]
    public void APR_ENG_001_AC1_SourceProjectWithoutItsTestProjectIsFound()
    {
        using RepositoryFixture fixture = new();
        File.Delete(Path.Combine(fixture.Root, "tests", "Themis.Testing.Tests", "Themis.Testing.Tests.csproj"));

        Assert.Contains($"src/{Packages.Testing} has no test project tests/{Packages.Testing}.Tests.", LayoutRules.ProjectViolations(fixture.Root));
    }

    [Fact]
    public void APR_ENG_001_AC1_RepositoryWithoutItsRepositoryTestsIsFound()
    {
        using RepositoryFixture fixture = new();
        File.Delete(Path.Combine(fixture.Root, "tests", Packages.RepositoryTests, Packages.RepositoryTests + ".csproj"));

        Assert.Contains($"tests/{Packages.RepositoryTests} is missing.", LayoutRules.ProjectViolations(fixture.Root));
    }

    [Theory]
    [InlineData(Packages.Core)]
    [InlineData(Packages.Engine)]
    [InlineData(Packages.Testing)]
    public void APR_ENG_001_AC1_PackageWithoutItsProjectIsFound(string package)
    {
        using RepositoryFixture fixture = new();
        File.Delete(Path.Combine(fixture.Root, "src", package, package + ".csproj"));

        Assert.Contains($"src/{package} is missing, though {package} is a package of 00 section 2.2.", LayoutRules.ProjectViolations(fixture.Root));
    }

    [Fact]
    public void APR_ENG_001_AC1_CommandLinePackageIsNotYetRequired()
    {
        using RepositoryFixture fixture = new();

        Assert.DoesNotContain(LayoutRules.ProjectViolations(fixture.Root), violation => violation.Contains(Packages.Cli, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Other.sln")]
    [InlineData("Themis.slnx")]
    [InlineData("tests/Tests.sln")]
    [InlineData("samples/Other.sln")]
    public void APR_ENG_001_AC1_SolutionOtherThanTheTwoIsFound(string path)
    {
        using RepositoryFixture fixture = new();
        fixture.Write(path, string.Empty);

        Assert.Contains($"{path} is a solution other than Themis.sln and samples/Samples.sln.", LayoutRules.ProjectViolations(fixture.Root));
    }

    [Fact]
    public void APR_ENG_001_AC1_SampleSolutionIsAccepted()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("samples/Samples.sln", string.Empty);

        Assert.Empty(LayoutRules.ProjectViolations(fixture.Root));
    }

    [Fact]
    public void APR_ENG_001_AC1_ProjectTheSolutionDoesNotListIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteSolution(["src/Themis.Core/Themis.Core.csproj"]);

        Assert.Contains("Themis.sln does not list src/Themis/Themis.csproj.", LayoutRules.ProjectViolations(fixture.Root));
    }

    [Fact]
    public void APR_ENG_001_AC1_ProjectTheSolutionListsOutsideTheLayoutIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteSolution([.. ProjectFile.ReadAll(fixture.Root).Select(project => project.RelativePath), "samples/Sample/Sample.csproj"]);

        Assert.Contains("Themis.sln lists samples/Sample/Sample.csproj, which is not a package's project or its test project.", LayoutRules.ProjectViolations(fixture.Root));
    }

    [Theory]
    [InlineData("public sealed class Request;\n")]
    [InlineData("// namespace Themis.Core;\n\npublic sealed class Request;\n")]
    [InlineData("[assembly: System.CLSCompliant(false)]\n")]
    public void APR_ENG_001_AC1_FileWithoutANamespaceIsFound(string source)
    {
        using RepositoryFixture fixture = new();
        fixture.Write("src/Themis.Core/Request.cs", source);

        Assert.Contains("src/Themis.Core/Request.cs declares no namespace.", LayoutRules.FolderViolations(fixture.Root));
    }

    [Fact]
    public void APR_ENG_001_AC1_TwoPublicTypesInOneFileAreFound()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("src/Themis.Core/Pair.cs", "namespace Themis.Core;\n\npublic sealed class One;\n\npublic sealed class Two;\n");

        Assert.Contains("src/Themis.Core/Pair.cs declares more than one public top-level type.", LayoutRules.FolderViolations(fixture.Root));
    }

    [Theory]
    [InlineData("namespace Themis.Core\n{\n    public sealed class One;\n\n    public sealed class Two;\n}\n")]
    [InlineData("namespace Themis.Core\n{\n    /// <summary>One.</summary>\n    public sealed class One\n    {\n    }\n\n    public enum Two\n    {\n        First,\n    }\n}\n")]
    public void APR_ENG_001_AC1_TwoPublicTypesInABlockScopedNamespaceAreFound(string source)
    {
        using RepositoryFixture fixture = new();
        fixture.Write("src/Themis.Core/Pair.cs", source);

        Assert.Contains("src/Themis.Core/Pair.cs declares more than one public top-level type.", LayoutRules.FolderViolations(fixture.Root));
    }

    [Theory]
    [InlineData("namespace Themis.Core;\n\npublic abstract record ApproverReference\n{\n    public sealed record Permission(string Name) : ApproverReference;\n\n    public sealed record Person(string Reference) : ApproverReference;\n}\n")]
    [InlineData("namespace Themis.Core\n{\n    public abstract record ApproverReference\n    {\n        public sealed record Permission(string Name) : ApproverReference;\n    }\n}\n")]
    [InlineData("namespace Themis.Core;\n\n// public sealed class Commented;\n\npublic sealed class One\n{\n    private const string Text = \"public sealed class Quoted\";\n}\n")]
    public void APR_ENG_001_AC1_TypeWithItsNestedCasesIsOneTopLevelType(string source)
    {
        using RepositoryFixture fixture = new();
        fixture.Write("src/Themis.Core/ApproverReference.cs", source);

        Assert.Empty(LayoutRules.FolderViolations(fixture.Root));
    }

    [Fact]
    public void APR_ENG_001_AC1_FolderOutsideTheLayoutIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("src/Themis.Core/Requests/Request.cs", "namespace Themis.Core.Requests;\n");

        Assert.Contains("src/Themis.Core/Themis.Core.csproj: the folder Requests is not one the project may hold.", LayoutRules.FolderViolations(fixture.Root));
    }

    [Theory]
    [InlineData("Types")]
    [InlineData("Requests")]
    [InlineData("Operations")]
    [InlineData("Migrations")]
    public void APR_ENG_001_AC1_EnginesFoldersAreAccepted(string folder)
    {
        using RepositoryFixture fixture = new();
        fixture.Write($"src/Themis/{folder}/Part.cs", $"namespace Themis.{folder};\n\ninternal sealed class Part;\n");

        Assert.Empty(LayoutRules.FolderViolations(fixture.Root));
    }

    [Fact]
    public void APR_ENG_001_AC1_FolderInsideAnEngineFolderIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("src/Themis/Requests/Steps/Step.cs", "namespace Themis.Requests.Steps;\n");

        Assert.Contains("src/Themis/Themis.csproj: the folder Requests holds a folder of its own.", LayoutRules.FolderViolations(fixture.Root));
    }

    [Fact]
    public void APR_ENG_001_AC1_FolderOfATestProjectIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("tests/Themis.Tests/Requests/RequestTests.cs", "namespace Themis.Tests.Requests;\n");

        Assert.Contains("tests/Themis.Tests/Themis.Tests.csproj: the folder Requests is not one the project may hold.", LayoutRules.FolderViolations(fixture.Root));
    }

    [Fact]
    public void APR_ENG_001_AC1_NamespaceThatDoesNotMatchItsFolderIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("src/Themis/Requests/Request.cs", "namespace Themis;\n");

        Assert.Contains("src/Themis/Requests/Request.cs declares the namespace Themis, not Themis.Requests.", LayoutRules.FolderViolations(fixture.Root));
    }

    [Fact]
    public void APR_ENG_001_AC1_PublicTypeOutsideTheProjectsRootIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("src/Themis/Requests/Request.cs", "namespace Themis.Requests;\n\npublic sealed class Request;\n");

        Assert.Contains("src/Themis/Requests/Request.cs declares a public type outside the project's root folder.", LayoutRules.FolderViolations(fixture.Root));
    }

    [Fact]
    public void APR_ENG_001_AC1_PackageSourcesFollowTheRule()
    {
        Assert.Empty(LayoutRules.PackageSourceViolations(RepositoryRoot.Path));
    }

    [Theory]
    [InlineData("<packageSources><add key=\"nuget.org\" value=\"https://api.nuget.org/v3/index.json\" /></packageSources>", "nuget.config does not clear the package sources first.")]
    [InlineData("<packageSources><clear /><add key=\"nuget.org\" value=\"https://api.nuget.org/v3/index.json\" /><add key=\"local\" value=\"artifacts/packages\" /></packageSources>", "does not add nuget.org, https://api.nuget.org/v3/index.json, as its one source.")]
    [InlineData("<packageSources><clear /><add key=\"nuget.org\" value=\"https://example.invalid/v3/index.json\" /></packageSources>", "does not add nuget.org, https://api.nuget.org/v3/index.json, as its one source.")]
    public void APR_ENG_001_AC1_PackageSourcesOutsideTheRuleAreFound(string sources, string expected)
    {
        using RepositoryFixture fixture = new();
        fixture.Write("nuget.config", $"""
            <configuration>
              {sources}
              <packageSourceMapping><packageSource key="nuget.org"><package pattern="*" /></packageSource></packageSourceMapping>
            </configuration>
            """);

        Assert.Contains(LayoutRules.PackageSourceViolations(fixture.Root), violation => violation.EndsWith(expected, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("<packageSourceMapping><packageSource key=\"nuget.org\"><package pattern=\"Microsoft.*\" /></packageSource></packageSourceMapping>")]
    [InlineData("<packageSourceMapping><packageSource key=\"nuget.org\"><package pattern=\"*\" /></packageSource><packageSource key=\"other\"><package pattern=\"Themis.*\" /></packageSource></packageSourceMapping>")]
    public void APR_ENG_001_AC1_PackageSourceMappingOutsideTheRuleIsFound(string mapping)
    {
        using RepositoryFixture fixture = new();
        fixture.Write("nuget.config", $"""
            <configuration>
              <packageSources><clear /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources>
              {mapping}
            </configuration>
            """);

        Assert.Contains("nuget.config does not map every package identifier, \"*\", to nuget.org alone.", LayoutRules.PackageSourceViolations(fixture.Root));
    }

    [Fact]
    public void APR_ENG_001_AC1_IgnoreFileListsThePackedPackages()
    {
        Assert.Empty(LayoutRules.IgnoreViolations(RepositoryRoot.Path));
    }

    [Fact]
    public void APR_ENG_001_AC1_IgnoreFileWithoutThePackedPackagesIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.Write(".gitignore", "bin/\nobj/\n# /artifacts/packages/\n");

        Assert.Contains(".gitignore does not list artifacts/packages/.", LayoutRules.IgnoreViolations(fixture.Root));
    }

    [Fact]
    public void APR_ENG_001_AC1_RootOfTheListedFilesIsAccepted()
    {
        Assert.Empty(LayoutRules.RootFileViolations([.. RootFiles, "RELEASE-NOTES.md", "src/Themis.Core/Themis.Core.csproj", "docs/spec/00-overview.md", ".github/workflows/gates.yml", "build/gates/run-gates.ps1"]));
    }

    [Theory]
    [InlineData("README.md", "README.md sits at the repository's root, which holds only the files APR-ENG-001 lists.")]
    [InlineData("AGENTS.md", "AGENTS.md sits at the repository's root, which holds only the files APR-ENG-001 lists.")]
    [InlineData("tools/run.ps1", "tools/ is a folder at the repository's root that APR-ENG-001 does not list.")]
    [InlineData("tmp/notes.txt", "tmp/ is a folder at the repository's root that APR-ENG-001 does not list.")]
    [InlineData(".config/other.json", ".config/other.json sits in .config/, which holds only the tool manifest.")]
    public void APR_ENG_001_AC1_RootFileOutsideTheListIsFound(string path, string expected)
    {
        Assert.Contains(expected, LayoutRules.RootFileViolations([.. RootFiles, path]));
    }

    [Theory]
    [InlineData("CHANGELOG.md")]
    [InlineData("nuget.config")]
    [InlineData(".config/dotnet-tools.json")]
    public void APR_ENG_001_AC1_ListedRootFileMissingIsFound(string missing)
    {
        Assert.Contains($"{missing} is missing from the repository's root.", LayoutRules.RootFileViolations([.. RootFiles.Where(file => !string.Equals(file, missing, StringComparison.Ordinal))]));
    }

    [Fact]
    public void RepositoryFixture_AsMade_KeepsEveryRule()
    {
        using RepositoryFixture fixture = new();

        Assert.Empty(LayoutRules.ProjectViolations(fixture.Root));
        Assert.Empty(LayoutRules.FolderViolations(fixture.Root));
        Assert.Empty(SettingRules.SettingViolations(fixture.Root));
        Assert.Empty(PackageRules.AllowListViolations(fixture.Root));
        Assert.Empty(PackageRules.MockingViolations(fixture.Root));
        Assert.Empty(ReferenceRules.ReferenceViolations(fixture.Root));
    }
}