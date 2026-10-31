using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Themis.Core;
using Themis.Testing;

using Xunit;

using static Themis.Tests.Requests;

namespace Themis.Tests;

/// <summary>The deadlines job's actions against a real database in a container, each test on a database of its own (<c>01</c> APR-REQ-009; <c>03</c> APR-LIB-007; <c>04</c> APR-OPS-004).</summary>
[Trait("Category", "integration")]
public sealed class DeadlineTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Midnight = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    [Fact]
    public async Task APR_REQ_009_AC1_StepWithAOneHourDeadlineAndRejectIsRejectedWithDeadlineOnceTheClockPassesTheHourAndNotBefore()
    {
        ClockFake clock = new(Midnight);
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: clock);
        await host.PublishAsync(Type(Due(Step("review", ApprovalMode.AnyOne, Person(A)), DeadlineAction.Reject)));
        RequestId request = await host.CreatedAsync(Request());

        clock.Advance(Hour - TimeSpan.FromSeconds(1));
        int before = await host.RunAsync(ApprovalJob.Deadlines);
        RequestStatus pending = (await host.ReadAsync(request)).Status;
        clock.Advance(TimeSpan.FromSeconds(1));
        int at = await host.RunAsync(ApprovalJob.Deadlines);
        RequestRecord record = await host.ReadAsync(request);

        Assert.Equal((0, RequestStatus.Pending, 1), (before, pending, at));
        Assert.Equal((RequestStatus.Rejected, (ReasonCode?)ReasonCode.Deadline, (string?)null), (record.Status, record.ReasonCode, record.ClosedBy));
        Assert.Equal((StepStatus.Closed, (DateTimeOffset?)(Midnight + Hour)), (record.Steps[0].Status, record.Steps[0].DeadlineRanAt));
        RecordedEvent last = (await host.EventsAsync(request))[^1];
        Assert.Equal((EventKind.Rejected, (string?)null, (ReasonCode?)ReasonCode.Deadline, (int?)1), (last.Kind, last.Actor, last.ReasonCode, last.Step));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task Deadline_Cancel_CancelsTheRequestWithDeadline()
    {
        ClockFake clock = new(Midnight);
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: clock);
        await host.PublishAsync(Type(Due(Step("review", ApprovalMode.AnyOne, Person(A)), DeadlineAction.Cancel)));
        RequestId request = await host.CreatedAsync(Request());

        clock.Advance(Hour);
        await host.RunAsync(ApprovalJob.Deadlines);
        RequestRecord record = await host.ReadAsync(request);

        Assert.Equal((RequestStatus.Cancelled, (ReasonCode?)ReasonCode.Deadline, (string?)null), (record.Status, record.ReasonCode, record.ClosedBy));
        Assert.Equal(EventKind.Cancelled, (await host.EventsAsync(request))[^1].Kind);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_009_AC2_EscalatedAllStepCompletesAtTheFirstApprovalOfItsEscalationAssignee()
    {
        ClockFake clock = new(Midnight);
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: clock);
        await host.PublishAsync(Type(Due(Step("review", ApprovalMode.All, Person(A), Person(B)), DeadlineAction.Escalate, Person(C))));
        RequestId request = await host.CreatedAsync(Request());

        clock.Advance(Hour);
        int handled = await host.RunAsync(ApprovalJob.Deadlines);
        RequestRecord escalated = await host.ReadAsync(request);
        Result<DecisionReceipt> own = await host.DecideAsync(request, A);
        Result<DecisionReceipt> escalation = await host.DecideAsync(request, C);

        Assert.Equal(1, handled);
        Assert.Equal([(A, AssignmentOrigin.Approver), (B, AssignmentOrigin.Approver), (C, AssignmentOrigin.Escalation)], Origins(escalated));
        RecordedEvent added = (await host.EventsAsync(request)).Single(found => found.Kind == EventKind.Escalated);
        Assert.Equal(((int?)1, (string?)null), (added.Step, added.Actor));
        Assert.Equal([C], added.Notify);
        Assert.Equal((RequestStatus.Pending, RequestStatus.Approved), (own.Value?.Status, escalation.Value?.Status));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_009_AC3_StepEscalatedOnceIsNotEscalatedAgain()
    {
        ClockFake clock = new(Midnight);
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: clock);
        await host.PublishAsync(Type(Due(Step("review", ApprovalMode.AnyOne, Person(A)), DeadlineAction.Escalate, Permission(Approve))));
        host.Directory.Grant(Approve, C);
        RequestId request = await host.CreatedAsync(Request());

        clock.Advance(Hour);
        int first = await host.RunAsync(ApprovalJob.Deadlines);
        host.Directory.Grant(Approve, B);
        clock.Advance(Hour * 3);
        int second = await host.RunAsync(ApprovalJob.Deadlines);

        Assert.Equal((1, 0), (first, second));
        Assert.Single(await host.EventsAsync(request), found => found.Kind == EventKind.Escalated);
        Assert.Equal([(A, AssignmentOrigin.Approver), (C, AssignmentOrigin.Escalation)], Origins(await host.ReadAsync(request)));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task Deadline_EscalateToExcludedOrAlreadyAssigned_AddsOnlyThePersonsLeft()
    {
        ClockFake clock = new(Midnight);
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: clock);
        await host.PublishAsync(Type(Due(Step("review", ApprovalMode.AnyOne, Person(A)), DeadlineAction.Escalate, Permission(Approve))));
        host.Directory.Grant(Approve, Requester);
        host.Directory.Grant(Approve, A);
        host.Directory.Grant(Approve, B);
        RequestId request = await host.CreatedAsync(Request());

        clock.Advance(Hour);
        await host.RunAsync(ApprovalJob.Deadlines);
        RequestRecord record = await host.ReadAsync(request);

        Assert.Equal([(A, AssignmentOrigin.Approver), (B, AssignmentOrigin.Escalation)], Origins(record));
        Assert.Equal([B], (await host.EventsAsync(request)).Single(found => found.Kind == EventKind.Escalated).Notify);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task Deadline_EscalationLeavingNoOne_GoesToTheStepsFallback()
    {
        ClockFake clock = new(Midnight);
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: clock);
        await host.PublishAsync(Type(Due(Step("review", ApprovalMode.AnyOne, Person(A)), DeadlineAction.Escalate, Person(Requester))));
        RequestId request = await host.CreatedAsync(Request());

        clock.Advance(Hour);
        await host.RunAsync(ApprovalJob.Deadlines);
        RequestRecord record = await host.ReadAsync(request);

        Assert.Equal([(A, AssignmentOrigin.Approver), (StepFallback, AssignmentOrigin.Fallback)], Origins(record));
        Assert.Equal([StepFallback], (await host.EventsAsync(request)).Single(found => found.Kind == EventKind.Escalated).Notify);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task Deadline_EscalationWithBothFallbacksExcluded_KeepsTheStepsAssigneesAndRaisesRequestUnassignable()
    {
        ClockFake clock = new(Midnight);
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: clock);
        StepDefinition step = Due(Step("review", ApprovalMode.AnyOne, Person(A)), DeadlineAction.Escalate, Person(Subject)) with { Fallback = Subject };
        await host.PublishAsync(Type(step) with { Fallback = Requester });
        RequestId request = await host.CreatedAsync(Request(subject: Subject));

        clock.Advance(Hour);
        int handled = await host.RunAsync(ApprovalJob.Deadlines);
        RequestRecord record = await host.ReadAsync(request);

        Assert.Equal(1, handled);
        Assert.Equal((RequestStatus.Pending, (DateTimeOffset?)(Midnight + Hour)), (record.Status, record.Steps[0].DeadlineRanAt));
        Assert.Equal([A], Assigned(record, 1));
        Assert.DoesNotContain(await host.EventsAsync(request), found => found.Kind == EventKind.Escalated);
        Assert.Equal((request.Value, 1), await host.UnassignableAsync(Key));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task Deadline_DirectoryFailing_LeavesTheStepForTheJobsNextRun()
    {
        ClockFake clock = new(Midnight);
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: clock);
        await host.PublishAsync(Type(Due(Step("review", ApprovalMode.AnyOne, Person(A)), DeadlineAction.Escalate, Permission(Approve))));
        host.Directory.Grant(Approve, C);
        RequestId request = await host.CreatedAsync(Request());

        clock.Advance(Hour);
        host.Directory.FailWith(new InvalidOperationException("The directory is down."));
        int failed = await host.RunAsync(ApprovalJob.Deadlines);
        DateTimeOffset? notRun = (await host.ReadAsync(request)).Steps[0].DeadlineRanAt;
        host.Directory.FailWith(null);
        int next = await host.RunAsync(ApprovalJob.Deadlines);

        Assert.Equal((0, (DateTimeOffset?)null, 1), (failed, notRun, next));
        Assert.Equal([(A, AssignmentOrigin.Approver), (C, AssignmentOrigin.Escalation)], Origins(await host.ReadAsync(request)));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task Deadline_RequestAHostTransactionHolds_IsLeftForALaterRunRatherThanWaitedFor()
    {
        ClockFake clock = new(Midnight);
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: clock);
        await host.PublishAsync(Type(Due(Step("review", ApprovalMode.AnyOne, Person(A)), DeadlineAction.Reject)));
        RequestId request = await host.CreatedAsync(Request());
        clock.Advance(Hour);
        await using NpgsqlConnection database = await host.OpenHostAsync();

        int held;
        await using (NpgsqlTransaction transaction = await RequestHost.LockAsync(database, request))
        {
            held = await host.RunAsync(ApprovalJob.Deadlines);
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }

        int later = await host.RunAsync(ApprovalJob.Deadlines);

        Assert.Equal((0, 1), (held, later));
        Assert.Equal(RequestStatus.Rejected, (await host.ReadAsync(request)).Status);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_OPS_004_AC1_WithHostedOffNoStepsDeadlineRunsUntilRunAsyncIsCalledForDeadlinesWhichThenRunsIt()
    {
        ClockFake clock = new(Midnight);
        await using RequestHost host = await RequestHost.StartAsync(postgres, new Dictionary<string, string?> { ["Themis:Jobs:Hosted"] = "false", ["Themis:Jobs:Interval"] = "00:00:05" }, clock: clock, hosted: true);
        await host.PublishAsync(Type(Due(Step("review", ApprovalMode.AnyOne, Person(A)), DeadlineAction.Reject)));
        RequestId request = await host.CreatedAsync(Request());

        clock.Advance(Hour * 2);
        await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        RequestStatus untouched = (await host.ReadAsync(request)).Status;
        int handled = await host.RunAsync(ApprovalJob.Deadlines);

        Assert.Equal((RequestStatus.Pending, 1), (untouched, handled));
        Assert.Equal(RequestStatus.Rejected, (await host.ReadAsync(request)).Status);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_OPS_004_AC2_TwoProcessesRunningTheDeadlinesJobAtOnceRunEachStepsActionOnce()
    {
        ClockFake clock = new(Midnight);
        Dictionary<string, string?> configuration = new() { ["Themis:Jobs:BatchSize"] = "2" };
        await using RequestHost first = await RequestHost.StartAsync(postgres, configuration, clock: clock);
        await using RequestHost second = await RequestHost.StartOnAsync(first.Connection, configuration, clock: clock);
        await first.PublishAsync(Type(Due(Step("review", ApprovalMode.AnyOne, Person(A)), DeadlineAction.Escalate, Person(C))));
        List<RequestId> requests = [];
        foreach (int number in Enumerable.Range(1, 7))
        {
            requests.Add(await first.CreatedAsync(Request() with { Target = new Target { Kind = "order", Id = $"order-{number}" } }));
        }

        clock.Advance(Hour);
        int[] handled = await Task.WhenAll(first.RunAsync(ApprovalJob.Deadlines), second.RunAsync(ApprovalJob.Deadlines), first.RunAsync(ApprovalJob.Deadlines), second.RunAsync(ApprovalJob.Deadlines));

        Assert.Equal(7, handled.Sum());
        foreach (RequestId request in requests)
        {
            Assert.Single(await first.EventsAsync(request), found => found.Kind == EventKind.Escalated);
            Assert.Equal([(A, AssignmentOrigin.Approver), (C, AssignmentOrigin.Escalation)], Origins(await first.ReadAsync(request)));
        }

        first.AssertNoLogHoldsAValueUsed();
        second.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_LIB_007_AC2_TestMovesTheClockPastAStepsDeadlineAndFindsTheStepEscalatedAfterRunUntilIdleAsync()
    {
        ClockFake clock = new(Midnight);
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: clock);
        TestJobs jobs = new(host.Services.GetRequiredService<IApprovalJobs>());
        await host.PublishAsync(Type(Due(Step("review", ApprovalMode.AnyOne, Person(A)), DeadlineAction.Escalate, Person(C))));
        RequestId request = await host.CreatedAsync(Request());

        clock.Advance(Hour);
        int handled = await jobs.RunUntilIdleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, handled);
        Assert.Equal([(A, AssignmentOrigin.Approver), (C, AssignmentOrigin.Escalation)], Origins(await host.ReadAsync(request)));
        ApprovalEvent escalated = host.Observer.Received[^1];
        Assert.Equal((EventKind.Escalated, (RequestId?)request, (int?)1), (escalated.Kind, escalated.Request, escalated.Step));
        Assert.Equal([C], escalated.Notify);
        host.AssertNoLogHoldsAValueUsed();
    }

    /// <summary><paramref name="step"/> with a one-hour deadline of <paramref name="action"/>, escalating to <paramref name="escalateTo"/> where given.</summary>
    private static StepDefinition Due(StepDefinition step, DeadlineAction action, ApproverReference? escalateTo = null)
    {
        return step with { Deadline = new Deadline { After = Hour, Action = action, EscalateTo = escalateTo } };
    }

    /// <summary>The persons and origins of the first step's assignments, by person in ordinal order.</summary>
    private static (string Person, AssignmentOrigin Origin)[] Origins(RequestRecord record)
    {
        return [.. record.Steps[0].Assignments.Select(assignment => (assignment.Person, assignment.Origin)).OrderBy(assignment => assignment.Person, StringComparer.Ordinal)];
    }
}