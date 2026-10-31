namespace Themis.Repository.Tests;

/// <summary>
/// What a process run answered: its exit code and its output and error streams together.
/// </summary>
internal sealed record ProcessResult(int ExitCode, string Output);