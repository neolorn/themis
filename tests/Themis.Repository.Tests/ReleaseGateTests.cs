using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace Themis.Repository.Tests;

/// <summary>
/// The release gate of <c>05</c> APR-ENG-012 on this repository at HEAD, which
/// <c>build/gates/gate-release-compatibility.ps1</c> runs (<c>03</c> APR-LIB-006).
/// </summary>
[Trait("Category", "integration")]
public sealed class ReleaseGateTests
{
    [Fact]
    public async Task ReleaseGate_ThisRepositoryAtHead_IsWithinWhatItsNewestFinalReleaseAllows()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        IReadOnlyList<string> violations = await ReleaseGate.CompatibilityViolationsAsync(RepositoryRoot.Path, ReleaseSet.Read(), ReleaseGate.WithoutDefault, cancellationToken);

        Assert.Empty(violations);
    }

    /// <remarks>Where the baseline is not the set, the set as the release commit writes it is left at <c>tmp/release-baseline.json</c>.</remarks>
    [Fact]
    public async Task ReleaseGate_ThisRepositoryAtHead_HoldsTheOwnSetOfItsLastReleaseCommit()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ReleaseSet current = ReleaseSet.Read();

        IReadOnlyList<string> violations = await ReleaseGate.BaselineViolationsAsync(RepositoryRoot.Path, current, cancellationToken);
        if (violations.Count > 0)
        {
            Directory.CreateDirectory(RepositoryRoot.Combine("tmp"));
            await File.WriteAllTextAsync(RepositoryRoot.Combine("tmp", "release-baseline.json"), current.Write(), cancellationToken);
        }

        Assert.Empty(violations);
    }
}