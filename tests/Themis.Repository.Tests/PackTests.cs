using System.Linq;

using Xunit;

namespace Themis.Repository.Tests;

/// <remarks>
/// The packages are packed once for the class, from the working tree, into a folder under
/// <c>tmp/</c>.
/// </remarks>
[Trait("Category", "integration")]
public sealed class PackTests(PackedPackages packed) : IClassFixture<PackedPackages>
{
    [Fact]
    public void Pack_EachPackage_CarriesItsDescriptionAuthorsAndAddressAlone()
    {
        Assert.Equal(ProjectFile.SourceProjects(RepositoryRoot.Path).Count, packed.Manifests.Count);
        Assert.Empty(packed.Manifests.SelectMany(PackageRules.ManifestViolations));
    }
}