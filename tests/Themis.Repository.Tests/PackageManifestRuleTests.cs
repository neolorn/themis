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

    private static PackageManifest Parse(string nuspec)
    {
        return PackageManifest.Parse(XDocument.Parse(nuspec));
    }
}