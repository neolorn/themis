using System;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Themis.Operations;

using Xunit;

namespace Themis.Tests;

[Trait("Category", "unit")]
public sealed class ReadyTests
{
    [Fact]
    public async Task ApprovalsReady_BeforeTheStartupChecks_ThrowsInvalidOperationException()
    {
        await using TestHost host = TestHost.Build(before: TestHost.Seams(new SecretSourceFake()));

        Assert.Throws<InvalidOperationException>(host.Services.GetRequiredService<ApprovalsReady>().ThrowIfNotReady);
    }

    [Fact]
    public async Task ApprovalsReady_AfterAFailedStart_StillThrows()
    {
        await using TestHost host = TestHost.Build();
        await host.StartFailureAsync();

        Assert.Throws<InvalidOperationException>(host.Services.GetRequiredService<ApprovalsReady>().ThrowIfNotReady);
    }
}