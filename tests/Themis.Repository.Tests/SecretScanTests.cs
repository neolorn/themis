using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace Themis.Repository.Tests;

/// <remarks>
/// The credential-shaped string is drawn at run time and written only in a repository the
/// test makes under <c>tmp/</c>, so none is ever written in this repository's history.
/// </remarks>
[Trait("Category", "integration")]
public sealed class SecretScanTests
{
    private const string Alphanumeric = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    private const string Found = "leaks found";

    [Fact]
    public async Task APR_THR_003_AC1_ACommitCarryingACredentialShapedStringFailsTheScan()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await GitRepository.CreateAsync(folder, cancellationToken);
        repository.Write("README.md", "A repository with no secret.\n");
        await repository.CommitAsync("docs: add the readme", cancellationToken);
        string token = Token();
        repository.Write("settings.txt", $"token = {token}\n");
        string commit = await repository.CommitAsync("chore: add the settings", cancellationToken);

        ProcessResult scan = await ScanAsync(repository, repository.Root, cancellationToken);

        Assert.NotEqual(0, scan.ExitCode);
        Assert.Contains(Found, scan.Output, StringComparison.Ordinal);
        Assert.Contains(commit, scan.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(token, scan.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APR_THR_003_AC1_ACredentialShapedStringRemovedLaterStillFailsTheScan()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await GitRepository.CreateAsync(folder, cancellationToken);
        repository.Write("settings.txt", $"token = {Token()}\n");
        string commit = await repository.CommitAsync("chore: add the settings", cancellationToken);
        repository.Write("settings.txt", "token = (read from the secret source)\n");
        await repository.CommitAsync("fix: read the token from the secret source", cancellationToken);

        ProcessResult scan = await ScanAsync(repository, repository.Root, cancellationToken);

        Assert.NotEqual(0, scan.ExitCode);
        Assert.Contains(Found, scan.Output, StringComparison.Ordinal);
        Assert.Contains(commit, scan.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APR_THR_003_AC2_ALineCarryingTheInlineAllowMarkerFailsTheScan()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await GitRepository.CreateAsync(folder, cancellationToken);
        repository.Write("settings.txt", $"token = {Token()} # gitleaks" + ":allow\n");
        string commit = await repository.CommitAsync("chore: add the settings", cancellationToken);

        ProcessResult scan = await ScanAsync(repository, repository.Root, cancellationToken);

        Assert.NotEqual(0, scan.ExitCode);
        Assert.Contains(Found, scan.Output, StringComparison.Ordinal);
        Assert.Contains(commit, scan.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(".gitleaksignore")]
    [InlineData("config/.gitleaksignore")]
    public async Task APR_THR_003_AC2_ATrackedGitleaksIgnoreFailsTheScanWhateverItLists(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await GitRepository.CreateAsync(folder, cancellationToken);
        repository.Write("README.md", "A repository with no secret.\n");
        repository.Write(path, "# Nothing is listed.\n");
        await repository.CommitAsync("docs: add the readme", cancellationToken);

        ProcessResult scan = await ScanAsync(repository, repository.Root, cancellationToken);

        Assert.NotEqual(0, scan.ExitCode);
        Assert.Contains($"{path} is a .gitleaksignore the repository tracks", scan.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APR_THR_003_AC2_AGitleaksIgnoreAtTheScannedRootFailsTheScanWhateverItLists()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await GitRepository.CreateAsync(folder, cancellationToken);
        repository.Write("README.md", "A repository with no secret.\n");
        await repository.CommitAsync("docs: add the readme", cancellationToken);
        repository.Write(".gitleaksignore", "# Nothing is listed.\n");

        ProcessResult scan = await ScanAsync(repository, repository.Root, cancellationToken);

        Assert.NotEqual(0, scan.ExitCode);
        Assert.Contains(".gitleaksignore sits at the scanned repository's root", scan.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APR_THR_003_AC2_AFindingAGitleaksIgnoreOfTheWorkingDirectoryListsFailsTheScan()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await GitRepository.CreateAsync(folder, cancellationToken);
        repository.Write("settings.txt", $"token = {Token()}\n");
        string commit = await repository.CommitAsync("chore: add the settings", cancellationToken);
        string workingDirectory = Path.Combine(folder.Path, "working");
        Directory.CreateDirectory(workingDirectory);
        await File.WriteAllTextAsync(Path.Combine(workingDirectory, ".gitleaksignore"), $"{commit}:settings.txt:github-pat:1\n", cancellationToken);

        ProcessResult scan = await ScanAsync(repository, workingDirectory, cancellationToken);

        Assert.NotEqual(0, scan.ExitCode);
        Assert.Contains(Found, scan.Output, StringComparison.Ordinal);
        Assert.Contains($"{commit}:settings.txt:github-pat:1", scan.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SecretScan_HistoryWithoutSecrets_Passes()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await GitRepository.CreateAsync(folder, cancellationToken);
        repository.Write("README.md", "A repository with no secret.\n");
        await repository.CommitAsync("docs: add the readme", cancellationToken);

        ProcessResult scan = await ScanAsync(repository, repository.Root, cancellationToken);

        Assert.True(scan.ExitCode == 0, scan.Output);
    }

    [Fact]
    public async Task FetchSecretScanner_ChangedScanner_IsReplacedByTheArchives()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        string scanner = await FetchedScannerAsync(cancellationToken);
        string tools = Path.Combine(folder.Path, "tools");
        Directory.CreateDirectory(tools);
        foreach (string archive in Directory.GetFiles(Path.GetDirectoryName(scanner)!, "gitleaks_*"))
        {
            File.Copy(archive, Path.Combine(tools, Path.GetFileName(archive)));
        }

        string changed = Path.Combine(tools, Path.GetFileName(scanner));
        await File.WriteAllTextAsync(changed, "not the scanner", cancellationToken);

        ProcessResult fetch = await Processes.GateAsync("fetch-secret-scanner.ps1", folder.Path, cancellationToken, "-Tools", tools);

        Assert.True(fetch.ExitCode == 0, fetch.Output);
        Assert.Equal(await HashAsync(scanner, cancellationToken), await HashAsync(changed, cancellationToken));
    }

    [Fact]
    public async Task FetchSecretScanner_ArchiveOtherThanThePublished_Fails()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        string scanner = await FetchedScannerAsync(cancellationToken);
        string tools = Path.Combine(folder.Path, "tools");
        Directory.CreateDirectory(tools);
        foreach (string archive in Directory.GetFiles(Path.GetDirectoryName(scanner)!, "gitleaks_*"))
        {
            await File.WriteAllTextAsync(Path.Combine(tools, Path.GetFileName(archive)), "not the archive", cancellationToken);
        }

        ProcessResult fetch = await Processes.GateAsync("fetch-secret-scanner.ps1", folder.Path, cancellationToken, "-Tools", tools);

        Assert.NotEqual(0, fetch.ExitCode);
        Assert.Contains("does not match its published SHA-256", fetch.Output, StringComparison.Ordinal);
    }

    private static string Token()
    {
        // The shape of a GitHub personal access token, which gitleaks' default finds.
        return "ghp" + "_" + RandomNumberGenerator.GetString(Alphanumeric, 36);
    }

    private static Task<ProcessResult> ScanAsync(GitRepository repository, string workingDirectory, CancellationToken cancellationToken)
    {
        return Processes.GateAsync("gate-secret-scan.ps1", workingDirectory, cancellationToken, "-Repository", repository.Root);
    }

    private static async Task<string> FetchedScannerAsync(CancellationToken cancellationToken)
    {
        ProcessResult fetch = await Processes.GateAsync("fetch-secret-scanner.ps1", RepositoryRoot.Path, cancellationToken);
        Assert.True(fetch.ExitCode == 0, fetch.Output);
        return fetch.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[^1];
    }

    private static async Task<string> HashAsync(string file, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(file);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
    }
}