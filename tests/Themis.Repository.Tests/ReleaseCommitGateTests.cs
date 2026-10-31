using System;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace Themis.Repository.Tests;

/// <summary>The commit-message gate's rule for the release commit <c>chore: release V</c> (<c>05</c> APR-ENG-011).</summary>
[Trait("Category", "integration")]
public sealed class ReleaseCommitGateTests
{
    private const string Changelog = """
        # Changelog

        All notable changes to this project are documented in this file.

        ## [Unreleased]

        ### Added

        - A request is created for a target.
        - A decision is recorded on a request.

        """;

    private const string CoreShipped = "#nullable enable\nThemis.Core.Kept\nThemis.Core.Target\n";

    private const string CoreUnshipped = "#nullable enable\nThemis.Core.Request\nThemis.Core.Approval\n*REMOVED*Themis.Core.Target\n";

    private const string CoreReleased = "#nullable enable\nThemis.Core.Approval\nThemis.Core.Kept\nThemis.Core.Request\n";

    private const string EngineUnshipped = "#nullable enable\nThemis.Engine\n";

    private const string Emptied = "#nullable enable\n";

    private const string OutsideTheRule = "the first line names a release outside \"chore: release V\", V being X.Y.Z or X.Y.Z-rc.N with N from 1";

    /// <summary>An instant whose date in UTC, 2026-11-01, is not its date where it was written.</summary>
    private static readonly DateTimeOffset ReleasedAt = new(2026, 10, 31, 23, 30, 0, TimeSpan.FromHours(-2));

    [Theory]
    [InlineData("1.0.0-rc.1")]
    [InlineData("1.0.0-rc.10")]
    [InlineData("1.0.0")]
    [InlineData("2.10.0")]
    public async Task APR_ENG_011_AC2_ReleaseCommitChangingTheReleasedFilesAsItsValueGivesPasses(string version)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateAsync(folder, cancellationToken);
        Release(repository, version);
        await repository.CommitAsync($"chore: release {version}", ReleasedAt, cancellationToken);
        repository.Write("docs/reports/phase-05.md", "# Phase 5\n");
        await repository.CommitAsync("docs: report the release", cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.True(gate.ExitCode == 0, gate.Output);
        Assert.Contains("2 commit(s)", gate.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APR_ENG_011_AC3_ReleaseCommitChangingAFileOutsideItsValueFails()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateAsync(folder, cancellationToken);
        Release(repository, "1.0.0-rc.1");
        repository.Write("src/Themis/Engine.cs", "namespace Themis;\n");
        await repository.CommitAsync("chore: release 1.0.0-rc.1", ReleasedAt, cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.NotEqual(0, gate.ExitCode);
        Assert.Contains("it changes src/Themis/Engine.cs, which a release commit does not change", gate.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("unsorted", "src/Themis.Core/PublicAPI.Shipped.txt is not its parent's entries and those of PublicAPI.Unshipped.txt beside it")]
    [InlineData("removed kept", "src/Themis.Core/PublicAPI.Shipped.txt is not its parent's entries and those of PublicAPI.Unshipped.txt beside it")]
    [InlineData("unshipped kept", "src/Themis/PublicAPI.Unshipped.txt holds more than #nullable enable")]
    [InlineData("dated where written", "CHANGELOG.md does not move the Unreleased entries under \"## [1.0.0-rc.1] - 2026-11-01\" below an empty \"## [Unreleased]\"")]
    [InlineData("unreleased kept", "CHANGELOG.md does not move the Unreleased entries under \"## [1.0.0-rc.1] - 2026-11-01\" below an empty \"## [Unreleased]\"")]
    [InlineData("no notes", "RELEASE-NOTES.md has no section headed \"## 1.0.0-rc.1\"")]
    public async Task APR_ENG_011_AC3_ReleaseCommitWritingOneOfItsFilesOtherwiseThanItsValueGivesFails(string change, string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateAsync(folder, cancellationToken);
        Release(repository, "1.0.0-rc.1");
        switch (change)
        {
            case "unsorted":
                repository.Write("src/Themis.Core/PublicAPI.Shipped.txt", "#nullable enable\nThemis.Core.Request\nThemis.Core.Approval\nThemis.Core.Kept\n");
                break;
            case "removed kept":
                repository.Write("src/Themis.Core/PublicAPI.Shipped.txt", "#nullable enable\nThemis.Core.Approval\nThemis.Core.Kept\nThemis.Core.Request\nThemis.Core.Target\n");
                break;
            case "unshipped kept":
                repository.Write("src/Themis/PublicAPI.Unshipped.txt", EngineUnshipped);
                break;
            case "dated where written":
                repository.Write("CHANGELOG.md", Changelog.Replace("## [Unreleased]\n", "## [Unreleased]\n\n## [1.0.0-rc.1] - 2026-10-31\n", StringComparison.Ordinal));
                break;
            case "unreleased kept":
                repository.Write("CHANGELOG.md", Changelog.Replace("## [Unreleased]\n", "## [Unreleased]\n\n### Added\n\n- A request is created for a target.\n\n## [1.0.0-rc.1] - 2026-11-01\n", StringComparison.Ordinal));
                break;
            default:
                repository.Write("RELEASE-NOTES.md", "# Release notes\n\n## 1.0.0\n");
                break;
        }

        await repository.CommitAsync("chore: release 1.0.0-rc.1", ReleasedAt, cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.NotEqual(0, gate.ExitCode);
        Assert.Contains(reason, gate.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("test: add a test of the release", "tests/Release.cs")]
    [InlineData("docs: correct the readme", "README.md")]
    public async Task APR_ENG_011_AC3_ReleaseCommitFollowedByACommitOtherThanADocsCommitChangingOnlyDocsFails(string message, string path)
    {
        ArgumentNullException.ThrowIfNull(message);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateAsync(folder, cancellationToken);
        Release(repository, "1.0.0-rc.1");
        await repository.CommitAsync("chore: release 1.0.0-rc.1", ReleasedAt, cancellationToken);
        repository.Write(path, "text\n");
        await repository.CommitAsync(message, cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.NotEqual(0, gate.ExitCode);
        Assert.Contains($"\"{message}\": it follows the release commit of 1.0.0-rc.1 and is not a docs: commit changing only docs/", gate.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("chore: release 1.0")]
    [InlineData("chore: release 1.0.0-rc.0")]
    [InlineData("chore: release 1.0.0-rc.01")]
    [InlineData("chore: release 1.0.0-beta.1")]
    [InlineData("chore: release 01.0.0")]
    [InlineData("chore: release v1.0.0")]
    [InlineData("chore: release 1.0.0 to the hosts")]
    [InlineData("chore(core): release 1.0.0")]
    [InlineData("build: release 1.0.0-rc.1")]
    public async Task APR_ENG_011_AC3_ReleaseCommitNamingAVersionOutsideTheRuleFails(string message)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateAsync(folder, cancellationToken);
        await repository.CommitAsync(message, cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.NotEqual(0, gate.ExitCode);
        Assert.Contains(OutsideTheRule, gate.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("chore: release the advisory lock earlier")]
    [InlineData("chore: bump the release notes' wording")]
    public async Task Gate_ChoreThatNamesNoVersion_IsNoReleaseCommit(string message)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateAsync(folder, cancellationToken);
        await repository.CommitAsync(message, cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.True(gate.ExitCode == 0, gate.Output);
    }

    private static async Task<(GitRepository Repository, string Start)> CreateAsync(ScratchFolder folder, CancellationToken cancellationToken)
    {
        GitRepository repository = await GitRepository.CreateAsync(folder, cancellationToken);
        repository.Write("CHANGELOG.md", Changelog);
        repository.Write("src/Themis.Core/PublicAPI.Shipped.txt", CoreShipped);
        repository.Write("src/Themis.Core/PublicAPI.Unshipped.txt", CoreUnshipped);
        repository.Write("src/Themis/PublicAPI.Shipped.txt", Emptied);
        repository.Write("src/Themis/PublicAPI.Unshipped.txt", EngineUnshipped);
        string start = await repository.CommitAsync("docs: add the changelog and the public API files", cancellationToken);
        return (repository, start);
    }

    /// <summary>Writes each file of the release commit of <paramref name="version"/> as its value gives, dated <see cref="ReleasedAt"/>.</summary>
    private static void Release(GitRepository repository, string version)
    {
        repository.Write("src/Themis.Core/PublicAPI.Shipped.txt", CoreReleased);
        repository.Write("src/Themis.Core/PublicAPI.Unshipped.txt", Emptied);
        repository.Write("src/Themis/PublicAPI.Shipped.txt", EngineUnshipped);
        repository.Write("src/Themis/PublicAPI.Unshipped.txt", Emptied);
        repository.Write("build/gates/release-baseline.json", "{}\n");
        repository.Write("CHANGELOG.md", Changelog.Replace("## [Unreleased]\n", $"## [Unreleased]\n\n## [{version}] - 2026-11-01\n", StringComparison.Ordinal));
        repository.Write("RELEASE-NOTES.md", $"# Release notes\n\n## {version}\n\nThe first release.\n");
    }

    private static Task<ProcessResult> RunAsync(GitRepository repository, string start, CancellationToken cancellationToken)
    {
        return Processes.GateAsync("gate-commit-messages.ps1", repository.Root, cancellationToken, "-Repository", repository.Root, "-Base", start, "-Head", "HEAD");
    }
}