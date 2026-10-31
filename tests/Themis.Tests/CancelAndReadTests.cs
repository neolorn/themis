using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Themis.Core;

using Xunit;

using static Themis.Tests.Requests;

namespace Themis.Tests;

/// <summary>Cancelling and reading requests against a real database in a container, each test on a database of its own (<c>01</c> APR-REQ-010, APR-REQ-013; <c>04</c> APR-OPS-005).</summary>
[Trait("Category", "integration")]
public sealed class CancelAndReadTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly ApprovalTypeDefinition OneStep = Type(Step("review", ApprovalMode.AnyOne, Person(A)));

    [Fact]
    public async Task APR_REQ_010_AC1_PendingRequestCancelledByItsRequesterEndsCancelledWithTheActorRecorded()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(OneStep);
        RequestId request = await host.CreatedAsync(Request());

        Result cancelled = await host.CancelAsync(request, Requester, reason: Reason);
        RequestRecord record = await host.ReadAsync(request);

        Assert.True(cancelled.IsSuccess);
        Assert.Equal((RequestStatus.Cancelled, Requester, (string?)null, Reason, (ReasonCode?)null), (record.Status, record.ClosedBy, record.ClosedFor, record.Reason, record.ReasonCode));
        Assert.Equal(StepStatus.Closed, record.Steps[0].Status);
        RecordedEvent last = (await host.EventsAsync(request))[^1];
        Assert.Equal((EventKind.Cancelled, Requester), (last.Kind, last.Actor));
        Assert.Equal([Requester, Watcher], last.Notify);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task Cancel_ApprovedRequest_EndsCancelledWithTheActorAndThePersonActedFor()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(OneStep);
        RequestId request = await host.CreatedAsync(Request());
        await host.DecideAsync(request, A);

        Result cancelled = await host.CancelAsync(request, C, onBehalfOf: Requester);
        RequestRecord record = await host.ReadAsync(request);

        Assert.True(cancelled.IsSuccess);
        Assert.Equal((RequestStatus.Cancelled, C, Requester), (record.Status, record.ClosedBy, record.ClosedFor));
        RecordedEvent last = (await host.EventsAsync(request))[^1];
        Assert.Equal((EventKind.Cancelled, C, Requester, (int?)null), (last.Kind, last.Actor, last.ActingFor, last.Step));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task Cancel_ClosedUnknownOrReasonTooLong_IsRefusedAndChangesNothing()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(OneStep);
        RequestId request = await host.CreatedAsync(Request());
        await host.CancelAsync(request, Requester);
        string before = await host.CountsAsync();

        Result closed = await host.CancelAsync(request, Requester);
        Result unknown = await host.CancelAsync(new RequestId(Guid.CreateVersion7()), Requester);
        Result tooLong = await host.CancelAsync(request, Requester, reason: new string('r', 2001));

        Assert.Equal(["approvals.request.closed", "approvals.request.unknown", "approvals.reason.invalid"], new[] { closed, unknown, tooLong }.Select(result => result.Failure?.Code));
        Assert.Equal(before, await host.CountsAsync());
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_013_AC2_RequestDecidedByThePersonOrPastItsStepLeavesTheirPendingList()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(Type(Step("review", ApprovalMode.All, Person(A), Person(B)), Step("finance", ApprovalMode.AnyOne, Person(C))));
        RequestId first = await host.CreatedAsync(Request());
        RequestId second = await host.CreatedAsync(Request() with { Target = new Target { Kind = "order", Id = "order-second" } });

        IReadOnlyList<PendingItem> before = (await host.PendingAsync(A)).Value!.Items;
        await host.DecideAsync(first, A);
        IReadOnlyList<PendingItem> decided = (await host.PendingAsync(A)).Value!.Items;
        await host.DecideAsync(second, A);
        await host.DecideAsync(second, B);
        IReadOnlyList<PendingItem> pastB = (await host.PendingAsync(B)).Value!.Items;

        Assert.Equal([first, second], before.Select(item => item.Request));
        Assert.Equal([second], decided.Select(item => item.Request));
        Assert.Equal([first], pastB.Select(item => item.Request));
        PendingItem item = before[0];
        Assert.Equal((Key, "order", TargetId, 1, "review", (string?)null), (item.Type, item.Target.Kind, item.Target.Id, item.Step, item.StepName, item.ActingFor));
        Assert.Equal([second], (await host.PendingAsync(C)).Value!.Items.Select(found => found.Request));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task Pending_PagesOfOne_AnswerTheRequestsOldestFirstThenNoCursor()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(OneStep);
        RequestId[] created = [await host.CreatedAsync(Request()), await host.CreatedAsync(Request() with { Target = new Target { Kind = "order", Id = "order-2" } }), await host.CreatedAsync(Request() with { Target = new Target { Kind = "order", Id = "order-3" } })];

        List<RequestId> read = [];
        string? cursor = null;
        int pages = 0;
        do
        {
            PendingPage page = (await host.PendingAsync(A, cursor, size: 1)).Value!;
            read.AddRange(page.Items.Select(item => item.Request));
            cursor = page.Next;
            pages++;
        }
        while (cursor is not null && pages < 10);

        Assert.Equal(created, read);
        Assert.Equal(3, pages);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task Pending_CursorThatDoesNotRead_IsRefusedCursorInvalid()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);

        Result<PendingPage> refused = await host.PendingAsync(A, "not-a-cursor");

        Assert.Equal("approvals.request.cursorinvalid", refused.Failure?.Code);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => host.PendingAsync(A, size: 101));
    }

    [Fact]
    public async Task Pending_RequesterOrSubject_IsLeftOut()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(OneStep);
        await host.CreatedAsync(Request(subject: Subject));

        Assert.Empty((await host.PendingAsync(Requester)).Value!.Items);
        Assert.Empty((await host.PendingAsync(Subject)).Value!.Items);
        Assert.Single((await host.PendingAsync(A)).Value!.Items);
    }

    [Fact]
    public async Task Read_UnknownRequest_IsRefusedUnknown()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);

        Result<RequestRecord> refused = await host.CallAsync<IApprovals, Result<RequestRecord>>(approvals => approvals.ReadAsync(new RequestId(Guid.CreateVersion7()), TestContext.Current.CancellationToken));

        Assert.Equal("approvals.request.unknown", refused.Failure?.Code);
    }

    [Fact]
    public async Task Read_DecidedRequest_AnswersItsWholeRecord()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        PublishedVersion published = await host.PublishAsync(OneStep);
        ApprovalRequest asked = Request(subject: Subject) with { SubjectRoles = [Buyer] };
        RequestId request = await host.CreatedAsync(asked);

        Result<DecisionReceipt> decided = await host.DecideAsync(request, A, reason: Reason);
        RequestRecord record = await host.ReadAsync(request);

        Assert.Equal((Key, published.Version, "main", Requests.Target, Requester, Subject, RequestStatus.Approved), (record.Type, record.Version, record.Policy, record.Target, record.Requester, record.Subject, record.Status));
        Assert.Equal(asked.Snapshot.OrderBy(field => field.Key, StringComparer.Ordinal), record.Snapshot.OrderBy(field => field.Key, StringComparer.Ordinal));
        Assert.Equal([Buyer], record.RequesterRoles);
        Assert.Equal([Buyer], record.SubjectRoles);
        Assert.NotNull(record.ApprovedAt);
        DecisionRecord decision = Assert.Single(record.Decisions);
        Assert.Equal((decided.Value!.Decision, 1, A, DecisionOutcome.Approve, Reason), (decision.Decision, decision.Step, decision.Decider, decision.Outcome, decision.Reason));
        StepRecord step = Assert.Single(record.Steps);
        Assert.Equal(("review", ApprovalMode.AnyOne, StepStatus.Done), (step.Name, step.Mode, step.Status));
        Assert.Equal(decision.Decision, Assert.Single(step.Assignments).Decision);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task Logs_RequestsLifeToItsCancelAndItsRefusals_HoldNoPersonTargetRoleValueOrReason()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(Type(Step("review", ApprovalMode.AnyOne, Permission(Approve)), Step("finance", ApprovalMode.All, Person(B), Person(C))));
        host.Directory.Grant(Approve, A);
        RequestId request = await host.CreatedAsync(Request(subject: Subject) with { SubjectRoles = [Buyer] });

        await host.CreateAsync(Request(subject: Subject));
        await host.DecideAsync(request, Late, reason: Reason);
        await host.DecideAsync(request, A, DecisionOutcome.Reject);
        await host.DecideAsync(request, A, reason: Reason);
        await host.DecideAsync(request, B, reason: Reason);
        await host.PendingAsync(C);
        await host.DecideAsync(request, C, reason: Reason);
        await host.CancelAsync(request, Requester, onBehalfOf: Subject, reason: Reason);
        RequestId rejected = await host.CreatedAsync(Request() with { Target = new Target { Kind = "order", Id = "order-other" } });
        await host.DecideAsync(rejected, A, DecisionOutcome.Reject, Reason);

        Assert.Equal(RequestStatus.Cancelled, (await host.ReadAsync(request)).Status);
        Assert.NotEmpty(host.Logs.Entries);
        host.AssertNoLogHoldsAValueUsed();
    }
}