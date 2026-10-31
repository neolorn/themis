using Xunit;

namespace Themis.Repository.Tests;

/// <remarks>
/// The grants are read from each project's built assembly under <c>bin/Release/net10.0/</c>,
/// which the build of <c>Themis.sln</c> the gates run first leaves there.
/// </remarks>
[Trait("Category", "contract")]
public sealed class GrantTests
{
    [Fact]
    public void APR_ENG_001_AC1_GrantsFollowTheRule()
    {
        Assert.Empty(ReferenceRules.GrantViolations(RepositoryRoot.Path));
    }
}