using System;
using System.IO;

using Xunit;

namespace Themis.Repository.Tests;

[Trait("Category", "unit")]
public sealed class PackageAllowListTests
{
    [Fact]
    public void APR_ENG_009_AC1_PackagesAndToolsAreWithinTheTable()
    {
        Assert.Empty(PackageRules.AllowListViolations(RepositoryRoot.Path));
    }

    [Fact]
    public void APR_ENG_009_AC1_PackageOutsideTheTableIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject("tests/Themis.Core.Tests/Themis.Core.Tests.csproj", """
              <ItemGroup>
                <PackageReference Include="xunit.v3" />
                <PackageReference Include="Shouldly" />
              </ItemGroup>
            """);

        Assert.Contains(
            "tests/Themis.Core.Tests/Themis.Core.Tests.csproj references Shouldly, which is not in APR-ENG-009's table.",
            PackageRules.AllowListViolations(fixture.Root));
    }

    [Theory]
    [InlineData("Polly")]
    [InlineData("Microsoft.Extensions.Http")]
    [InlineData("Dapper")]
    public void APR_ENG_009_AC1_PackageVersionOutsideTheTableIsFound(string package)
    {
        using RepositoryFixture fixture = new();
        fixture.Write("Directory.Packages.props", $"""
            <Project>
              <ItemGroup>
                <PackageVersion Include="{package}" Version="8.0.0" />
              </ItemGroup>
            </Project>
            """);

        Assert.Contains($"Directory.Packages.props names {package}, which is not in APR-ENG-009's table.", PackageRules.AllowListViolations(fixture.Root));
    }

    [Fact]
    public void APR_ENG_009_AC1_PackageTakenByAReferenceOutsideTheProjectFileIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("src/Themis.Core/packages.lock.json", """
            { "version": 2, "dependencies": { "net10.0": { "Newtonsoft.Json": { "type": "Direct", "requested": "[13.0.3, )", "resolved": "13.0.3" } } } }
            """);

        Assert.Contains(
            "src/Themis.Core/Themis.Core.csproj references Newtonsoft.Json, which is not in APR-ENG-009's table.",
            PackageRules.AllowListViolations(fixture.Root));
    }

    [Fact]
    public void APR_ENG_009_AC1_ToolOtherThanDotnetEfIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.Write(".config/dotnet-tools.json", """
            {
              "version": 1,
              "isRoot": true,
              "tools": {
                "dotnet-ef": { "version": "10.0.14", "commands": [ "dotnet-ef" ] },
                "dotnet-reportgenerator-globaltool": { "version": "5.0.0", "commands": [ "reportgenerator" ] }
              }
            }
            """);

        Assert.Contains(
            "The tool manifest names dotnet-reportgenerator-globaltool; it names dotnet-ef alone.",
            PackageRules.AllowListViolations(fixture.Root));
    }

    [Fact]
    public void APR_ENG_009_AC1_ToolManifestWithoutDotnetEfIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.Write(".config/dotnet-tools.json", """{ "version": 1, "isRoot": true, "tools": { } }""");

        Assert.Contains("The tool manifest does not name dotnet-ef.", PackageRules.AllowListViolations(fixture.Root));
    }

    [Fact]
    public void APR_ENG_009_AC1_DotnetEfAtAVersionOtherThanEntityFrameworkCoresIsFound()
    {
        using RepositoryFixture fixture = new();
        string props = File.ReadAllText(Path.Combine(fixture.Root, "Directory.Packages.props"));
        fixture.Write("Directory.Packages.props", props.Replace(
            "<PackageVersion Include=\"Microsoft.EntityFrameworkCore\" Version=\"10.0.14\" />",
            "<PackageVersion Include=\"Microsoft.EntityFrameworkCore\" Version=\"10.0.13\" />",
            StringComparison.Ordinal));

        Assert.Contains(
            "The tool manifest takes dotnet-ef 10.0.14, not the EF Core version 10.0.13 of Microsoft.EntityFrameworkCore.",
            PackageRules.AllowListViolations(fixture.Root));
    }

    [Fact]
    public void APR_ENG_008_AC1_NoMockingPackageIsReferenced()
    {
        Assert.Empty(PackageRules.MockingViolations(RepositoryRoot.Path));
    }

    [Fact]
    public void APR_ENG_008_AC1_MockingPackageReferencedIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject("tests/Themis.Core.Tests/Themis.Core.Tests.csproj", """
              <ItemGroup>
                <PackageReference Include="xunit.v3" />
                <PackageReference Include="Moq" />
              </ItemGroup>
            """);

        Assert.Contains(
            "tests/Themis.Core.Tests/Themis.Core.Tests.csproj takes the mocking package Moq.",
            PackageRules.MockingViolations(fixture.Root));
    }

    [Fact]
    public void APR_ENG_008_AC1_MockingPackageTakenThroughAnotherIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("tests/Themis.Core.Tests/packages.lock.json", """
            { "version": 2, "dependencies": { "net10.0": { "NSubstitute": { "type": "Transitive", "resolved": "5.0.0" } } } }
            """);

        Assert.Contains(
            "tests/Themis.Core.Tests/Themis.Core.Tests.csproj takes the mocking package NSubstitute.",
            PackageRules.MockingViolations(fixture.Root));
    }
    [Fact]
    public void APR_ENG_009_AC1_LibrarysOwnPackagesTheSampleHostTakesAreNoThirdPartyPackages()
    {
        using RepositoryFixture fixture = new();
        string props = File.ReadAllText(Path.Combine(fixture.Root, "Directory.Packages.props"));
        fixture.Write("Directory.Packages.props", props.Replace("</Project>", "  <ItemGroup><PackageVersion Include=\"Themis\" Version=\"$(ThemisVersion)\" /><PackageVersion Include=\"Themis.Testing\" Version=\"$(ThemisVersion)\" /></ItemGroup>\n</Project>", StringComparison.Ordinal));

        Assert.Empty(PackageRules.AllowListViolations(fixture.Root));
    }
}