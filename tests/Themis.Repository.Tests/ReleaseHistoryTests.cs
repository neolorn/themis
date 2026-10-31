using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace Themis.Repository.Tests;

/// <summary>The release gate read against a repository's tags and commits (<c>03</c> APR-LIB-006, <c>05</c> APR-ENG-011).</summary>
[Trait("Category", "integration")]
public sealed class ReleaseHistoryTests
{
    private const string Shipped = "#nullable enable\nThemis.Core.Kept\nThemis.Core.Target\n";

    private const string Emptied = "#nullable enable\n";

    private const string Removed = "#nullable enable\n*REMOVED*Themis.Core.Target\n";

    private static readonly ReleaseSet Current = new(
        ["approvals.request.closed"],
        ["Themis:Timeout"],
        new Dictionary<string, IReadOnlyDictionary<string, string>> { ["RequestStatus"] = new Dictionary<string, string> { ["Pending"] = "pending" } },
        new Dictionary<string, string> { ["approvals.decisions"] = "Counter" });

    [Fact]
    public async Task APR_LIB_006_AC2_WithNoFinalReleaseTaggedAPullRequestAndAReleaseCandidatesTagThatRemoveAPublicMemberPass()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await ReleasedAsync(folder, "v1.0.0-rc.1", cancellationToken);
        repository.Write("src/Themis.Core/PublicAPI.Unshipped.txt", Removed);
        string head = await repository.CommitAsync("refactor(core): remove the target", cancellationToken);

        IReadOnlyList<string> pullRequest = await ReleaseGate.CompatibilityViolationsAsync(repository.Root, ReleaseSet.Empty, _ => true, cancellationToken);
        await repository.GitAsync(cancellationToken, "tag", "v1.0.0-rc.2", head);
        IReadOnlyList<string> candidate = await ReleaseGate.CompatibilityViolationsAsync(repository.Root, ReleaseSet.Empty, _ => true, cancellationToken);

        Assert.Empty(pullRequest);
        Assert.Empty(candidate);
    }

    [Fact]
    public async Task APR_LIB_006_AC2_WithAFinalRelease100TaggedTheSameRemovalFailsInAPullRequestCarryingNoBreakingMarkAndInATagV110Rc1()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await ReleasedAsync(folder, "v1.0.0", cancellationToken);
        repository.Write("src/Themis.Core/PublicAPI.Unshipped.txt", Removed);
        string head = await repository.CommitAsync("refactor(core): remove the target", cancellationToken);

        IReadOnlyList<string> pullRequest = await ReleaseGate.CompatibilityViolationsAsync(repository.Root, ReleaseSet.Empty, _ => true, cancellationToken);
        await repository.GitAsync(cancellationToken, "tag", "v1.1.0-rc.1", head);
        IReadOnlyList<string> candidate = await ReleaseGate.CompatibilityViolationsAsync(repository.Root, ReleaseSet.Empty, _ => true, cancellationToken);

        Assert.Equal(["the public member Themis.Core: Themis.Core.Target is changed or removed since v1.0.0, and no commit since then carries ! or BREAKING CHANGE:"], pullRequest);
        Assert.Equal(["v1.1.0-rc.1 is below the major release the changes since v1.0.0 need: the public member Themis.Core: Themis.Core.Target is changed or removed"], candidate);
    }

    [Fact]
    public async Task CompatibilityViolations_RemovalMarkedBreakingAfterTheFinalRelease_PassInAPullRequestAndInAMajorReleasesTag()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await ReleasedAsync(folder, "v1.0.0", cancellationToken);
        repository.Write("src/Themis.Core/PublicAPI.Unshipped.txt", Removed);
        await repository.CommitAsync("refactor(core)!: remove the target", cancellationToken);
        string head = await repository.CommitAsync("docs: report the removal", cancellationToken);

        IReadOnlyList<string> pullRequest = await ReleaseGate.CompatibilityViolationsAsync(repository.Root, ReleaseSet.Empty, _ => true, cancellationToken);
        await repository.GitAsync(cancellationToken, "tag", "v2.0.0-rc.1", head);
        IReadOnlyList<string> candidate = await ReleaseGate.CompatibilityViolationsAsync(repository.Root, ReleaseSet.Empty, _ => true, cancellationToken);

        Assert.Empty(pullRequest);
        Assert.Empty(candidate);
    }

    [Fact]
    public async Task CompatibilityViolations_CodeRemovedSinceTheFinalReleasesBaseline_FailsInAPullRequest()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await ReleasedAsync(folder, "v1.0.0", cancellationToken, Current);
        await repository.CommitAsync("refactor(engine): answer the closed request's code no more", cancellationToken);

        IReadOnlyList<string> violations = await ReleaseGate.CompatibilityViolationsAsync(repository.Root, Current with { Codes = [] }, _ => true, cancellationToken);

        Assert.Equal(["the code approvals.request.closed is removed since v1.0.0, and no commit since then carries ! or BREAKING CHANGE:"], violations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task APR_ENG_011_AC3_ReleaseCommitWhoseBaselineIsNotTheReleasesOwnSetFails(bool reported)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await CreateAsync(folder, cancellationToken);
        repository.Write("build/gates/release-baseline.json", ReleaseSet.Empty.Write());
        await repository.CommitAsync("chore: release 1.0.0-rc.1", cancellationToken);
        if (reported)
        {
            repository.Write("docs/reports/phase-05.md", "# Phase 5\n");
            await repository.CommitAsync("docs: report the release", cancellationToken);
        }

        IReadOnlyList<string> violations = await ReleaseGate.BaselineViolationsAsync(repository.Root, Current, cancellationToken);

        Assert.Equal(["build/gates/release-baseline.json is not the release's own set, which the release commit of 1.0.0-rc.1 writes"], violations);
    }

    [Fact]
    public async Task BaselineViolations_ReleaseCommitWritingTheReleasesOwnSet_AreNone()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await CreateAsync(folder, cancellationToken);
        repository.Write("build/gates/release-baseline.json", Current.Write());
        await repository.CommitAsync("chore: release 1.0.0-rc.1", cancellationToken);

        Assert.Empty(await ReleaseGate.BaselineViolationsAsync(repository.Root, Current, cancellationToken));
    }

    [Fact]
    public async Task BaselineViolations_ChangeOutsideDocsSinceTheReleaseCommit_AreNotLookedFor()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await CreateAsync(folder, cancellationToken);
        repository.Write("build/gates/release-baseline.json", ReleaseSet.Empty.Write());
        await repository.CommitAsync("chore: release 1.0.0-rc.1", cancellationToken);
        repository.Write("src/Themis/Codes.cs", "namespace Themis;\n");
        await repository.CommitAsync("feat(engine): answer a new code", cancellationToken);

        Assert.Empty(await ReleaseGate.BaselineViolationsAsync(repository.Root, Current, cancellationToken));
    }

    [Fact]
    public void WithoutDefault_MemberOfABuiltSeam_IsTrue()
    {
        Assert.True(ReleaseGate.WithoutDefault("Themis.Core: Themis.Core.IApproverDirectory.ResolveAsync(string! permission, Themis.Core.ApproverQuery! query, System.Threading.CancellationToken cancellationToken) -> System.Threading.Tasks.ValueTask<System.Collections.Generic.IReadOnlyList<string!>!>"));
        Assert.True(ReleaseGate.WithoutDefault("Themis.Core: Themis.Core.IApproverDirectory.NotYetBuiltAsync() -> System.Threading.Tasks.ValueTask"));
    }

    private static async Task<GitRepository> CreateAsync(ScratchFolder folder, CancellationToken cancellationToken)
    {
        GitRepository repository = await GitRepository.CreateAsync(folder, cancellationToken);
        repository.Write("src/Themis.Core/PublicAPI.Shipped.txt", Shipped);
        repository.Write("src/Themis.Core/PublicAPI.Unshipped.txt", Emptied);
        await repository.CommitAsync("build: add the public API files", cancellationToken);
        return repository;
    }

    /// <summary>A repository whose first commit is released as <paramref name="tag"/>, its baseline <paramref name="baseline"/> or the empty set.</summary>
    private static async Task<GitRepository> ReleasedAsync(ScratchFolder folder, string tag, CancellationToken cancellationToken, ReleaseSet? baseline = null)
    {
        GitRepository repository = await GitRepository.CreateAsync(folder, cancellationToken);
        repository.Write("src/Themis.Core/PublicAPI.Shipped.txt", Shipped);
        repository.Write("src/Themis.Core/PublicAPI.Unshipped.txt", Emptied);
        repository.Write("build/gates/release-baseline.json", (baseline ?? ReleaseSet.Empty).Write());
        string release = await repository.CommitAsync($"chore: release {tag[1..]}", cancellationToken);
        await repository.GitAsync(cancellationToken, "tag", tag, release);
        return repository;
    }
}