using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Xunit;

namespace Themis.Repository.Tests;

/// <summary>
/// The pipeline's definition, <c>.github/workflows/gates.yml</c>, read as text (<c>05</c>
/// APR-ENG-011, APR-ENG-012): the release tags it runs for, and the release job.
/// </summary>
/// <remarks>The release itself is proven by a release tag's run, which the phase report names as awaiting it.</remarks>
[Trait("Category", "unit")]
public sealed class WorkflowTests
{
    private static readonly string[] Lines = File.ReadAllLines(RepositoryRoot.Combine(".github", "workflows", "gates.yml"));

    [Fact]
    public void APR_ENG_011_AC4_PipelineRunsForTheTagsOfFinalReleasesAndReleaseCandidates()
    {
        string[] tags = [.. Block(Lines, "on:").SkipWhile(line => line.Trim() != "tags:").Skip(1).TakeWhile(line => line.TrimStart().StartsWith("- ", StringComparison.Ordinal)).Select(line => line.Trim())];

        Assert.Equal(["- 'v[0-9]+.[0-9]+.[0-9]+'", "- 'v[0-9]+.[0-9]+.[0-9]+-rc.[0-9]+'"], tags);
    }

    [Fact]
    public void APR_ENG_011_AC4_ReleaseJobRunsOnlyForATagOnceTheGatesJobHasPassed()
    {
        string[] release = Job("release");

        Assert.Contains("    needs: gates", release);
        Assert.Contains("    if: github.ref_type == 'tag'", release);
    }

    [Fact]
    public void APR_ENG_011_AC4_OnlyTheReleaseJobCanWriteToTheRepositorysContents()
    {
        string[] permissions = [.. Block(Lines, "permissions:").Skip(1).Where(line => line.Length > 0)];

        Assert.Equal(["  contents: read"], permissions);
        Assert.Equal(["release"], [.. Jobs().Where(job => Job(job).Any(line => line.Trim() == "contents: write"))]);
        Assert.DoesNotContain(Jobs(), job => Job(job).Any(line => line.Trim() is "permissions: write-all" or "contents: write" && job != "release"));
    }

    [Fact]
    public void APR_ENG_011_AC4_ReleaseJobPacksEveryPackageAtTheTagsVersionAndCreatesTheTagsReleaseMarkingAReleaseCandidatesAPreRelease()
    {
        string release = string.Join('\n', Job("release"));

        Assert.Contains("./build/gates/pack-packages.ps1 -Version $version", release, StringComparison.Ordinal);
        Assert.Contains("gh release create $env:TAG", release, StringComparison.Ordinal);
        Assert.Contains("--notes-file RELEASE-NOTES.md", release, StringComparison.Ordinal);
        Assert.Contains("if ($env:TAG.Contains('-rc.')) { $arguments += '--prerelease' }", release, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet nuget push", release, StringComparison.Ordinal);
    }

    [Fact]
    public void GatesJob_EveryEvent_RunsEveryGateAndCannotWriteToTheContents()
    {
        string gates = string.Join('\n', Job("gates"));

        Assert.Contains("./build/gates/run-gates.ps1 -Base $base -Head $head", gates, StringComparison.Ordinal);
        Assert.DoesNotContain("contents: write", gates, StringComparison.Ordinal);
    }

    private static string[] Jobs()
    {
        return [.. Block(Lines, "jobs:").Skip(1).Where(line => line.Length > 2 && line.StartsWith("  ", StringComparison.Ordinal) && line[2] != ' ' && line.EndsWith(':')).Select(line => line.Trim().TrimEnd(':'))];
    }

    /// <summary>The lines of the job <paramref name="name"/>, from its own line to the next job's.</summary>
    private static string[] Job(string name)
    {
        string[] jobs = [.. Block(Lines, "jobs:").Skip(1)];
        return [.. jobs.SkipWhile(line => line != $"  {name}:").TakeWhile((line, index) => index == 0 || !(line.Length > 2 && line.StartsWith("  ", StringComparison.Ordinal) && line[2] != ' ' && line.EndsWith(':')))];
    }

    /// <summary>The lines of the top-level key <paramref name="key"/>, from its own line to the next top-level key's.</summary>
    private static IEnumerable<string> Block(IEnumerable<string> lines, string key)
    {
        return lines.SkipWhile(line => line != key).TakeWhile((line, index) => index == 0 || line.Length == 0 || line.StartsWith(' ') || line.StartsWith('#'));
    }
}