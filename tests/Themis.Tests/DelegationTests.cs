using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Themis.Core;

using Xunit;

using static Themis.Tests.Requests;

namespace Themis.Tests;

/// <summary>Delegations and the decisions made through them against a real database in a container, each test on a database of its own (<c>01</c> APR-REQ-008, APR-REQ-004, APR-REQ-013).</summary>
[Trait("Category", "integration")]
public sealed class DelegationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Other = "purchase.other";

    private static readonly DateTimeOffset Midnight = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly ApprovalTypeDefinition OneStep = Type(Step("review", ApprovalMode.AnyOne, Person(A)));

    [Fact]
    public async Task APR_REQ_008_AC1_DelegationToOneselfIsRefused()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: new ClockFake(Midnight));

        Result<DelegationId> refused = await host.DelegateAsync(Lend(A, A));

        Assert.Equal(("approvals.delegation.invalid", "to"), (refused.Failure?.Code, refused.Failure?.Details["member"]));
        Assert.Empty(await host.DelegationsAsync(A));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task Create_EndsNotAfterStartsOrNotAfterNowOrPeriodOver365Days_IsRefusedNamingEnds()
    {
        ClockFake clock = new(Midnight);
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: clock);

        Result<DelegationId>[] refused =
        [
            await host.DelegateAsync(Lend(A, B) with { Ends = Midnight + TimeSpan.FromDays(1), Starts = Midnight + TimeSpan.FromDays(1) }),
            await host.DelegateAsync(Lend(A, B) with { Starts = Midnight - TimeSpan.FromDays(2), Ends = Midnight }),
            await host.DelegateAsync(Lend(A, B) with { Ends = Midnight + TimeSpan.FromDays(365) + TimeSpan.FromSeconds(1) }),
        ];
        Result<DelegationId> longest = await host.DelegateAsync(Lend(A, B) with { Ends = Midnight + TimeSpan.FromDays(365) });

        Assert.All(refused, result => Assert.Equal(("approvals.delegation.invalid", "ends"), (result.Failure?.Code, result.Failure?.Details["member"])));
        Assert.True(longest.IsSuccess);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_008_AC2_DelegateDecidesForTheLenderWithinThePeriodAndIsRefusedNotAssignedAfterItEndsOrIsEnded()
    {
        ClockFake clock = new(Midnight);
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: clock);
        await host.PublishAsync(OneStep);
        await host.DelegatedAsync(Lend(A, B) with { Starts = Midnight + TimeSpan.FromHours(1), Ends = Midnight + TimeSpan.FromHours(2) });
        List<RequestId> requests = [];
        foreach (int number in Enumerable.Range(1, 4))
        {
            requests.Add(await host.CreatedAsync(Request() with { Target = new Target { Kind = "order", Id = $"order-{number}" } }));
        }


        Result<DecisionReceipt> before = await host.DecideAsync(requests[0], B, actingFor: A);
        clock.Advance(TimeSpan.FromMinutes(90));
        Result<DecisionReceipt> within = await host.DecideAsync(requests[0], B, actingFor: A);
        clock.Advance(TimeSpan.FromMinutes(30));
        Result<DecisionReceipt> after = await host.DecideAsync(requests[1], B, actingFor: A);
        DelegationId ended = await host.DelegatedAsync(Lend(A, B) with { Ends = Midnight + TimeSpan.FromDays(1) });
        Result<DecisionReceipt> live = await host.DecideAsync(requests[2], B, actingFor: A);
        Result endedNow = await host.EndDelegationAsync(ended, A);
        Result<DecisionReceipt> afterEnded = await host.DecideAsync(requests[3], B, actingFor: A);

        Assert.Equal("approvals.decision.notassigned", before.Failure?.Code);
        Assert.Equal(RequestStatus.Approved, within.Value?.Status);
        Assert.Equal("approvals.decision.notassigned", after.Failure?.Code);
        Assert.Equal(RequestStatus.Approved, live.Value?.Status);
        Assert.True(endedNow.IsSuccess);
        Assert.Equal("approvals.decision.notassigned", afterEnded.Failure?.Code);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_008_AC3_DelegateOfADelegateCannotDecideForTheFirstLender()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: new ClockFake(Midnight));
        await host.PublishAsync(OneStep);
        await host.DelegatedAsync(Lend(A, B));
        await host.DelegatedAsync(Lend(B, C));
        RequestId request = await host.CreatedAsync(Request());

        Result<DecisionReceipt> forFirst = await host.DecideAsync(request, C, actingFor: A);
        Result<DecisionReceipt> forSecond = await host.DecideAsync(request, C, actingFor: B);

        Assert.Equal(("approvals.decision.notassigned", "approvals.decision.notassigned"), (forFirst.Failure?.Code, forSecond.Failure?.Code));
        Assert.Empty((await host.ReadAsync(request)).Decisions);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_008_AC4_DecisionThroughADelegationRecordsTheDeciderAndThePersonActedFor()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: new ClockFake(Midnight));
        await host.PublishAsync(Type(Step("review", ApprovalMode.AnyOne, Permission(Approve))));
        host.Directory.Grant(Approve, A);
        await host.DelegatedAsync(Lend(A, B));
        RequestId request = await host.CreatedAsync(Request());

        Result<DecisionReceipt> decided = await host.DecideAsync(request, B, reason: Reason, actingFor: A);
        RequestRecord record = await host.ReadAsync(request);

        DecisionRecord decision = Assert.Single(record.Decisions);
        Assert.Equal((decided.Value?.Decision, B, A), (decision.Decision, decision.Decider, decision.ActingFor));
        Assert.Equal(decision.Decision, Assert.Single(record.Steps[0].Assignments).Decision);
        Assert.Equal([(B, (string?)A), (A, null)], host.Directory.Checks.Select(check => (check.Person, check.ActingFor)));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task Decide_LenderTheDirectoryNoLongerAnswers_IsRefusedNotPermitted()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: new ClockFake(Midnight));
        await host.PublishAsync(Type(Step("review", ApprovalMode.AnyOne, Permission(Approve))));
        host.Directory.Grant(Approve, A);
        await host.DelegatedAsync(Lend(A, B));
        RequestId request = await host.CreatedAsync(Request());

        host.Directory.Revoke(Approve, A);
        Result<DecisionReceipt> refused = await host.DecideAsync(request, B, actingFor: A);

        Assert.Equal("approvals.decision.notpermitted", refused.Failure?.Code);
        Assert.Empty((await host.ReadAsync(request)).Decisions);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task Decide_ThroughADelegationNotCoveringTheType_IsRefusedNotAssigned()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: new ClockFake(Midnight));
        await host.PublishAsync(OneStep);
        await host.DelegatedAsync(Lend(A, B) with { Types = [Other] });
        RequestId request = await host.CreatedAsync(Request());

        Result<DecisionReceipt> refused = await host.DecideAsync(request, B, actingFor: A);

        Assert.Equal("approvals.decision.notassigned", refused.Failure?.Code);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task Decide_DelegateWhoDecidedTheStepInTheirOwnName_IsRefusedDuplicateActingForTheLender()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: new ClockFake(Midnight));
        await host.PublishAsync(Type(Step("review", ApprovalMode.All, Person(A), Person(B))));
        await host.DelegatedAsync(Lend(A, B));
        RequestId request = await host.CreatedAsync(Request());

        await host.DecideAsync(request, B);
        Result<DecisionReceipt> refused = await host.DecideAsync(request, B, actingFor: A);

        Assert.Equal("approvals.decision.duplicate", refused.Failure?.Code);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_004_AC2_DelegateWhoIsTheRequestsSubjectIsRefusedSeparation()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: new ClockFake(Midnight));
        await host.PublishAsync(OneStep);
        await host.DelegatedAsync(Lend(A, Subject));
        RequestId request = await host.CreatedAsync(Request(subject: Subject));

        Result<DecisionReceipt> refused = await host.DecideAsync(request, Subject, actingFor: A);

        Assert.Equal("approvals.decision.separation", refused.Failure?.Code);
        Assert.Empty((await host.ReadAsync(request)).Decisions);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_013_AC1_DelegatesPendingListHoldsTheLendersOpenAssignmentsOfTheCoveredTypesMarkedAsActedForTheLenderAndNoneOfAnotherType()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: new ClockFake(Midnight));
        await host.PublishAsync(OneStep);
        await host.PublishAsync(OneStep with { Key = Other });
        await host.DelegatedAsync(Lend(A, B) with { Types = [Key] });
        RequestId covered = await host.CreatedAsync(Request());
        await host.CreatedAsync(Request() with { Type = Other });

        PendingPage page = (await host.PendingAsync(B)).Value!;

        PendingItem item = Assert.Single(page.Items);
        Assert.Equal((covered, Key, A), (item.Request, item.Type, item.ActingFor));
        Assert.Equal(2, (await host.PendingAsync(A)).Value!.Items.Count);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task Pending_OwnAndLentAssignmentsOfOneRequest_ListItOnceAsTheirOwn()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: new ClockFake(Midnight));
        await host.PublishAsync(Type(Step("review", ApprovalMode.All, Person(A), Person(B))));
        await host.DelegatedAsync(Lend(A, B));
        RequestId request = await host.CreatedAsync(Request());

        PendingItem item = Assert.Single((await host.PendingAsync(B)).Value!.Items);

        Assert.Equal((request, (string?)null), (item.Request, item.ActingFor));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task Create_Delegation_WritesItsDelegationCreatedEventNamingTheDelegate()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: new ClockFake(Midnight));

        DelegationId delegation = await host.DelegatedAsync(Lend(A, B));
        await host.RunAsync(ApprovalJob.Observer);

        ApprovalEvent created = Assert.Single(host.Observer.Received);
        Assert.Equal((EventKind.DelegationCreated, 1L, (RequestId?)null, (DelegationId?)delegation, (string?)Requester), (created.Kind, created.Sequence, created.Request, created.Delegation, created.Actor));
        Assert.Equal([B], created.Notify);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task End_LiveDelegation_RecordsItsActorAndInstantAndAnUnknownOrEndedOneIsRefused()
    {
        ClockFake clock = new(Midnight);
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: clock);
        DelegationId delegation = await host.DelegatedAsync(Lend(A, B));
        DelegationId lapsed = await host.DelegatedAsync(Lend(A, C) with { Ends = Midnight + TimeSpan.FromHours(1) });
        clock.Advance(TimeSpan.FromMinutes(30));
        Result live = await host.EndDelegationAsync(delegation, Requester);
        clock.Advance(TimeSpan.FromMinutes(30));

        Result ended = await host.EndDelegationAsync(delegation, A);
        Result passed = await host.EndDelegationAsync(lapsed, A);
        Result unknown = await host.EndDelegationAsync(new DelegationId(Guid.CreateVersion7()), A);

        Assert.True(live.IsSuccess);
        Assert.Equal((Midnight + TimeSpan.FromMinutes(30), (string?)Requester), await host.DelegationEndAsync(delegation));
        Assert.Equal(("approvals.delegation.ended", "approvals.delegation.ended", "approvals.delegation.unknown"), (ended.Failure?.Code, passed.Failure?.Code, unknown.Failure?.Code));
        Assert.Equal(((DateTimeOffset?)null, (string?)null), await host.DelegationEndAsync(lapsed));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task List_Person_AnswersTheDelegationsFromAndToThemThatHaveNotEnded()
    {
        ClockFake clock = new(Midnight);
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: clock);
        DelegationId from = await host.DelegatedAsync(Lend(A, B) with { Types = [Key] });
        DelegationId to = await host.DelegatedAsync(Lend(C, A));
        DelegationId ended = await host.DelegatedAsync(Lend(A, C));
        await host.DelegatedAsync(Lend(A, Late) with { Ends = Midnight + TimeSpan.FromHours(1) });
        await host.DelegatedAsync(Lend(B, C));
        await host.EndDelegationAsync(ended, A);
        clock.Advance(TimeSpan.FromHours(1));

        IReadOnlyList<DelegationRecord> listed = await host.DelegationsAsync(A);

        Assert.Equal(new[] { from, to }.OrderBy(found => found.Value), listed.Select(found => found.Delegation).OrderBy(found => found.Value));
        DelegationRecord record = listed.Single(found => found.Delegation == from);
        Assert.Equal((A, B, Midnight, Midnight + TimeSpan.FromDays(30), Requester, Midnight), (record.From, record.To, record.Starts, record.Ends, record.CreatedBy, record.CreatedAt));
        Assert.Equal([Key], record.Types);
        host.AssertNoLogHoldsAValueUsed();
    }

    /// <summary>A delegation from <paramref name="from"/> to <paramref name="to"/> of every type, from now for 30 days, recorded by <see cref="Requester"/>.</summary>
    private static DelegationDefinition Lend(string from, string to)
    {
        return new DelegationDefinition { From = from, To = to, Starts = Midnight, Ends = Midnight + TimeSpan.FromDays(30), CreatedBy = Requester };
    }
}