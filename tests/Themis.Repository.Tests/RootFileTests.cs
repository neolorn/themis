using System;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace Themis.Repository.Tests;

/// <remarks>
/// The root is read from the paths Git tracks, so a file the clone excludes, which never
/// enters history, is left out.
/// </remarks>
[Trait("Category", "integration")]
public sealed class RootFileTests
{
    [Fact]
    public async Task APR_ENG_001_AC1_RootHoldsTheFilesTheLayoutLists()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        ProcessResult files = await Processes.GitAsync(RepositoryRoot.Path, cancellationToken, "ls-files", "--cached", "--full-name");

        Assert.True(files.ExitCode == 0, files.Output);
        Assert.Empty(LayoutRules.RootFileViolations(files.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));
    }
}