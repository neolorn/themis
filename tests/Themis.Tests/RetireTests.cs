using System;
using System.Linq;
using System.Threading.Tasks;

using Themis.Core;

using Xunit;

namespace Themis.Tests;

/// <summary>Retiring approval types against a real database in a container, each test on a database of its own (<c>02</c> APR-TYPE-001).</summary>
[Trait("Category", "integration")]
public sealed class RetireTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task RetireAsync_PublishedType_IsRetiredUntilAPublishMakesItCurrentAgain()
    {
        await using TypeHost host = await TypeHost.StartAsync(postgres);
        await host.PublishAsync(Definitions.Valid);

        Result retired = await host.RetireAsync("purchase.order");
        ApprovalTypeVersion read = (await host.ReadAsync("purchase.order")).Value!;
        Result<PublishedVersion> again = await host.PublishAsync(Definitions.Valid);

        Assert.True(retired.IsSuccess);
        Assert.True(read.Retired);
        Assert.Equal((1, false), (again.Value!.Version, again.Value.Created));
        Assert.False((await host.ReadAsync("purchase.order")).Value!.Retired);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task RetireAsync_ThenAChangedDefinitionPublished_MakesTheNextVersionCurrent()
    {
        await using TypeHost host = await TypeHost.StartAsync(postgres);
        await host.PublishAsync(Definitions.Valid);
        await host.RetireAsync("purchase.order");

        Result<PublishedVersion> changed = await host.PublishAsync(Definitions.Valid with { Duplicates = DuplicateRule.RequireOther });

        Assert.Equal((2, true), (changed.Value!.Version, changed.Value.Created));
        Assert.False((await host.ReadAsync("purchase.order", 1)).Value!.Retired);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task RetireAsync_RetiredType_StaysRetired()
    {
        await using TypeHost host = await TypeHost.StartAsync(postgres);
        await host.PublishAsync(Definitions.Valid);
        await host.RetireAsync("purchase.order");

        Result again = await host.RetireAsync("purchase.order");

        Assert.True(again.IsSuccess);
        Assert.True((await host.ReadAsync("purchase.order")).Value!.Retired);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task RetireAsync_NoSuchType_AnswersTypeUnknownAndWritesNothing()
    {
        await using TypeHost host = await TypeHost.StartAsync(postgres);

        Result retired = await host.RetireAsync("purchase.order");

        Assert.Equal("approvals.type.unknown", retired.Failure?.Code);
        Assert.Equal(0L, await host.TypeCountAsync());
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task RetireAsync_TypesLockHeld_WaitsForIt()
    {
        await using TypeHost host = await TypeHost.StartAsync(postgres);
        await host.PublishAsync(Definitions.Valid);
        await using TypeHost.HeldLock held = await host.HoldTypeLockAsync("purchase.order");

        Task<Result> retire = Task.Run(() => host.RetireAsync("purchase.order"));
        await held.WaitForWaitersAsync(1);
        Assert.False(retire.IsCompleted);
        await held.ReleaseAsync();

        Assert.True((await retire).IsSuccess);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task RetireAsync_Retirement_IsLoggedWithTheKeyAndTheVersion()
    {
        await using TypeHost host = await TypeHost.StartAsync(postgres);
        await host.PublishAsync(Definitions.Valid);
        await host.PublishAsync(Definitions.Valid with { Duplicates = DuplicateRule.RequireOther });

        await host.RetireAsync("purchase.order");

        string retired = Assert.Single(host.Logs.Entries, entry => entry.Category == "Themis.Types.ApprovalTypeService" && entry.Message.Contains("retired", StringComparison.Ordinal)).Message;
        Assert.Matches(@"purchase\.order.*\bversion 2\b", retired);
        host.AssertNoLogHoldsAValueUsed();
    }
}