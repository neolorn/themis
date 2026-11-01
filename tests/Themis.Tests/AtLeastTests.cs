using System;
using System.Linq;
using System.Threading.Tasks;

using Themis.Core;

using Xunit;

using static Themis.Tests.Requests;

namespace Themis.Tests;

/// <summary>Steps of the mode <c>AtLeast</c> against a real database in a container, each test on a database of its own (<c>02</c> APR-TYPE-004; T-025).</summary>
[Trait("Category", "integration")]
public sealed class AtLeastTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Midnight = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task APR_TYPE_004_AC3_StepNeedingTwoOfFiveCompletesAtTheSecondApprovalByTwoPersons()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(Type(TwoOf(Permission(Approve))));
        foreach (string person in new[] { A, B, C, Late, Subject })
        {
            host.Directory.Grant(Approve, person);
        }

        RequestId request = await host.CreatedAsync(Request());

        Result<DecisionReceipt> first = await host.DecideAsync(request, A);
        Result<DecisionReceipt> second = await host.DecideAsync(request, B);
        RequestRecord record = await host.ReadAsync(request);

        Assert.Equal([RequestStatus.Pending, RequestStatus.Approved], new[] { first, second }.Select(result => result.Value?.Status));
        Assert.Equal((ApprovalMode.AtLeast, (int?)2, StepStatus.Done), (record.Steps[0].Mode, record.Steps[0].Required, record.Steps[0].Status));
        Assert.Equal(5, record.Steps[0].Assignments.Count);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_TYPE_004_AC3_OnePersonApprovingTwiceThroughAnAssignmentSkipSatisfiedAndADelegationCountsOnce()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: new ClockFake(Midnight));
        await host.PublishAsync(Type(Step("review", ApprovalMode.AnyOne, Person(B)), TwoOf(Person(A), Person(B), Person(C), Person(Late))));
        await host.DelegatedAsync(new DelegationDefinition { From = C, To = B, Starts = Midnight, Ends = Midnight + TimeSpan.FromDays(10), CreatedBy = C });
        RequestId request = await host.CreatedAsync(Request());

        Result<DecisionReceipt> reviewed = await host.DecideAsync(request, B);
        Result<DecisionReceipt> forC = await host.DecideAsync(request, B, actingFor: C);
        Result<DecisionReceipt> byA = await host.DecideAsync(request, A);
        RequestRecord record = await host.ReadAsync(request);

        Assert.Equal([RequestStatus.Pending, RequestStatus.Pending, RequestStatus.Approved], new[] { reviewed, forC, byA }.Select(result => result.Value?.Status));
        Assert.Equal(reviewed.Value?.Decision, record.Steps[1].Assignments.Single(assignment => assignment.Person == B).Decision);
        Assert.Equal(StepStatus.Done, record.Steps[1].Status);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_TYPE_004_AC4_RejectionInAnAtLeastStepRejectsTheRequest()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(Type(TwoOf(Person(A), Person(B), Person(C)), Step("finance", ApprovalMode.AnyOne, Person(Late))));
        RequestId request = await host.CreatedAsync(Request());

        Result<DecisionReceipt> approved = await host.DecideAsync(request, A);
        Result<DecisionReceipt> rejected = await host.DecideAsync(request, B, DecisionOutcome.Reject, Reason);
        RequestRecord record = await host.ReadAsync(request);

        Assert.Equal((RequestStatus.Pending, RequestStatus.Rejected), (approved.Value?.Status, rejected.Value?.Status));
        Assert.Equal((RequestStatus.Rejected, B), (record.Status, record.ClosedBy));
        Assert.Equal([StepStatus.Closed, StepStatus.Waiting], record.Steps.Select(step => step.Status));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_TYPE_004_AC5_AtLeastStepStartingWithFewerAssignmentsThanItsRequiredGoesToItsFallbackWhoseApprovalCompletesIt()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(Type(Step("review", ApprovalMode.AtLeast, Permission(Approve)) with { Required = 3 }));
        host.Directory.Grant(Approve, A);
        host.Directory.Grant(Approve, B);
        host.Directory.Grant(Approve, Requester);
        RequestId request = await host.CreatedAsync(Request());

        AssignmentRecord assigned = Assert.Single((await host.ReadAsync(request)).Steps[0].Assignments);
        Result<DecisionReceipt> refused = await host.DecideAsync(request, A);
        Result<DecisionReceipt> approved = await host.DecideAsync(request, StepFallback);

        Assert.Equal((StepFallback, AssignmentOrigin.Fallback), (assigned.Person, assigned.Origin));
        Assert.Equal("approvals.decision.notassigned", refused.Failure?.Code);
        Assert.Equal(RequestStatus.Approved, approved.Value?.Status);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_TYPE_004_AC5_AtLeastStepStartingWithFewerAssignmentsThanItsRequiredAndNoFallbackLeftRaisesRequestUnassignable()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(Type(TwoOf(Person(Requester), Person(A)) with { Fallback = Requester }) with { Fallback = Requester });

        Result<CreatedRequest> created = await host.CreateAsync(Request());
        RequestRecord record = await host.ReadAsync(created.Value!.Request);

        Assert.Equal(RequestStatus.Cancelled, created.Value.Status);
        Assert.Equal((RequestStatus.Cancelled, (ReasonCode?)ReasonCode.NoApprover), (record.Status, record.ReasonCode));
        Assert.Equal((created.Value.Request.Value, 1), await host.UnassignableAsync(Key));
        await host.RunAsync(ApprovalJob.Watch);
        AlertCall raised = Assert.Single(host.Alerts.Calls);
        Assert.Equal((true, "request-unassignable", Key), (raised.Raised, raised.Condition, raised.Scope));
        host.AssertNoLogHoldsAValueUsed();
    }

    /// <summary>A step <c>approval</c> of <paramref name="approvers"/> that two of them must approve.</summary>
    private static StepDefinition TwoOf(params ApproverReference[] approvers)
    {
        return Step("approval", ApprovalMode.AtLeast, approvers) with { Required = 2 };
    }
}