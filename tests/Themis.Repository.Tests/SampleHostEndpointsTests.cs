using System;
using System.IO;

using Xunit;

namespace Themis.Repository.Tests;

/// <summary>
/// The sample host serves the endpoints of <c>Themis.Endpoints</c> (<c>03</c> APR-LIB-004
/// criterion 5): it maps them with its person seam, its own approval endpoints give way, and
/// its tests, which the sample host's gate runs from the packed packages, call them.
/// </summary>
[Trait("Category", "contract")]
public sealed class SampleHostEndpointsTests
{
    [Fact]
    public void APR_LIB_004_AC5_SampleHostMapsTheEndpointsWithItsPersonSeamAndItsGateTestsCallThem()
    {
        string program = File.ReadAllText(RepositoryRoot.Combine("samples", "Observatory", "Program.cs"));
        string own = File.ReadAllText(RepositoryRoot.Combine("samples", "Observatory", "Endpoints.cs"));
        string tests = File.ReadAllText(RepositoryRoot.Combine("samples", "Observatory.Tests", "ObservatoryTests.cs"));
        string project = File.ReadAllText(RepositoryRoot.Combine("samples", "Observatory", "Observatory.csproj"));

        Assert.Contains("<PackageReference Include=\"Themis.Endpoints\" />", project, StringComparison.Ordinal);
        Assert.Contains("app.MapThemisApprovals(\"/approvals\");", program, StringComparison.Ordinal);
        Assert.Contains("AddSingleton<ICurrentPerson, HeaderPerson>()", program, StringComparison.Ordinal);
        Assert.DoesNotContain("/decisions", own, StringComparison.Ordinal);
        Assert.DoesNotContain("/pending", own, StringComparison.Ordinal);
        Assert.Contains("public async Task APR_LIB_004_AC5_", tests, StringComparison.Ordinal);
        Assert.Contains("\"/approvals/delegations\"", tests, StringComparison.Ordinal);
    }
}