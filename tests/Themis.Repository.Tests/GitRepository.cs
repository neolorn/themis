using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace Themis.Repository.Tests;

/// <summary>
/// A Git repository a test makes under <c>tmp/</c>, apart from this one, to run a gate on.
/// </summary>
internal sealed class GitRepository(ScratchFolder folder)
{
    internal string Root { get; } = Path.Combine(folder.Path, "repository");

    internal static async Task<GitRepository> CreateAsync(ScratchFolder folder, CancellationToken cancellationToken)
    {
        GitRepository repository = new(folder);
        Directory.CreateDirectory(repository.Root);
        await repository.GitAsync(cancellationToken, "init", "--quiet", "--initial-branch=main");
        return repository;
    }

    internal void Write(string relativePath, string content)
    {
        string file = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content.ReplaceLineEndings("\n"));
    }

    /// <summary>Commits every change of the working tree, or none, under the given message, and answers the commit.</summary>
    internal Task<string> CommitAsync(string message, CancellationToken cancellationToken)
    {
        return CommitAsync(message, date: null, cancellationToken);
    }

    /// <summary>Commits as <see cref="CommitAsync(string, CancellationToken)"/> does, the commit dated <paramref name="date"/> where given.</summary>
    internal async Task<string> CommitAsync(string message, DateTimeOffset? date, CancellationToken cancellationToken)
    {
        string messageFile = Path.Combine(folder.Path, "message.txt");
        await File.WriteAllTextAsync(messageFile, message.ReplaceLineEndings("\n"), cancellationToken);
        await GitAsync(cancellationToken, "add", "--all");
        string[] dated = date is DateTimeOffset at ? ["--date", at.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture)] : [];
        await GitAsync(cancellationToken, ["commit", "--quiet", "--allow-empty", "--cleanup=verbatim", "--file", messageFile, .. dated]);
        ProcessResult head = await GitAsync(cancellationToken, "rev-parse", "HEAD");
        return head.Output.Trim();
    }

    internal async Task<ProcessResult> GitAsync(CancellationToken cancellationToken, params string[] arguments)
    {
        ProcessResult result = await Processes.GitAsync(Root, cancellationToken, arguments);
        Assert.True(result.ExitCode == 0, result.Output);
        return result;
    }
}