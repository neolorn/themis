using System;
using System.IO;

using Xunit;

namespace Themis.Repository.Tests;

[Trait("Category", "unit")]
public sealed class ProjectReferenceTests
{
    private const string SampleHost = "samples/Observatory/Observatory.csproj";

    private const string SampleTests = "samples/Observatory.Tests/Observatory.Tests.csproj";

    private const string BuildOnly = """
            <PackageReference Include="Microsoft.CodeAnalysis.PublicApiAnalyzers" PrivateAssets="all" />
            <PackageReference Include="MinVer" PrivateAssets="all" />
        """;

    [Fact]
    public void APR_LIB_001_AC1_EachProjectReferencesOnlyWhatItsRowAllows()
    {
        Assert.Empty(ReferenceRules.ReferenceViolations(RepositoryRoot.Path));
    }

    [Theory]
    [InlineData(Packages.Core, Packages.Engine)]
    [InlineData(Packages.Engine, Packages.Testing)]
    [InlineData(Packages.Testing, Packages.Cli)]
    public void APR_LIB_001_AC1_ProjectReferenceOutsideTheTableIsFound(string project, string reference)
    {
        using RepositoryFixture fixture = new();
        fixture.WriteSource(project, [reference]);

        Assert.Contains(
            $"src/{project}/{project}.csproj references the project {reference}, which APR-LIB-001 does not allow it.",
            ReferenceRules.ReferenceViolations(fixture.Root));
    }

    [Theory]
    [InlineData(Packages.Core, "Microsoft.Extensions.Options")]
    [InlineData(Packages.Core, "Microsoft.EntityFrameworkCore")]
    [InlineData(Packages.Testing, "Microsoft.EntityFrameworkCore")]
    [InlineData(Packages.Testing, "xunit.v3")]
    public void APR_LIB_001_AC1_PackageReferenceOutsideTheTableIsFound(string project, string package)
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject($"src/{project}/{project}.csproj", $"""
              <ItemGroup>
            {BuildOnly}
                <PackageReference Include="{package}" />
              </ItemGroup>
            """);

        Assert.Contains(
            $"src/{project}/{project}.csproj references the package {package}, which APR-LIB-001 does not allow it.",
            ReferenceRules.ReferenceViolations(fixture.Root));
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Microsoft.EntityFrameworkCore.Relational")]
    [InlineData("Npgsql.EntityFrameworkCore.PostgreSQL")]
    [InlineData("Microsoft.Extensions.Diagnostics.HealthChecks")]
    [InlineData("Microsoft.Extensions.Hosting.Abstractions")]
    public void APR_LIB_001_AC1_EnginesPackageWithinItsRowIsAccepted(string package)
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject("src/Themis/Themis.csproj", $"""
              <ItemGroup>
            {BuildOnly}
                <PackageReference Include="Microsoft.EntityFrameworkCore.Design" PrivateAssets="all" />
                <PackageReference Include="{package}" />
                <ProjectReference Include="..\..\src\Themis.Core\Themis.Core.csproj" />
              </ItemGroup>
            """);

        Assert.Empty(ReferenceRules.ReferenceViolations(fixture.Root));
    }

    [Fact]
    public void APR_LIB_001_AC1_TestProjectReferenceOutsideItsRowIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteTest("Themis.Core.Tests", [Packages.Core, Packages.Engine]);

        Assert.Contains(
            "tests/Themis.Core.Tests/Themis.Core.Tests.csproj references the project Themis, which APR-LIB-001 does not allow it.",
            ReferenceRules.ReferenceViolations(fixture.Root));
    }

    [Fact]
    public void APR_LIB_001_AC1_TestProjectTakingTheTestingPackageIsAccepted()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteTest("Themis.Tests", [Packages.Engine, Packages.Testing]);

        Assert.Empty(ReferenceRules.ReferenceViolations(fixture.Root));
    }

    [Fact]
    public void APR_LIB_001_AC1_TestProjectPackageOutsideTheTestPackagesIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject("tests/Themis.Core.Tests/Themis.Core.Tests.csproj", """
              <ItemGroup>
                <PackageReference Include="xunit.v3" />
                <PackageReference Include="Microsoft.EntityFrameworkCore" />
                <ProjectReference Include="..\..\src\Themis.Core\Themis.Core.csproj" />
              </ItemGroup>
            """);

        Assert.Contains(
            "tests/Themis.Core.Tests/Themis.Core.Tests.csproj references the package Microsoft.EntityFrameworkCore, which APR-LIB-001 does not allow it.",
            ReferenceRules.ReferenceViolations(fixture.Root));
    }

    [Fact]
    public void APR_LIB_001_AC1_ShippedProjectWithoutItsBuildOnlyReferencesIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject("src/Themis.Core/Themis.Core.csproj", """
              <ItemGroup>
                <PackageReference Include="MinVer" />
              </ItemGroup>
            """);

        string[] violations = [.. ReferenceRules.ReferenceViolations(fixture.Root)];

        Assert.Contains("src/Themis.Core/Themis.Core.csproj references MinVer without PrivateAssets=\"all\", though it is build-only.", violations);
        Assert.Contains("src/Themis.Core/Themis.Core.csproj does not reference Microsoft.CodeAnalysis.PublicApiAnalyzers as a build-only reference.", violations);
    }

    [Fact]
    public void APR_LIB_001_AC1_EngineWithoutTheDesignPackageIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteSource(Packages.Engine, [Packages.Core]);

        Assert.Contains(
            "src/Themis/Themis.csproj does not reference Microsoft.EntityFrameworkCore.Design as a build-only reference.",
            ReferenceRules.ReferenceViolations(fixture.Root));
    }

    [Fact]
    public void APR_LIB_001_AC1_DesignPackageOutsideTheEngineIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject("src/Themis.Testing/Themis.Testing.csproj", $"""
              <ItemGroup>
            {BuildOnly}
                <PackageReference Include="Microsoft.EntityFrameworkCore.Design" PrivateAssets="all" />
                <ProjectReference Include="..\..\src\Themis.Core\Themis.Core.csproj" />
                <ProjectReference Include="..\..\src\Themis\Themis.csproj" />
              </ItemGroup>
            """);

        Assert.Contains(
            "src/Themis.Testing/Themis.Testing.csproj references the package Microsoft.EntityFrameworkCore.Design, which APR-LIB-001 does not allow it.",
            ReferenceRules.ReferenceViolations(fixture.Root));
    }

    [Theory]
    [InlineData("src/Themis/Themis.csproj", "Microsoft.AspNetCore.App")]
    [InlineData("src/Themis.Core/Themis.Core.csproj", "Microsoft.AspNetCore.App")]
    [InlineData("tests/Themis.Tests/Themis.Tests.csproj", "Microsoft.AspNetCore.App")]
    [InlineData("src/Themis.Testing/Themis.Testing.csproj", "Microsoft.WindowsDesktop.App")]
    public void APR_LIB_001_AC1_FrameworkReferenceIsFound(string project, string framework)
    {
        ArgumentNullException.ThrowIfNull(project);
        using RepositoryFixture fixture = new();
        string file = Path.Combine(fixture.Root, project);
        fixture.Write(project, File.ReadAllText(file).Replace("</Project>", $"  <ItemGroup><FrameworkReference Include=\"{framework}\" /></ItemGroup>\n</Project>", StringComparison.Ordinal));

        Assert.Contains(
            $"{project} references the framework {framework}, which APR-LIB-001 does not allow it.",
            ReferenceRules.ReferenceViolations(fixture.Root));
    }

    [Fact]
    public void APR_LIB_001_AC1_PackageReferenceInTheSharedBuildSettingsIsFound()
    {
        using RepositoryFixture fixture = new();
        string props = File.ReadAllText(Path.Combine(fixture.Root, "Directory.Build.props"));
        fixture.Write("Directory.Build.props", props.Replace("</Project>", "  <ItemGroup><PackageReference Include=\"Microsoft.EntityFrameworkCore\" /></ItemGroup>\n</Project>", StringComparison.Ordinal));
        fixture.Write("src/Themis.Core/packages.lock.json", """
            {
              "version": 2,
              "dependencies": {
                "net10.0": {
                  "Microsoft.CodeAnalysis.PublicApiAnalyzers": { "type": "Direct", "requested": "[5.6.0, )", "resolved": "5.6.0" },
                  "Microsoft.EntityFrameworkCore": { "type": "Direct", "requested": "[10.0.14, )", "resolved": "10.0.14" },
                  "MinVer": { "type": "Direct", "requested": "[8.0.0, )", "resolved": "8.0.0" }
                }
              }
            }
            """);

        string[] violations = [.. ReferenceRules.ReferenceViolations(fixture.Root)];

        Assert.Contains("Directory.Build.props adds a PackageReference item Microsoft.EntityFrameworkCore; it adds no item but the CLSCompliant attribute.", violations);
        Assert.Contains("src/Themis.Core/Themis.Core.csproj takes the package Microsoft.EntityFrameworkCore by a reference outside its project file, which APR-LIB-001 does not allow it.", violations);
        Assert.DoesNotContain(violations, violation => violation.Contains("MinVer", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("DependOnTheExactVersionPacked", "PackageReference")]
    [InlineData("AnotherTarget", "_ProjectReferencesWithVersions")]
    public void APR_LIB_001_AC1_ItemInATargetOtherThanTheExactVersionUpdateIsFound(string target, string item)
    {
        using RepositoryFixture fixture = new();
        string props = File.ReadAllText(Path.Combine(fixture.Root, "Directory.Build.props"));
        fixture.Write("Directory.Build.props", props.Replace("</Project>", $"  <Target Name=\"{target}\"><ItemGroup><{item} Include=\"Microsoft.EntityFrameworkCore\" /></ItemGroup></Target>\n</Project>", StringComparison.Ordinal));

        Assert.Contains(
            $"Directory.Build.props adds a {item} item Microsoft.EntityFrameworkCore; it adds no item but the CLSCompliant attribute.",
            ReferenceRules.ReferenceViolations(fixture.Root));
    }

    [Fact]
    public void APR_LIB_001_AC1_GlobalPackageReferenceIsFound()
    {
        using RepositoryFixture fixture = new();
        string props = File.ReadAllText(Path.Combine(fixture.Root, "Directory.Packages.props"));
        fixture.Write("Directory.Packages.props", props.Replace("</Project>", "  <ItemGroup><GlobalPackageReference Include=\"Microsoft.EntityFrameworkCore\" Version=\"10.0.14\" /></ItemGroup>\n</Project>", StringComparison.Ordinal));

        Assert.Contains(
            "Directory.Packages.props adds a GlobalPackageReference item Microsoft.EntityFrameworkCore; it adds no item but PackageVersion.",
            ReferenceRules.ReferenceViolations(fixture.Root));
    }

    [Fact]
    public void APR_LIB_001_AC1_SampleHostAndItsTestProjectWithinTheirRowAreAccepted()
    {
        using RepositoryFixture fixture = new();
        WriteSample(fixture, SampleHost, """
                <PackageReference Include="Themis" />
                <FrameworkReference Include="Microsoft.AspNetCore.App" />
            """);
        WriteSample(fixture, SampleTests, """
                <PackageReference Include="Themis.Testing" />
                <PackageReference Include="xunit.v3" />
                <PackageReference Include="Testcontainers.PostgreSql" />
                <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
                <ProjectReference Include="..\Observatory\Observatory.csproj" />
            """);

        Assert.Empty(ReferenceRules.ReferenceViolations(fixture.Root));
        Assert.Empty(PackageRules.AllowListViolations(fixture.Root));
    }

    [Theory]
    [InlineData(SampleHost, """<ProjectReference Include="..\..\src\Themis\Themis.csproj" />""", "references the project Themis")]
    [InlineData(SampleHost, """<ProjectReference Include="..\Observatory.Tests\Observatory.Tests.csproj" />""", "references the project Observatory.Tests")]
    [InlineData(SampleHost, """<PackageReference Include="Microsoft.EntityFrameworkCore" />""", "references the package Microsoft.EntityFrameworkCore")]
    [InlineData(SampleHost, """<PackageReference Include="xunit.v3" />""", "references the package xunit.v3")]
    [InlineData(SampleHost, """<FrameworkReference Include="Microsoft.WindowsDesktop.App" />""", "references the framework Microsoft.WindowsDesktop.App")]
    [InlineData(SampleTests, """<ProjectReference Include="..\..\src\Themis.Testing\Themis.Testing.csproj" />""", "references the project Themis.Testing")]
    [InlineData(SampleTests, """<PackageReference Include="Npgsql" />""", "references the package Npgsql")]
    public void APR_LIB_001_AC1_SampleProjectReferenceOutsideItsRowIsFound(string project, string reference, string expected)
    {
        using RepositoryFixture fixture = new();
        WriteSample(fixture, project, reference);

        Assert.Contains($"{project} {expected}, which APR-LIB-001 does not allow it.", ReferenceRules.ReferenceViolations(fixture.Root));
    }

    [Theory]
    [InlineData("""<PackageReference Include="Themis" />""", "references the package Themis")]
    [InlineData("""<PackageReference Include="Themis.Testing" />""", "references the package Themis.Testing")]
    public void APR_LIB_001_AC1_TestProjectTakingAPackedPackageOfTheLibraryIsFound(string reference, string expected)
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject("tests/Themis.Tests/Themis.Tests.csproj", $"""
              <ItemGroup>
                <PackageReference Include="xunit.v3" />
                {reference}
              </ItemGroup>
            """);

        Assert.Contains($"tests/Themis.Tests/Themis.Tests.csproj {expected}, which APR-LIB-001 does not allow it.", ReferenceRules.ReferenceViolations(fixture.Root));
    }

    /// <summary>Writes a project of the sample host's solution, an ASP.NET Core project with no lockfile, holding <paramref name="items"/>.</summary>
    private static void WriteSample(RepositoryFixture fixture, string project, string items)
    {
        fixture.Write(project, $"""
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <ItemGroup>
            {items}
              </ItemGroup>
            </Project>
            """);
    }
}