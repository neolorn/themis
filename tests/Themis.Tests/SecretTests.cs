using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Themis.Core;
using Themis.Operations;

using Xunit;

namespace Themis.Tests;

[Trait("Category", "unit")]
public sealed class SecretTests
{
    [Fact]
    public async Task APR_OPS_008_AC1_SourceThatAnswersNoConnectionStopsTheStartNamingIt()
    {
        await using TestHost host = TestHost.Build(before: TestHost.Seams(new SecretSourceFake()));

        ApprovalsStartupException failure = await host.StartFailureAsync();

        Assert.Equal(("approvals.startup.secretunavailable", "connection"), (failure.Code, failure.Details["key"]));
    }

    [Fact]
    public async Task APR_OPS_008_AC1_SourceThatAnswersConnectionWithNoVersionStopsTheStartNamingIt()
    {
        SecretSourceFake secrets = new();
        secrets.Set("connection", new SecretVersions { Versions = [] });
        await using TestHost host = TestHost.Build(before: TestHost.Seams(secrets));

        ApprovalsStartupException failure = await host.StartFailureAsync();

        Assert.Equal(("approvals.startup.secretunavailable", "connection"), (failure.Code, failure.Details["key"]));
    }

    [Theory]
    [InlineData("Host=db;Port=many")]
    [InlineData("Host=db;Colour=blue")]
    [InlineData("Database=approvals")]
    [InlineData("")]
    [InlineData("not a connection string")]
    public async Task APR_OPS_008_AC1_ValueNpgsqlDoesNotReadStopsTheStartAsInvalid(string value)
    {
        await using TestHost host = TestHost.Build(before: TestHost.Seams(SecretSourceFake.WithConnection(value)));

        ApprovalsStartupException failure = await host.StartFailureAsync();

        Assert.Equal(("approvals.startup.secretinvalid", "connection"), (failure.Code, failure.Details["key"]));
    }

    [Fact]
    public async Task APR_OPS_008_AC1_ValueThatIsNotUtf8StopsTheStartAsInvalid()
    {
        SecretSourceFake secrets = new();
        secrets.Set("connection", [0x48, 0x6F, 0x73, 0x74, 0x3D, 0xC3, 0x28]);
        await using TestHost host = TestHost.Build(before: TestHost.Seams(secrets));

        ApprovalsStartupException failure = await host.StartFailureAsync();

        Assert.Equal(("approvals.startup.secretinvalid", "connection"), (failure.Code, failure.Details["key"]));
    }

    [Fact]
    public async Task APR_OPS_008_AC2_NoLogEntryOfAFailedStartHoldsTheConnectionString()
    {
        string password = Guid.NewGuid().ToString("N");
        string connection = $"Host=db.invalid;Username=app;Password={password};Colour=blue";
        using LogCapture logs = new(everyCategory: true);
        await using TestHost host = TestHost.Build(before: TestHost.Seams(SecretSourceFake.WithConnection(connection)), logs: logs);

        await host.StartFailureAsync();

        Assert.NotEmpty(logs.Entries);
        Assert.All(logs.Entries, entry => Assert.DoesNotMatch($"{password}|db\\.invalid", entry.Message));
    }

    [Fact]
    public async Task Start_ConnectionOfSeveralVersions_OpensTheHighest()
    {
        SecretSourceFake secrets = new();
        secrets.Set("connection", new SecretVersions
        {
            Versions =
            [
                new SecretVersion { Version = 3, Value = Encoding.UTF8.GetBytes("Host=127.0.0.1;Port=1;Database=third;Username=app") },
                new SecretVersion { Version = 1, Value = Encoding.UTF8.GetBytes("not a connection string") },
            ],
        });
        await using TestHost host = TestHost.Build(before: TestHost.Seams(secrets));

        _ = await Record.ExceptionAsync(host.StartAsync);

        Assert.Equal("third", new NpgsqlConnectionStringBuilder(host.Services.GetRequiredService<ApprovalsDatabase>().Source.ConnectionString).Database);
    }

    [Fact]
    public async Task Start_SourceThatDoesNotAnswerWithinTheTimeout_StopsAsUnavailable()
    {
        await using TestHost host = TestHost.Build(
            new System.Collections.Generic.Dictionary<string, string?> { ["Themis:Timeout"] = "00:00:01" },
            before: TestHost.Seams(new SilentSecretSource()));

        ApprovalsStartupException failure = await host.StartFailureAsync();

        Assert.Equal(("approvals.startup.secretunavailable", "connection"), (failure.Code, failure.Details["key"]));
    }

    [Fact]
    public async Task Start_SecretSource_IsAskedForTheConnectionAlone()
    {
        SecretSourceFake secrets = SecretSourceFake.WithConnection(TestHost.UnreachableConnection());
        await using TestHost host = TestHost.Build(before: TestHost.Seams(secrets));

        _ = await Record.ExceptionAsync(host.StartAsync);

        Assert.Equal(["connection"], secrets.Read);
    }

    /// <summary>A secret source that never answers.</summary>
    private sealed class SilentSecretSource : ISecretSource
    {
        public async ValueTask<SecretVersions?> ReadAsync(string name, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, CancellationToken.None);
            return null;
        }
    }
}