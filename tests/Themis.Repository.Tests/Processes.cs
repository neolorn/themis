using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Themis.Repository.Tests;

/// <summary>
/// Runs the tools the integration tests drive: <c>git</c>, <c>dotnet</c> and <c>pwsh</c>.
/// </summary>
/// <remarks>
/// A process starts without the <c>GIT_</c> variables of the machine's environment, and
/// <c>git</c> without hooks, so the machine's state never changes what a test sees.
/// </remarks>
internal static class Processes
{
    private static readonly TimeSpan Limit = TimeSpan.FromMinutes(5);

    internal static Task<ProcessResult> GitAsync(string folder, CancellationToken cancellationToken, params string[] arguments)
    {
        return RunAsync("git", ["-c", "user.name=Test", "-c", "user.email=test@example.invalid", "-c", "commit.gpgsign=false", "-c", "core.autocrlf=false", "-c", "core.hooksPath=", .. arguments], folder, cancellationToken);
    }

    internal static Task<ProcessResult> DotnetAsync(string folder, CancellationToken cancellationToken, params string[] arguments)
    {
        return RunAsync("dotnet", arguments, folder, cancellationToken);
    }

    internal static Task<ProcessResult> GateAsync(string gate, string folder, CancellationToken cancellationToken, params string[] arguments)
    {
        string script = RepositoryRoot.Combine("build", "gates", gate);
        return RunAsync("pwsh", ["-NoProfile", "-NonInteractive", "-File", script, .. arguments], folder, cancellationToken);
    }

    internal static async Task<ProcessResult> RunAsync(string file, IReadOnlyList<string> arguments, string folder, CancellationToken cancellationToken)
    {
        ProcessStartInfo start = new(file)
        {
            WorkingDirectory = folder,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        foreach (string name in start.Environment.Keys.Where(name => name.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            start.Environment.Remove(name);
        }

        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["TESTINGPLATFORM_TELEMETRY_OPTOUT"] = "1";
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";

        using Process process = new() { StartInfo = start };
        StringBuilder output = new();
        object gate = new();
        process.OutputDataReceived += (_, line) => Append(output, gate, line.Data);
        process.ErrorDataReceived += (_, line) => Append(output, gate, line.Data);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using CancellationTokenSource limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(Limit);
        try
        {
            await process.WaitForExitAsync(limit.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        lock (gate)
        {
            return new ProcessResult(process.ExitCode, output.ToString());
        }
    }

    private static void Append(StringBuilder output, object gate, string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (gate)
        {
            output.AppendLine(line);
        }
    }
}