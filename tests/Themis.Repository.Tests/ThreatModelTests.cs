using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using Xunit;

namespace Themis.Repository.Tests;

/// <remarks>
/// The requirements are read from the table of <c>07</c> APR-THR-001, and the tests from every
/// C# file under <c>tests/</c>, their comments and string literals blanked so a probe's source
/// held in a literal is no test of the repository's.
/// </remarks>
[Trait("Category", "contract")]
public sealed partial class ThreatModelTests
{
    /// <summary>
    /// The requirements the table names whose control is the pipeline's, proved by its run on
    /// the pull request rather than by a test (<c>05</c> APR-ENG-012).
    /// </summary>
    private static readonly string[] CarriedByThePipeline = ["APR-ENG-012"];

    [Fact]
    public void APR_THR_001_AC1_EveryControlTheLibraryCarriesHasATestCarryingTheIdentifierOfTheRequirementThatNamesIt()
    {
        string model = File.ReadAllText(RepositoryRoot.Combine("docs", "spec", "07-threat-model.md"));
        int start = model.IndexOf("**APR-THR-001**", StringComparison.Ordinal);
        int end = model.IndexOf("**Acceptance criteria**", start, StringComparison.Ordinal);
        string table = model[start..end];
        string[] named = [.. RequirementPattern().Matches(table).Select(match => match.Value).Distinct(StringComparer.Ordinal).Where(requirement => requirement != "APR-THR-001").Except(CarriedByThePipeline, StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        HashSet<string> tested = TestedRequirements();

        Assert.Contains("APR-REQ-008", named);
        Assert.Contains("APR-THR-002", named);
        string[] untested = [.. named.Where(requirement => !tested.Contains(requirement))];
        Assert.Empty(untested);
    }

    /// <summary>The requirements some test of the repository is named after, as <c>APR-REQ-008</c> for <c>APR_REQ_008_AC1_...</c>.</summary>
    private static HashSet<string> TestedRequirements()
    {
        HashSet<string> tested = new(StringComparer.Ordinal);
        IEnumerable<string> files = Directory.EnumerateFiles(RepositoryRoot.Combine("tests"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => part is "bin" or "obj"));
        foreach (string file in files)
        {
            string code = CSharpSource.Read(File.ReadAllText(file)).Code;
            foreach (Match test in TestPattern().Matches(code))
            {
                tested.Add(test.Groups["requirement"].Value.Replace('_', '-'));
            }
        }

        return tested;
    }

    [GeneratedRegex(@"APR-[A-Z]{3,4}-\d{3}")]
    private static partial Regex RequirementPattern();

    [GeneratedRegex(@"\[(?:Fact|Theory)\b[^\]]*\](?:\s*\[[^\]]*\])*\s*public\s+(?:async\s+)?[\w<>]+\s+(?<requirement>APR_[A-Z]{3,4}_\d{3})_AC\d+_\w+\s*\(")]
    private static partial Regex TestPattern();
}