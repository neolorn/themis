using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Npgsql;

using Themis.Core;
using Themis.Testing;

using Xunit;

using static Themis.Tests.Requests;

namespace Themis.Tests;

/// <summary>Deciding requests against a real database in a container, each test on a database of its own (<c>01</c> APR-REQ-004 to APR-REQ-006; <c>04</c> APR-OPS-003).</summary>
[Trait("Category", "integration")]
public sealed class DecideTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task APR_REQ_004_AC1_DecisionByAPersonWithNoAssignmentOfTheCurrentStepIsRefusedNotAssigned()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(Type(Step("review", ApprovalMode.AnyOne, Person(A))));
        RequestId request = await host.CreatedAsync(Request());

        Result<DecisionReceipt> refused = await host.DecideAsync(request, B);

        Assert.Equal("approvals.decision.notassigned", refused.Failure?.Code);
        Assert.Empty((await host.ReadAsync(request)).Decisions);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_004_AC3_PersonTheDirectoryNoLongerAnswersAsAbleToApproveIsRefusedNotPermittedAndNothingIsRecorded()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(Type(Step("review", ApprovalMode.AnyOne, Permission(Approve))));
        host.Directory.Grant(Approve, A);
        RequestId request = await host.CreatedAsync(Request());
        string before = await host.CountsAsync();

        host.Directory.Revoke(Approve, A);
        Result<DecisionReceipt> refused = await host.DecideAsync(request, A);

        Assert.Equal("approvals.decision.notpermitted", refused.Failure?.Code);
        Assert.Equal(before, await host.CountsAsync());
        Assert.Null(Assert.Single((await host.ReadAsync(request)).Steps[0].Assignments).Decision);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_004_AC4_TwoApprovalsOfAnAnyOneStepAtOnceRecordOneDecisionAndTheOtherIsRefusedNotAssigned()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(Type(Step("review", ApprovalMode.AnyOne, Person(A), Person(B)), Step("finance", ApprovalMode.AnyOne, Person(C))));
        RequestId request = await host.CreatedAsync(Request());

        Result<DecisionReceipt>[] decided = await AtOnceAsync(host, request, () => host.DecideAsync(request, A), () => host.DecideAsync(request, B));

        Assert.Single(decided, result => result.Value is not null);
        Assert.Equal("approvals.decision.notassigned", Assert.Single(decided, result => result.Failure is not null).Failure?.Code);
        RequestRecord record = await host.ReadAsync(request);
        Assert.Single(record.Decisions);
        Assert.Equal([StepStatus.Done, StepStatus.Current], record.Steps.Select(step => step.Status));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_004_AC5_DecisionOnARejectedRequestIsRefusedClosed()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(Type(Step("review", ApprovalMode.All, Person(A), Person(B))));
        RequestId request = await host.CreatedAsync(Request());
        await host.DecideAsync(request, A, DecisionOutcome.Reject, Reason);

        Result<DecisionReceipt> refused = await host.DecideAsync(request, B);

        Assert.Equal("approvals.request.closed", refused.Failure?.Code);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_004_AC6_AllStepOfThreeAssignmentsCompletesAtTheThirdApprovalAndNotBefore()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(Type(Step("review", ApprovalMode.All, Person(A), Person(B), Person(C))));
        RequestId request = await host.CreatedAsync(Request());

        Result<DecisionReceipt> first = await host.DecideAsync(request, A);
        Result<DecisionReceipt> second = await host.DecideAsync(request, B);
        Result<DecisionReceipt> third = await host.DecideAsync(request, C);

        Assert.Equal([RequestStatus.Pending, RequestStatus.Pending, RequestStatus.Approved], new[] { first, second, third }.Select(result => result.Value?.Status));
        Assert.Equal(StepStatus.Done, (await host.ReadAsync(request)).Steps[0].Status);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task Decide_Requester_IsRefusedSeparation()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(Type(Step("review", ApprovalMode.AnyOne, Person(A))));
        RequestId request = await host.CreatedAsync(Request());
        await using NpgsqlConnection database = new(host.Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using (NpgsqlCommand assign = new("INSERT INTO approvals.assignments (request, step, person, origin, via) VALUES (@request, 1, @person, 'approver', '{}')", database))
        {
            assign.Parameters.AddWithValue("request", request.Value);
            assign.Parameters.AddWithValue("person", Requester);
            await assign.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        Result<DecisionReceipt> refused = await host.DecideAsync(request, Requester);

        Assert.Equal("approvals.decision.separation", refused.Failure?.Code);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task Decide_ActingForAnotherWithoutADelegation_IsRefusedNotAssigned()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(Type(Step("review", ApprovalMode.AnyOne, Person(A))));
        RequestId request = await host.CreatedAsync(Request());

        Result<DecisionReceipt> refused = await host.DecideAsync(request, B, actingFor: A);

        Assert.Equal("approvals.decision.notassigned", refused.Failure?.Code);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task Decide_ReasonOver2000Characters_IsRefusedInvalidBeforeTheRequestIsRead()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);

        Result<DecisionReceipt> refused = await host.DecideAsync(new RequestId(Guid.CreateVersion7()), A, DecisionOutcome.Reject, new string('r', 2001));
        Result<DecisionReceipt> unknown = await host.DecideAsync(new RequestId(Guid.CreateVersion7()), A, DecisionOutcome.Reject, Reason);

        Assert.Equal(("approvals.reason.invalid", "approvals.request.unknown"), (refused.Failure?.Code, unknown.Failure?.Code));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_005_AC1_RejectionWithoutAReasonIsRefusedAndRecordsNothing()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(Type(Step("review", ApprovalMode.AnyOne, Person(A))));
        RequestId request = await host.CreatedAsync(Request());
        string before = await host.CountsAsync();

        Result<DecisionReceipt> none = await host.DecideAsync(request, A, DecisionOutcome.Reject);
        Result<DecisionReceipt> empty = await host.DecideAsync(request, A, DecisionOutcome.Reject, string.Empty);

        Assert.Equal(("approvals.decision.reasonrequired", "approvals.decision.reasonrequired"), (none.Failure?.Code, empty.Failure?.Code));
        Assert.Equal(before, await host.CountsAsync());
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_005_AC2_RejectionInTheFirstOfThreeStepsRejectsTheRequestAndNoLaterStepStarts()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(Type(Step("review", ApprovalMode.AnyOne, Person(A)), Step("finance", ApprovalMode.AnyOne, Person(B)), Step("lead", ApprovalMode.AnyOne, Person(C))));
        RequestId request = await host.CreatedAsync(Request());

        Result<DecisionReceipt> rejected = await host.DecideAsync(request, A, DecisionOutcome.Reject, Reason);
        RequestRecord record = await host.ReadAsync(request);

        Assert.Equal(RequestStatus.Rejected, rejected.Value?.Status);
        Assert.Equal((RequestStatus.Rejected, A, Reason), (record.Status, record.ClosedBy, record.Reason));
        Assert.Equal([StepStatus.Closed, StepStatus.Waiting, StepStatus.Waiting], record.Steps.Select(step => step.Status));
        Assert.All(record.Steps.Skip(1), step => Assert.Empty(step.Assignments));
        RecordedEvent last = (await host.EventsAsync(request))[^1];
        Assert.Equal((EventKind.Rejected, A), (last.Kind, last.Actor));
        Assert.Equal([Requester, Watcher], last.Notify);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_005_AC3_ResubmissionNamesTheRejectedRequestAndReadingItAnswersThatLink()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(Type(Step("review", ApprovalMode.AnyOne, Person(A))));
        RequestId rejected = await host.CreatedAsync(Request());
        Result<CreatedRequest> whileOpen = await host.CreateAsync(Request() with { Target = new Target { Kind = "order", Id = "order-other" }, Previous = rejected });
        await host.DecideAsync(rejected, A, DecisionOutcome.Reject, Reason);

        RequestId resubmitted = await host.CreatedAsync(Request(amount: 400m) with { Previous = rejected });

        Assert.Equal("approvals.request.previousinvalid", whileOpen.Failure?.Code);
        Assert.Equal(rejected, (await host.ReadAsync(resubmitted)).Previous);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_006_AC1_UnderSkipAPersonWhoApprovedStep1AndIsAssignedToAnAnyOneStep2LeavesItCompleteWithoutASecondDecision()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(Typed([new PolicyDefinition { Name = "main", Steps = [Step("review", ApprovalMode.AnyOne, Person(A)), Step("finance", ApprovalMode.AnyOne, Person(A), Person(B))] }], DuplicateRule.Skip));
        RequestId request = await host.CreatedAsync(Request());

        Result<DecisionReceipt> decided = await host.DecideAsync(request, A);
        RequestRecord record = await host.ReadAsync(request);

        Assert.Equal(RequestStatus.Approved, decided.Value?.Status);
        Assert.Equal(decided.Value?.Decision, Assert.Single(record.Decisions).Decision);
        Assert.Equal(decided.Value?.Decision, record.Steps[1].Assignments.Single(assignment => assignment.Person == A).Decision);
        Assert.Equal(StepStatus.Done, record.Steps[1].Status);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_006_AC2_UnderRequireOtherTheSamePersonIsNotAssignedToStep2AndAnotherAssigneeDecidesIt()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(Typed([new PolicyDefinition { Name = "main", Steps = [Step("review", ApprovalMode.AnyOne, Person(A)), Step("finance", ApprovalMode.AnyOne, Person(A), Person(B))] }], DuplicateRule.RequireOther));
        RequestId request = await host.CreatedAsync(Request());

        Result<DecisionReceipt> first = await host.DecideAsync(request, A);
        string[] assigned = Assigned(await host.ReadAsync(request), 2);
        Result<DecisionReceipt> second = await host.DecideAsync(request, B);

        Assert.Equal(RequestStatus.Pending, first.Value?.Status);
        Assert.Equal([B], assigned);
        Assert.Equal(RequestStatus.Approved, second.Value?.Status);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_007_AC2_RequestWhoseRequesterIsBothTheStepsAndTheTypesFallbackEndsCancelledWithNoApprover()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(Type(Step("review", ApprovalMode.AnyOne, Person(Requester)) with { Fallback = Requester }) with { Fallback = Requester });

        Result<CreatedRequest> created = await host.CreateAsync(Request());
        RequestRecord record = await host.ReadAsync(created.Value!.Request);

        Assert.Equal(RequestStatus.Cancelled, created.Value.Status);
        Assert.Equal((RequestStatus.Cancelled, (string?)null, (ReasonCode?)ReasonCode.NoApprover), (record.Status, record.ClosedBy, record.ReasonCode));
        Assert.Equal([EventKind.Requested, EventKind.Cancelled], (await host.EventsAsync(created.Value.Request)).Select(found => found.Kind));
        Assert.Equal((created.Value.Request.Value, 1), await host.UnassignableAsync(Key));
        await host.RunAsync(ApprovalJob.Watch);
        AlertCall raised = Assert.Single(host.Alerts.Calls);
        Assert.Equal((true, "request-unassignable", Key), (raised.Raised, raised.Condition, raised.Scope));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_014_AC3_ApprovalCompletingTheLastStepWritesApprovedNamingTheRequesterAndTheWatchers()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(Type(Step("review", ApprovalMode.AnyOne, Person(A)), Step("finance", ApprovalMode.AnyOne, Person(B))));
        RequestId request = await host.CreatedAsync(Request());

        await host.DecideAsync(request, A);
        await host.DecideAsync(request, B);
        IReadOnlyList<RecordedEvent> events = await host.EventsAsync(request);

        Assert.Equal([(EventKind.Requested, 1L), (EventKind.StepStarted, 2L), (EventKind.Approved, 3L)], events.Select(found => (found.Kind, found.Sequence)));
        Assert.Equal([B], events[1].Notify);
        Assert.Equal([Requester, Watcher], events[2].Notify);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_TYPE_006_AC1_WatcherIsNamedOnTheRequestedAndApprovedEventsAndTheirDecisionIsRefusedUnlessAssigned()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(Type(Step("review", ApprovalMode.AnyOne, Person(A))));
        RequestId request = await host.CreatedAsync(Request());

        Result<DecisionReceipt> watcher = await host.DecideAsync(request, Watcher);
        await host.DecideAsync(request, A);
        IReadOnlyList<RecordedEvent> events = await host.EventsAsync(request);

        Assert.Equal("approvals.decision.notassigned", watcher.Failure?.Code);
        Assert.Equal([EventKind.Requested, EventKind.Approved], events.Select(found => found.Kind));
        Assert.All(events, found => Assert.Contains(Watcher, found.Notify));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_OPS_003_AC2_TwoDecisionsOnOneRequestAtOnceNeverDeadlockAndTheSecondIsJudgedOnWhatTheFirstLeft()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(Type(Step("review", ApprovalMode.All, Person(A), Person(B))));
        RequestId request = await host.CreatedAsync(Request());

        Result<DecisionReceipt>[] decided = await AtOnceAsync(host, request, () => host.DecideAsync(request, A), () => host.DecideAsync(request, B));

        Assert.Equal([RequestStatus.Pending, RequestStatus.Approved], decided.Select(result => result.Value?.Status).Order());
        Assert.Equal(2, (await host.ReadAsync(request)).Decisions.Count);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_LIB_005_AC2_DirectoryThatDoesNotAnswerWithinTheTimeoutLeavesADecisionRefusedUnavailableAndTheRequestAsItWas()
    {
        SlowDirectory? slow = null;
        await using RequestHost host = await RequestHost.StartAsync(
            postgres,
            new Dictionary<string, string?> { ["Themis:Timeout"] = "00:00:01" },
            directory => slow = new SlowDirectory(directory));
        await host.PublishAsync(Type(Step("review", ApprovalMode.AnyOne, Person(A))));
        RequestId request = await host.CreatedAsync(Request());
        string before = await host.CountsAsync();

        slow!.Slow = true;
        Result<DecisionReceipt> refused = await host.DecideAsync(request, A);
        slow.Slow = false;

        Assert.Equal("approvals.directory.unavailable", refused.Failure?.Code);
        Assert.Equal(before, await host.CountsAsync());
        Assert.Equal(RequestStatus.Pending, (await host.ReadAsync(request)).Status);
        Assert.Equal(RequestStatus.Approved, (await host.DecideAsync(request, A)).Value?.Status);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task Decide_DirectoryThatThrows_IsRefusedUnavailable()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(Type(Step("review", ApprovalMode.AnyOne, Person(A))));
        RequestId request = await host.CreatedAsync(Request());

        host.Directory.FailWith(new InvalidOperationException("The directory is down."));
        Result<DecisionReceipt> refused = await host.DecideAsync(request, A);

        Assert.Equal("approvals.directory.unavailable", refused.Failure?.Code);
        host.AssertNoLogHoldsAValueUsed();
    }

    /// <summary>Runs <paramref name="first"/> and <paramref name="second"/> while the request's row is held locked, so both wait for it, then frees it.</summary>
    private static async Task<Result<DecisionReceipt>[]> AtOnceAsync(RequestHost host, RequestId request, Func<Task<Result<DecisionReceipt>>> first, Func<Task<Result<DecisionReceipt>>> second)
    {
        await using NpgsqlConnection holder = new(host.Connection);
        await holder.OpenAsync(TestContext.Current.CancellationToken);
        Task<Result<DecisionReceipt>>[] decisions;
        await using (NpgsqlTransaction locked = await RequestHost.LockAsync(holder, request))
        {
            decisions = [Task.Run(first, TestContext.Current.CancellationToken), Task.Run(second, TestContext.Current.CancellationToken)];
            await host.WaitForLockWaitersAsync(2);
            await locked.RollbackAsync(TestContext.Current.CancellationToken);
        }

        return await Task.WhenAll(decisions);
    }

    /// <summary>A directory that, while <see cref="Slow"/> is set, answers a check only after ten seconds.</summary>
    private sealed class SlowDirectory(IApproverDirectory directory) : IApproverDirectory
    {
        internal bool Slow { get; set; }

        public ValueTask<IReadOnlyList<string>> ResolveAsync(string permission, ApproverQuery query, CancellationToken cancellationToken)
        {
            return directory.ResolveAsync(permission, query, cancellationToken);
        }

        public async ValueTask<bool> CanApproveAsync(ApproverCheck check, CancellationToken cancellationToken)
        {
            if (Slow)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }

            return await directory.CanApproveAsync(check, cancellationToken);
        }
    }
}