using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Themis.Core;

using Xunit;

using static Themis.Tests.Requests;

namespace Themis.Tests;

/// <summary>A target's requests and a person's export, against a real database in a container, each test on a database of its own (<c>01</c> APR-REQ-013, APR-REQ-015).</summary>
[Trait("Category", "integration")]
public sealed class ExportTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Midnight = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task APR_REQ_015_AC2_ExportHoldsEveryRequestAssignmentDecisionAndDelegationThatNamesThePersonAndNoneThatDoesNot()
    {
        ClockFake clock = new(Midnight);
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: clock);
        await host.PublishAsync(Type(Step("review", ApprovalMode.AnyOne, Person(A), Person(B))));
        DelegationId lent = await host.DelegatedAsync(Lend(B, C));
        DelegationId borrowed = await host.DelegatedAsync(Lend(A, B));
        await host.DelegatedAsync(Lend(C, A));
        RequestId made = await host.CreatedAsync(On("order-made") with { Requester = B });
        RequestId subject = await host.CreatedAsync(On("order-subject") with { Subject = B });
        RequestId decided = await host.CreatedAsync(On("order-decided"));
        RequestId forThem = await host.CreatedAsync(On("order-for"));
        RequestId other = await host.CreatedAsync(On("order-other"));
        DecisionId byThem = (await host.DecideAsync(decided, B, DecisionOutcome.Reject, Reason)).Value!.Decision;
        DecisionId madeForThem = (await host.DecideAsync(forThem, C, reason: Reason, actingFor: B)).Value!.Decision;
        await host.DecideAsync(other, A);

        PersonExport export = await host.ExportAsync(B);

        Assert.Equal(Sorted(made, subject), Sorted([.. export.Requests.Select(record => record.Request)]));
        Assert.Equal(Sorted(decided, forThem, other), Sorted([.. export.Assignments.Select(assignment => assignment.Request)]));
        Assert.All(export.Assignments, assignment => Assert.Equal((1, AssignmentOrigin.Approver), (assignment.Step, assignment.Origin)));
        Assert.Equal(byThem, export.Assignments.Single(assignment => assignment.Request == decided).Decision);
        Assert.Equal([(byThem, decided, B, (string?)null, (string?)Reason), (madeForThem, forThem, C, B, Reason)], export.Decisions.OrderBy(decision => decision.At).ThenBy(decision => decision.Decision == madeForThem).Select(decision => (decision.Decision, decision.Request, decision.Decider, decision.ActingFor, decision.Reason)));
        Assert.Equal(new[] { lent, borrowed }.OrderBy(found => found.Value), export.Delegations.Select(found => found.Delegation).OrderBy(found => found.Value));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task ByTarget_TargetWithRequests_AnswersThemNewestFirstAndNoneOfAnotherTarget()
    {
        ClockFake clock = new(Midnight);
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: clock);
        await host.PublishAsync(Type(Step("review", ApprovalMode.AnyOne, Person(A))));
        RequestId first = await host.CreatedAsync(Request());
        await host.CancelAsync(first, Requester);
        clock.Advance(TimeSpan.FromMinutes(1));
        RequestId second = await host.CreatedAsync(Request());
        await host.CreatedAsync(On("order-elsewhere"));

        IReadOnlyList<RequestSummary> summaries = await host.ByTargetAsync(Requests.Target);

        Assert.Equal([second, first], summaries.Select(summary => summary.Request));
        Assert.Equal((Key, 1, RequestStatus.Pending, Midnight + TimeSpan.FromMinutes(1), (DateTimeOffset?)null), (summaries[0].Type, summaries[0].Version, summaries[0].Status, summaries[0].CreatedAt, summaries[0].ClosedAt));
        Assert.Equal((RequestStatus.Cancelled, (DateTimeOffset?)Midnight), (summaries[1].Status, summaries[1].ClosedAt));
        host.AssertNoLogHoldsAValueUsed();
    }

    private static ApprovalRequest On(string target)
    {
        return Request() with { Target = new Target { Kind = "order", Id = target } };
    }

    private static DelegationDefinition Lend(string from, string to)
    {
        return new DelegationDefinition { From = from, To = to, Starts = Midnight, Ends = Midnight + TimeSpan.FromDays(30), CreatedBy = Requester };
    }

    private static Guid[] Sorted(params RequestId[] requests)
    {
        return [.. requests.Select(request => request.Value).Order()];
    }
}