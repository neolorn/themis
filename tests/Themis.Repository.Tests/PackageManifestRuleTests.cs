using System;
using System.Xml.Linq;

using Xunit;

namespace Themis.Repository.Tests;

[Trait("Category", "unit")]
public sealed class PackageManifestRuleTests
{
    private const string Core = """
        <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
          <metadata>
            <id>Themis.Core</id>
            <version>1.2.3</version>
            <authors>Hossam Ismail</authors>
            <description>The public types and contracts every other package shares</description>
            <repository type="git" url="https://github.com/neolorn/themis" />
          </metadata>
        </package>
        """;

    [Theory]
    [InlineData("1.2.3")]
    [InlineData("[1.2.2]")]
    [InlineData("[1.2.3, )")]
    public void APR_LIB_001_AC2_DependencyOnALibraryPackageNotAtTheExactVersionPackedIsFound(string version)
    {
        PackageManifest[] manifests = [Parse(Core), Parse(Engine(version))];

        Assert.Equal(
            [$"Themis depends on Themis.Core at {version}, not the exact version packed, [1.2.3]."],
            PackageRules.DependencyViolations(manifests));
    }

    [Fact]
    public void APR_LIB_001_AC2_DependencyAtTheExactVersionPackedIsAccepted()
    {
        PackageManifest[] manifests = [Parse(Core), Parse(Engine("[1.2.3]"))];

        Assert.Empty(PackageRules.DependencyViolations(manifests));
    }

    [Fact]
    public void APR_LIB_001_AC2_DependencyOnALibraryPackageNotPackedWithItIsFound()
    {
        PackageManifest[] manifests = [Parse(Engine("[1.2.3]"))];

        Assert.Equal(["Themis depends on Themis.Core, which was not packed with it."], PackageRules.DependencyViolations(manifests));
    }

    [Fact]
    public void PackageTable_Descriptions_AreTheCellsWithoutMarkupOrReferences()
    {
        Assert.Equal("A .NET tool, command themis, that applies the migrations", PackageTable.Descriptions[Packages.Cli]);
        Assert.Equal(Packages.All.Count, PackageTable.Descriptions.Count);
    }

    [Fact]
    public void ManifestViolations_PackageAsTheRuleGivesIt_HasNone()
    {
        Assert.Empty(PackageRules.ManifestViolations(Parse(Core)));
    }

    [Fact]
    public void ManifestViolations_ToolPackageType_IsAcceptedOnTheToolAlone()
    {
        const string type = "<packageTypes><packageType name=\"DotnetTool\" /></packageTypes></metadata>";
        PackageManifest tool = Parse(Core
            .Replace("</metadata>", type, StringComparison.Ordinal)
            .Replace("<id>Themis.Core</id>", "<id>Themis.Cli</id>", StringComparison.Ordinal)
            .Replace("The public types and contracts every other package shares", "A .NET tool, command themis, that applies the migrations", StringComparison.Ordinal));
        PackageManifest other = Parse(Core.Replace("</metadata>", type, StringComparison.Ordinal));

        Assert.Empty(PackageRules.ManifestViolations(tool));
        Assert.Equal(["Themis.Core carries the package property packageTypes, which is left unset."], PackageRules.ManifestViolations(other));
    }

    [Theory]
    [InlineData("<tags>approvals</tags>", "Themis.Core carries the package property tags, which is left unset.")]
    [InlineData("<license type=\"expression\">MIT</license>", "Themis.Core carries the package property license, which is left unset.")]
    [InlineData("<projectUrl>https://example.invalid/</projectUrl>", "Themis.Core carries the package property projectUrl, which is left unset.")]
    public void ManifestViolations_AnotherPackageProperty_IsFound(string element, string violation)
    {
        PackageManifest manifest = Parse(Core.Replace("</metadata>", element + "</metadata>", StringComparison.Ordinal));

        Assert.Equal([violation], PackageRules.ManifestViolations(manifest));
    }

    [Theory]
    [InlineData("<authors>Hossam Ismail</authors>", "<authors>someone</authors>", "Themis.Core's authors are someone, not Hossam Ismail.")]
    [InlineData("every other package shares", "every package shares", "Themis.Core's description is not its cell of 00 section 2.2.")]
    [InlineData("github.com/neolorn/themis", "github.com/someone/themis", "Themis.Core's repository is https://github.com/someone/themis, not https://github.com/neolorn/themis.")]
    public void ManifestViolations_PropertyNotAsTheRuleGivesIt_IsFound(string text, string replacement, string violation)
    {
        PackageManifest manifest = Parse(Core.Replace(text, replacement, StringComparison.Ordinal));

        Assert.Equal([violation], PackageRules.ManifestViolations(manifest));
    }

    private static string Engine(string coreVersion)
    {
        return $"""
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>Themis</id>
                <version>1.2.3</version>
                <dependencies>
                  <group targetFramework="net10.0">
                    <dependency id="Themis.Core" version="{coreVersion}" exclude="Build,Analyzers" />
                    <dependency id="Npgsql.EntityFrameworkCore.PostgreSQL" version="10.0.0" exclude="Build,Analyzers" />
                  </group>
                </dependencies>
              </metadata>
            </package>
            """;
    }

    private static PackageManifest Parse(string nuspec)
    {
        return PackageManifest.Parse(XDocument.Parse(nuspec));
    }
}