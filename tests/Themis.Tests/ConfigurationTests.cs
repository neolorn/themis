using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Themis.Core;
using Themis.Operations;

using Xunit;

namespace Themis.Tests;

[Trait("Category", "unit")]
public sealed class ConfigurationTests
{
    /// <summary>A value outside each key's rule of <c>06</c> section 4: below it, above it, and one that does not read.</summary>
    public static TheoryData<string, string> OutsideTheirRule { get; } = new()
    {
        { "Timeout", "00:00:00.999" },
        { "Timeout", "00:01:00.001" },
        { "Timeout", "soon" },
        { "Database:Retries", "-1" },
        { "Database:Retries", "11" },
        { "Database:Retries", "three" },
        { "Jobs:Enabled", "yes" },
        { "Jobs:Hosted", "1" },
        { "Jobs:Interval", "00:00:04.999" },
        { "Jobs:Interval", "01:00:00.001" },
        { "Jobs:Interval", "30" },
        { "Jobs:ClaimTimeout", "00:00:59" },
        { "Jobs:ClaimTimeout", "01:00:01" },
        { "Jobs:ClaimTimeout", "later" },
        { "Jobs:BatchSize", "0" },
        { "Jobs:BatchSize", "1001" },
        { "Jobs:BatchSize", "1e2" },
        { "Jobs:StallFactor", "1" },
        { "Jobs:StallFactor", "101" },
        { "Jobs:StallFactor", "2.5" },
        { "Observer:Enabled", "on" },
        { "Observer:LagThreshold", "00:00:59" },
        { "Observer:LagThreshold", "1.00:00:01" },
        { "Observer:LagThreshold", "never" },
        { "Alerts:Repeat", "00:00:59" },
        { "Alerts:Repeat", "1.00:00:01" },
        { "Alerts:Repeat", "hourly" },
    };

    /// <summary>Each key's rule at its edges, which it accepts.</summary>
    public static TheoryData<string, string> AtTheirEdges { get; } = new()
    {
        { "Timeout", "00:00:01" },
        { "Timeout", "00:00:19" },
        { "Database:Retries", "0" },
        { "Database:Retries", "10" },
        { "Jobs:Enabled", "false" },
        { "Jobs:Hosted", "False" },
        { "Jobs:Interval", "00:00:05" },
        { "Jobs:Interval", "01:00:00" },
        { "Jobs:ClaimTimeout", "00:01:00" },
        { "Jobs:ClaimTimeout", "01:00:00" },
        { "Jobs:BatchSize", "1" },
        { "Jobs:BatchSize", "1000" },
        { "Jobs:StallFactor", "2" },
        { "Jobs:StallFactor", "100" },
        { "Observer:Enabled", "true" },
        { "Observer:LagThreshold", "00:01:00" },
        { "Observer:LagThreshold", "1.00:00:00" },
        { "Alerts:Repeat", "00:01:00" },
        { "Alerts:Repeat", "1.00:00:00" },
    };

    [Fact]
    public async Task APR_OPS_001_AC1_EveryKeyHasItsDefaultWhenAbsent()
    {
        await using TestHost host = TestHost.Build();

        ApprovalsOptions options = host.Services.GetRequiredService<IOptions<ApprovalsOptions>>().Value;

        Dictionary<string, string> bound = Values(options);
        Assert.Equal(
            Reference.Rows(4).Select(row => (row[0], row[1])),
            ConfigurationRules.Keys.Select(key => (key.Name, bound[key.Path])));
    }

    [Theory]
    [MemberData(nameof(OutsideTheirRule))]
    public async Task APR_OPS_001_AC1_KeyOutsideItsRuleStopsTheStartNamingIt(string key, string value)
    {
        await using TestHost host = TestHost.Build(new Dictionary<string, string?> { ["Themis:" + key] = value });

        ApprovalsStartupException failure = await host.StartFailureAsync();

        Assert.Equal(("approvals.startup.configurationinvalid", "Themis:" + key), (failure.Code, failure.Details["key"]));
    }

    [Fact]
    public async Task APR_OPS_001_AC1_ClaimTimeoutNotAboveThreeTimesTimeoutStopsTheStartNamingIt()
    {
        await using TestHost host = TestHost.Build(new Dictionary<string, string?> { ["Themis:Timeout"] = "00:00:20", ["Themis:Jobs:ClaimTimeout"] = "00:01:00" });

        ApprovalsStartupException failure = await host.StartFailureAsync();

        Assert.Equal(("approvals.startup.configurationinvalid", "Themis:Jobs:ClaimTimeout"), (failure.Code, failure.Details["key"]));
    }

    [Fact]
    public async Task APR_LIB_003_AC2_ConfigurationValueOutsideItsRuleStopsTheStartNamingTheKey()
    {
        await using TestHost host = TestHost.Build(new Dictionary<string, string?> { ["Themis:Jobs:BatchSize"] = "5000" });

        ApprovalsStartupException failure = await host.StartFailureAsync();

        Assert.Equal("approvals.startup.configurationinvalid", failure.Code);
        Assert.Equal(new Dictionary<string, string> { ["key"] = "Themis:Jobs:BatchSize" }, failure.Details);
    }

    [Theory]
    [MemberData(nameof(AtTheirEdges))]
    public void ConfigurationRules_KeyAtTheEdgeOfItsRule_IsAccepted(string key, string value)
    {
        Assert.Null(ConfigurationRules.Invalid(path => path == key ? value : null));
    }

    [Fact]
    public void ConfigurationRules_FirstKeyOutsideItsRule_IsTheOneAnswered()
    {
        Dictionary<string, string> raw = new(StringComparer.Ordinal) { ["Jobs:BatchSize"] = "0", ["Timeout"] = "0" };

        Assert.Equal("Themis:Timeout", ConfigurationRules.Invalid(path => raw.GetValueOrDefault(path))?.Name);
    }

    [Fact]
    public async Task APR_OPS_001_AC2_KeyChangedWhileTheApplicationRunsTakesEffectOnlyAfterARestart()
    {
        await using TestHost running = TestHost.Build(new Dictionary<string, string?> { ["Themis:Timeout"] = "00:00:07" }, before: TestHost.Seams(SecretSourceFake.WithConnection(TestHost.UnreachableConnection())));
        await running.StartAsync();

        running.Configuration["Themis:Timeout"] = "00:00:09";
        running.Configuration.Reload();

        Assert.Equal(TimeSpan.FromSeconds(7), running.Services.GetRequiredService<IOptions<ApprovalsOptions>>().Value.Timeout);
        await using TestHost restarted = TestHost.Build(running.Configuration, before: TestHost.Seams(SecretSourceFake.WithConnection(TestHost.UnreachableConnection())));
        await restarted.StartAsync();
        Assert.Equal(TimeSpan.FromSeconds(9), restarted.Services.GetRequiredService<IOptions<ApprovalsOptions>>().Value.Timeout);
    }

    [Fact]
    public async Task Start_KeyGiven_BindsItsValue()
    {
        await using TestHost host = TestHost.Build(new Dictionary<string, string?> { ["Themis:Jobs:Interval"] = "00:00:40" }, before: TestHost.Seams(SecretSourceFake.WithConnection(TestHost.UnreachableConnection())));

        await host.StartAsync();

        Assert.Equal(TimeSpan.FromSeconds(40), host.Services.GetRequiredService<IOptions<ApprovalsOptions>>().Value.Jobs.Interval);
    }

    [Fact]
    public void ApprovalsOptionsValidator_ValueOutsideItsRange_IsRefused()
    {
        ApprovalsOptions options = new() { Timeout = TimeSpan.FromMinutes(2) };

        Assert.True(new ApprovalsOptionsValidator().Validate(null, options).Failed);
    }

    /// <summary>Each key's bound value, written as <c>06</c> section 4 writes a default.</summary>
    private static Dictionary<string, string> Values(ApprovalsOptions options)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Timeout"] = Text(options.Timeout),
            ["Database:Retries"] = Text(options.Database.Retries),
            ["Jobs:Enabled"] = Text(options.Jobs.Enabled),
            ["Jobs:Hosted"] = Text(options.Jobs.Hosted),
            ["Jobs:Interval"] = Text(options.Jobs.Interval),
            ["Jobs:ClaimTimeout"] = Text(options.Jobs.ClaimTimeout),
            ["Jobs:BatchSize"] = Text(options.Jobs.BatchSize),
            ["Jobs:StallFactor"] = Text(options.Jobs.StallFactor),
            ["Observer:Enabled"] = Text(options.Observer.Enabled),
            ["Observer:LagThreshold"] = Text(options.Observer.LagThreshold),
            ["Alerts:Repeat"] = Text(options.Alerts.Repeat),
        };
    }

    private static string Text(object value)
    {
        return value switch
        {
            bool flag => flag ? "true" : "false",
            TimeSpan duration => duration.ToString("c", CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)!,
        };
    }
}