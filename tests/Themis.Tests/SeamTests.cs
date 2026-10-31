using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Themis.Core;

using Xunit;

namespace Themis.Tests;

[Trait("Category", "unit")]
public sealed class SeamTests
{
    [Fact]
    public async Task APR_LIB_005_AC1_SecretSourceMissingStopsTheStartNamingIt()
    {
        await using TestHost host = TestHost.Build(before: services =>
        {
            services.AddSingleton<IApproverDirectory, ApproverDirectoryFake>();
            services.AddSingleton<IApprovalObserver, ApprovalObserverFake>();
        });

        ApprovalsStartupException failure = await host.StartFailureAsync();

        Assert.Equal(("approvals.startup.seammissing", "secretSource"), (failure.Code, failure.Details["key"]));
    }

    [Fact]
    public async Task APR_LIB_005_AC1_ApproverDirectoryMissingStopsTheStartNamingIt()
    {
        await using TestHost host = TestHost.Build(before: services =>
        {
            services.AddSingleton<ISecretSource>(new SecretSourceFake());
            services.AddSingleton<IApprovalObserver, ApprovalObserverFake>();
        });

        ApprovalsStartupException failure = await host.StartFailureAsync();

        Assert.Equal(("approvals.startup.seammissing", "approverDirectory"), (failure.Code, failure.Details["key"]));
    }

    [Fact]
    public async Task APR_LIB_005_AC1_ObserverMissingWhereTheJobsRunAndTheObserverIsOnStopsTheStartNamingIt()
    {
        await using TestHost host = TestHost.Build(before: services =>
        {
            services.AddSingleton<ISecretSource>(new SecretSourceFake());
            services.AddSingleton<IApproverDirectory, ApproverDirectoryFake>();
        });

        ApprovalsStartupException failure = await host.StartFailureAsync();

        Assert.Equal("approvals.startup.seammissing", failure.Code);
        Assert.Equal(new Dictionary<string, string> { ["key"] = "approvalObserver" }, failure.Details);
    }

    [Theory]
    [InlineData("Themis:Observer:Enabled")]
    [InlineData("Themis:Jobs:Enabled")]
    public async Task Start_ObserverMissingWhereItIsNotRequired_GoesPastTheSeams(string off)
    {
        await using TestHost host = TestHost.Build(new Dictionary<string, string?> { [off] = "false" }, before: services =>
        {
            services.AddSingleton<ISecretSource>(new SecretSourceFake());
            services.AddSingleton<IApproverDirectory, ApproverDirectoryFake>();
        });

        ApprovalsStartupException? failure = await Record.ExceptionAsync(host.StartAsync) as ApprovalsStartupException;

        Assert.NotEqual("approvals.startup.seammissing", failure?.Code);
    }

    [Fact]
    public async Task Start_SeamRegisteredAfterAddThemis_IsTaken()
    {
        await using TestHost host = TestHost.Build(after: TestHost.Seams(new SecretSourceFake()));

        ApprovalsStartupException? failure = await Record.ExceptionAsync(host.StartAsync) as ApprovalsStartupException;

        Assert.NotEqual("approvals.startup.seammissing", failure?.Code);
    }
}