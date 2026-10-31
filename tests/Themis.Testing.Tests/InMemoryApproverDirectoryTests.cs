using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Themis.Core;

using Xunit;

namespace Themis.Testing.Tests;

/// <summary>The in-memory directory a host's tests fill (<c>03</c> APR-LIB-007).</summary>
[Trait("Category", "unit")]
public sealed class InMemoryApproverDirectoryTests
{
    private static readonly ApproverQuery Query = new()
    {
        Request = new RequestId(Guid.CreateVersion7()),
        Type = "purchase",
        Version = 1,
        Target = new Target { Kind = "order", Id = "order-1" },
        Requester = "person-requester",
        Step = 1,
    };

    [Fact]
    public async Task ResolveAsync_PersonsGranted_AnswersThemInTheOrderGrantedLessThoseRevoked()
    {
        InMemoryApproverDirectory directory = new();
        directory.Grant("orders.approve", "person-b");
        directory.Grant("orders.approve", "person-a");
        directory.Grant("orders.approve", "person-c");
        directory.Grant("orders.approve", "person-a");
        directory.Revoke("orders.approve", "person-c");

        IReadOnlyList<string> persons = await directory.ResolveAsync("orders.approve", Query, TestContext.Current.CancellationToken);

        Assert.Equal(["person-b", "person-a"], persons);
        Assert.Empty(await directory.ResolveAsync("orders.other", Query, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanApproveAsync_ByItsVia_AnswersWhetherThePersonIsNamedOrHoldsThePermissionAndRecordsTheCheck()
    {
        InMemoryApproverDirectory directory = new();
        directory.Grant("orders.approve", "person-a");

        bool[] answers =
        [
            await CheckAsync(directory, "person-a", new ApproverReference.Permission("orders.approve")),
            await CheckAsync(directory, "person-b", new ApproverReference.Permission("orders.approve")),
            await CheckAsync(directory, "person-b", new ApproverReference.Person("person-b")),
            await CheckAsync(directory, "person-b", new ApproverReference.Person("person-a")),
            await CheckAsync(directory, "person-b", new ApproverReference.Permission("orders.approve"), actingFor: "person-a"),
        ];

        Assert.Equal([true, false, true, false, true], answers);
        Assert.Equal(5, directory.Checks.Count);
    }

    [Fact]
    public async Task CanApproveAsync_BlockedPerson_AnswersFalseUntilUnblocked()
    {
        InMemoryApproverDirectory directory = new();
        directory.Block("person-a");

        bool blocked = await CheckAsync(directory, "person-a", new ApproverReference.Person("person-a"), actingFor: "person-b");
        directory.Unblock("person-a");

        Assert.False(blocked);
        Assert.True(await CheckAsync(directory, "person-a", new ApproverReference.Person("person-a")));
    }

    [Fact]
    public async Task FailWith_Failure_MakesEveryCallThrowItUntilCleared()
    {
        InMemoryApproverDirectory directory = new();
        InvalidOperationException failure = new("directory down");
        directory.FailWith(failure);

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(async () => await directory.ResolveAsync("orders.approve", Query, TestContext.Current.CancellationToken)));
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => CheckAsync(directory, "person-a", new ApproverReference.Person("person-a"))));
        directory.FailWith(null);
        Assert.True(await CheckAsync(directory, "person-a", new ApproverReference.Person("person-a")));
    }

    private static async Task<bool> CheckAsync(InMemoryApproverDirectory directory, string person, ApproverReference via, string? actingFor = null)
    {
        return await directory.CanApproveAsync(new ApproverCheck { Person = person, Via = via, ActingFor = actingFor, Query = Query }, TestContext.Current.CancellationToken);
    }
}