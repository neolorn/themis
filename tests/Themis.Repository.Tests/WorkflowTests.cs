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
    private const string AssetsLine = "$assets = @(Get-ChildItem -Path artifacts/packages -Filter '*.nupkg' -File | ForEach-Object FullName)";

    private const string CreateLine = "gh release create $env:TAG --verify-tag --title $env:TAG --notes-file RELEASE-NOTES.md @arguments @assets";

    private const string PreReleaseLine = @"if ($env:TAG -cmatch '^v\d+\.\d+\.\d+-rc\.[1-9]\d*$') { $arguments += '--prerelease' }";

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
        Assert.DoesNotContain("dotnet nuget push", release, StringComparison.Ordinal);
    }

    [Fact]
    public void APR_ENG_011_AC4_ReleaseStepAttachesThePackagesAloneAndMarksTheReleaseCandidateFormAloneAPreRelease()
    {
        Assert.Empty(ReleaseStepViolations(Job("release")));
    }

    [Theory]
    [InlineData(AssetsLine, AssetsLine + " + 'CHANGELOG.md'", "the release attaches something other than the packages")]
    [InlineData(CreateLine, CreateLine + " CHANGELOG.md", "the release attaches something other than the packages")]
    [InlineData(PreReleaseLine, "", "the release candidate's release is not marked a pre-release by its tag's exact form")]
    [InlineData(PreReleaseLine, "$arguments += '--prerelease'", "the release candidate's release is not marked a pre-release by its tag's exact form")]
    [InlineData(PreReleaseLine, "if ($env:TAG.Contains('-rc.')) { $arguments += '--prerelease' }", "the release candidate's release is not marked a pre-release by its tag's exact form")]
    [InlineData(PreReleaseLine, @"if ($env:TAG -cmatch '-rc\.\d+') { $arguments += '--prerelease' }", "the release candidate's release is not marked a pre-release by its tag's exact form")]
    public void APR_ENG_011_AC4_ReleaseStepOutsideTheRuleIsFound(string line, string replacement, string expected)
    {
        string[] release = [.. Job("release").Select(found => found.Trim() == line ? found.Replace(line, replacement, StringComparison.Ordinal) : found)];

        Assert.Contains(expected, ReleaseStepViolations(release));
    }

    [Fact]
    public void GatesJob_EveryEvent_RunsEveryGateAndCannotWriteToTheContents()
    {
        string gates = string.Join('\n', Job("gates"));

        Assert.Contains("./build/gates/run-gates.ps1 -Base $base -Head $head", gates, StringComparison.Ordinal);
        Assert.DoesNotContain("contents: write", gates, StringComparison.Ordinal);
    }

    /// <summary>
    /// The problems of the release step: it attaches the packages packed and nothing else, and
    /// marks the release a pre-release where, and only where, its tag has the release
    /// candidate's form, <c>vX.Y.Z-rc.N</c> with <c>N</c> from 1 (<c>05</c> APR-ENG-011).
    /// </summary>
    private static List<string> ReleaseStepViolations(string[] release)
    {
        List<string> violations = [];
        string[] lines = [.. release.Select(line => line.Trim())];
        if (!lines.Contains(AssetsLine, StringComparer.Ordinal) || !lines.Contains(CreateLine, StringComparer.Ordinal))
        {
            violations.Add("the release attaches something other than the packages");
        }

        string[] marked = [.. lines.Where(line => line.Contains("--prerelease", StringComparison.Ordinal))];
        if (marked is not [PreReleaseLine])
        {
            violations.Add("the release candidate's release is not marked a pre-release by its tag's exact form");
        }

        return violations;
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