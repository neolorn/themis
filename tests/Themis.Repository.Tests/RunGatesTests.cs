using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace Themis.Repository.Tests;

/// <remarks>
/// Each test copies <c>run-gates.ps1</c> into a repository it makes under <c>tmp/</c>, with
/// gates of its own, since the script runs the gates beside it on the repository above them.
/// </remarks>
[Trait("Category", "integration")]
public sealed class RunGatesTests
{
    private const string Passing = "Write-Host 'The probe gate ran.'\n";

    [Fact]
    public async Task RunGates_WorkingTreeOfHead_RunsEachGate()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await CreateAsync(folder, Passing, cancellationToken);

        ProcessResult run = await RunAsync(repository, "HEAD", cancellationToken);

        Assert.True(run.ExitCode == 0, run.Output);
        Assert.Contains("The probe gate ran.", run.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunGates_FailingGate_FailsTheRun()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await CreateAsync(folder, "exit 3\n", cancellationToken);

        ProcessResult run = await RunAsync(repository, "HEAD", cancellationToken);

        Assert.NotEqual(0, run.ExitCode);
        Assert.Contains("failed (exit code 3)", run.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunGates_HeadOtherThanTheCheckedOutCommit_Refuses()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await CreateAsync(folder, Passing, cancellationToken);
        string first = (await repository.GitAsync(cancellationToken, "rev-parse", "HEAD")).Output.Trim();
        await repository.CommitAsync("docs: add nothing", cancellationToken);

        ProcessResult run = await RunAsync(repository, first, cancellationToken);

        Assert.NotEqual(0, run.ExitCode);
        Assert.Contains($"not -Head {first}", run.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("The probe gate ran.", run.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("build/gates/gate-probe.ps1")]
    [InlineData("notes.txt")]
    public async Task RunGates_WorkingTreeOtherThanHeads_Refuses(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await CreateAsync(folder, Passing, cancellationToken);
        repository.Write(path, "Write-Host 'A change.'\n");

        ProcessResult run = await RunAsync(repository, "HEAD", cancellationToken);

        Assert.NotEqual(0, run.ExitCode);
        Assert.Contains("The working tree differs from HEAD in 1 path(s)", run.Output, StringComparison.Ordinal);
        Assert.Contains(path, run.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunGates_UntrackedFileOfAnExcludedPath_Runs()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await CreateAsync(folder, Passing, cancellationToken);
        repository.Write(".git/info/exclude", "/tmp/\n");
        repository.Write("tmp/tools/scanner.txt", "A tool.\n");

        ProcessResult run = await RunAsync(repository, "HEAD", cancellationToken);

        Assert.True(run.ExitCode == 0, run.Output);
    }

    [Fact]
    public async Task RunGates_NoGateScript_Fails()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await CreateAsync(folder, null, cancellationToken);

        ProcessResult run = await RunAsync(repository, "HEAD", cancellationToken);

        Assert.NotEqual(0, run.ExitCode);
        Assert.Contains("build/gates/ holds no gate script, so nothing would be checked", run.Output, StringComparison.Ordinal);
    }

    private static async Task<GitRepository> CreateAsync(ScratchFolder folder, string? gate, CancellationToken cancellationToken)
    {
        GitRepository repository = await GitRepository.CreateAsync(folder, cancellationToken);
        repository.Write("build/gates/run-gates.ps1", await File.ReadAllTextAsync(RepositoryRoot.Combine("build", "gates", "run-gates.ps1"), cancellationToken));
        if (gate is not null)
        {
            repository.Write("build/gates/gate-probe.ps1", gate);
        }

        await repository.CommitAsync("ci: add the gates", cancellationToken);
        return repository;
    }

    private static Task<ProcessResult> RunAsync(GitRepository repository, string head, CancellationToken cancellationToken)
    {
        string script = Path.Combine(repository.Root, "build", "gates", "run-gates.ps1");
        return Processes.RunAsync("pwsh", ["-NoProfile", "-NonInteractive", "-File", script, "-Head", head], repository.Root, cancellationToken);
    }
}