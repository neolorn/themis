using System;
using System.Collections.Generic;
using System.Linq;

using Xunit;

namespace Themis.Repository.Tests;

/// <summary>The release gate's comparison and its version rule (<c>03</c> APR-LIB-006).</summary>
[Trait("Category", "unit")]
public sealed class ReleaseRuleTests
{
    private static readonly string[] Surface =
    [
        "Themis.Core: Themis.Core.IApprovals",
        "Themis.Core: Themis.Core.IApprovals.ReadAsync(Themis.Core.RequestId request, System.Threading.CancellationToken cancellationToken) -> System.Threading.Tasks.ValueTask<Themis.Core.RequestRecord?>",
        "Themis.Core: Themis.Core.IApproverDirectory",
        "Themis.Core: Themis.Core.Target",
    ];

    private static readonly ReleaseSet Baseline = new(
        ["approvals.request.closed", "approvals.request.unknown"],
        ["Themis:Timeout", "Themis:Jobs:Enabled"],
        new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["RequestStatus"] = new Dictionary<string, string> { ["Pending"] = "pending", ["Approved"] = "approved" },
        },
        new Dictionary<string, string> { ["approvals.decisions"] = "Counter", ["approvals.events.pending"] = "Observable gauge" });

    [Theory]
    [InlineData("member removed")]
    [InlineData("member changed")]
    [InlineData("code removed")]
    [InlineData("key removed")]
    [InlineData("spelling changed")]
    [InlineData("enumeration member removed")]
    [InlineData("enumeration removed")]
    [InlineData("metric removed")]
    [InlineData("metric kind changed")]
    public void APR_LIB_006_AC1_ChangeOrRemovalOfTheSurfaceCodesKeysSpellingsOrMetricsNeedsAMajorRelease(string change)
    {
        (string[] surface, ReleaseSet current) = Changed(change);

        ReleaseComparison comparison = ReleaseRules.Compare(Surface, surface, Baseline, current, _ => true);

        Assert.Equal(ReleaseNeed.Major, comparison.Need);
        Assert.NotEmpty(comparison.Breaks);
    }

    [Theory]
    [InlineData("member added")]
    [InlineData("code added")]
    [InlineData("key added")]
    [InlineData("enumeration member added")]
    [InlineData("enumeration added")]
    [InlineData("metric added")]
    public void APR_LIB_006_AC1_AdditionOfAMemberCodeKeySpellingOrMetricNeedsAMinorRelease(string change)
    {
        (string[] surface, ReleaseSet current) = Changed(change);

        ReleaseComparison comparison = ReleaseRules.Compare(Surface, surface, Baseline, current, _ => true);

        Assert.Equal(ReleaseNeed.Minor, comparison.Need);
        Assert.Empty(comparison.Breaks);
        Assert.NotEmpty(comparison.Additions);
    }

    [Fact]
    public void Compare_NothingChanged_NeedsAPatchRelease()
    {
        ReleaseComparison comparison = ReleaseRules.Compare(Surface, Surface, Baseline, Baseline, _ => true);

        Assert.Equal(ReleaseNeed.Patch, comparison.Need);
        Assert.Empty(comparison.Breaks);
        Assert.Empty(comparison.Additions);
    }

    [Theory]
    [InlineData("Themis.Core: Themis.Core.IApproverDirectory.ForgetAsync(string! person, System.Threading.CancellationToken cancellationToken) -> System.Threading.Tasks.ValueTask", true, ReleaseNeedName.Major)]
    [InlineData("Themis.Core: Themis.Core.IApproverDirectory.ForgetAsync(string! person, System.Threading.CancellationToken cancellationToken) -> System.Threading.Tasks.ValueTask", false, ReleaseNeedName.Minor)]
    [InlineData("Themis.Core: Themis.Core.ISecretSource.Region.get -> string?", true, ReleaseNeedName.Major)]
    [InlineData("Themis.Core: Themis.Core.IApprovals.ForgetAsync(string! person, System.Threading.CancellationToken cancellationToken) -> System.Threading.Tasks.ValueTask", true, ReleaseNeedName.Minor)]
    public void APR_LIB_006_AC1_MemberAddedToAHostImplementedInterfaceWithoutADefaultImplementationIsABreak(string added, bool withoutDefault, string need)
    {
        ReleaseComparison comparison = ReleaseRules.Compare(Surface, [.. Surface, added], Baseline, Baseline, entry => entry == added && withoutDefault);

        Assert.Equal(Enum.Parse<ReleaseNeed>(need), comparison.Need);
    }

    [Theory]
    [InlineData(ReleaseNeedName.Major, "v1.0.0", "v2.0.0", true)]
    [InlineData(ReleaseNeedName.Major, "v1.0.0", "v2.0.0-rc.1", true)]
    [InlineData(ReleaseNeedName.Major, "v1.0.0", "v1.1.0", false)]
    [InlineData(ReleaseNeedName.Major, "v1.0.0", "v1.1.0-rc.1", false)]
    [InlineData(ReleaseNeedName.Minor, "v1.0.0", "v1.1.0", true)]
    [InlineData(ReleaseNeedName.Minor, "v1.2.3", "v2.0.0", true)]
    [InlineData(ReleaseNeedName.Minor, "v1.0.0", "v1.0.1", false)]
    [InlineData(ReleaseNeedName.Minor, "v1.9.0", "v1.10.0-rc.2", true)]
    [InlineData(ReleaseNeedName.Patch, "v1.0.0", "v1.0.1", true)]
    [InlineData(ReleaseNeedName.Patch, "v1.0.0", "v1.0.1-rc.1", true)]
    [InlineData(ReleaseNeedName.Patch, "v1.0.0", "v1.0.0-rc.2", false)]
    [InlineData(ReleaseNeedName.Patch, "v1.0.0", "v1.0.0", false)]
    public void APR_LIB_006_AC1_ReleaseTagBelowWhatChangedNeedsFails(string need, string baseline, string tag, bool passes)
    {
        IReadOnlyList<string> violations = ReleaseRules.TagViolations(tag, baseline, Needing(need));

        Assert.Equal(passes, violations.Count == 0);
    }

    [Theory]
    [InlineData(ReleaseNeedName.Major, false, false)]
    [InlineData(ReleaseNeedName.Major, true, true)]
    [InlineData(ReleaseNeedName.Minor, false, true)]
    [InlineData(ReleaseNeedName.Patch, false, true)]
    public void APR_LIB_006_AC1_PullRequestNeedingAMajorReleasePassesOnlyWhereACommitSinceTheNewestFinalReleaseIsMarkedBreaking(string need, bool marked, bool passes)
    {
        IReadOnlyList<string> violations = ReleaseRules.PullRequestViolations(marked, "v1.0.0", Needing(need));

        Assert.Equal(passes, violations.Count == 0);
    }

    [Theory]
    [InlineData("refactor(core)!: rename the request's values", true)]
    [InlineData("feat!: drop the old seam", true)]
    [InlineData("refactor(core): rename the request's values\n\nBREAKING CHANGE: the values are renamed", true)]
    [InlineData("refactor(core): rename the request's values\n\n- BREAKING CHANGE: is not a footer here", false)]
    [InlineData("feat(engine): add a code", false)]
    public void MarkedBreaking_CommitMessage_ReadsItsMarkOrFooter(string message, bool marked)
    {
        Assert.Equal(marked, ReleaseRules.MarkedBreaking(message));
    }

    [Fact]
    public void ReleaseSet_WrittenAndParsed_IsTheSameSet()
    {
        ReleaseSet parsed = ReleaseSet.Parse(Baseline.Write());

        Assert.Equal(Baseline.Write(), parsed.Write());
        Assert.EndsWith("}\n", Baseline.Write(), StringComparison.Ordinal);
    }

    [Fact]
    public void ReleaseSet_ReadFromTheBuiltAssemblies_HoldsTheCodesKeysSpellingsAndInstruments()
    {
        ReleaseSet set = ReleaseSet.Read();

        Assert.Contains("approvals.request.closed", set.Codes);
        Assert.Contains("Themis:Observer:Enabled", set.ConfigurationKeys);
        Assert.Equal("anyone", set.Spellings["ApprovalMode"]["AnyOne"]);
        Assert.Equal("Counter", set.Metrics["approvals.decisions"]);
        Assert.Equal("Observable gauge", set.Metrics["approvals.events.pending"]);
    }

    private static ReleaseComparison Needing(string need)
    {
        return Enum.Parse<ReleaseNeed>(need) switch
        {
            ReleaseNeed.Major => new ReleaseComparison(["Themis.Core: Themis.Core.Target is removed"], []),
            ReleaseNeed.Minor => new ReleaseComparison([], ["Themis.Core: Themis.Core.Request is added"]),
            _ => new ReleaseComparison([], []),
        };
    }

    private static (string[] Surface, ReleaseSet Current) Changed(string change)
    {
        Dictionary<string, IReadOnlyDictionary<string, string>> spellings = new(Baseline.Spellings);
        Dictionary<string, string> metrics = new(Baseline.Metrics);
        string[] surface = Surface;
        string[] codes = [.. Baseline.Codes];
        string[] keys = [.. Baseline.ConfigurationKeys];
        switch (change)
        {
            case "member removed":
                surface = [.. Surface.Where(entry => !entry.EndsWith(".Target", StringComparison.Ordinal))];
                break;
            case "member changed":
                surface = [.. Surface.Select(entry => entry.Replace("RequestRecord?", "RequestRecord!", StringComparison.Ordinal))];
                break;
            case "member added":
                surface = [.. Surface, "Themis.Core: Themis.Core.Request"];
                break;
            case "code removed":
                codes = ["approvals.request.closed"];
                break;
            case "code added":
                codes = [.. codes, "approvals.request.locked"];
                break;
            case "key removed":
                keys = ["Themis:Timeout"];
                break;
            case "key added":
                keys = [.. keys, "Themis:Jobs:Interval"];
                break;
            case "spelling changed":
                spellings["RequestStatus"] = new Dictionary<string, string> { ["Pending"] = "open", ["Approved"] = "approved" };
                break;
            case "enumeration member removed":
                spellings["RequestStatus"] = new Dictionary<string, string> { ["Pending"] = "pending" };
                break;
            case "enumeration member added":
                spellings["RequestStatus"] = new Dictionary<string, string> { ["Pending"] = "pending", ["Approved"] = "approved", ["Void"] = "void" };
                break;
            case "enumeration removed":
                spellings.Remove("RequestStatus");
                break;
            case "enumeration added":
                spellings["EventKind"] = new Dictionary<string, string> { ["Requested"] = "requested" };
                break;
            case "metric removed":
                metrics.Remove("approvals.decisions");
                break;
            case "metric kind changed":
                metrics["approvals.decisions"] = "Histogram";
                break;
            default:
                metrics["approvals.requests.created"] = "Counter";
                break;
        }

        return (surface, new ReleaseSet(codes, keys, spellings, metrics));
    }

    /// <summary>The names of <see cref="ReleaseNeed"/>, which a test's data can carry where the internal type cannot.</summary>
    private static class ReleaseNeedName
    {
        internal const string Major = nameof(ReleaseNeed.Major);

        internal const string Minor = nameof(ReleaseNeed.Minor);

        internal const string Patch = nameof(ReleaseNeed.Patch);
    }
}