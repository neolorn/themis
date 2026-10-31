using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Themis.Core;
using Themis.Testing;

using Xunit;

using static Themis.Tests.Requests;

namespace Themis.Tests;

/// <summary>Creating requests against a real database in a container, each test on a database of its own (<c>01</c> APR-REQ-001 to APR-REQ-003, APR-REQ-007; <c>02</c>).</summary>
[Trait("Category", "integration")]
public sealed class CreateTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly ApprovalTypeDefinition OneStep = Type(Step("review", ApprovalMode.AnyOne, Permission(Approve)));

    [Fact]
    public async Task APR_REQ_001_AC2_EachRefusalRecordsNothingAndNamesTheFieldWhereItHasOne()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(OneStep);
        host.Directory.Grant(Approve, A);
        ApprovalRequest valid = Request();
        Dictionary<string, FieldValue> extra = new(valid.Snapshot) { ["colour"] = new FieldValue.PlainText("red") };
        Dictionary<string, FieldValue> missing = new() { ["amount"] = new FieldValue.DecimalNumber(1m) };
        Dictionary<string, FieldValue> tooLong = new(valid.Snapshot) { ["region"] = new FieldValue.PlainText(new string('r', 1025)) };

        (ApprovalRequest Request, string Code, string? Field)[] refusals =
        [
            (valid with { Type = "purchase.other" }, "approvals.type.unknown", null),
            (valid with { Snapshot = missing }, "approvals.request.snapshotinvalid", "region"),
            (valid with { Snapshot = extra }, "approvals.request.snapshotinvalid", "colour"),
            (valid with { Snapshot = tooLong }, "approvals.request.snapshotinvalid", "region"),
            (valid with { Previous = new RequestId(Guid.CreateVersion7()) }, "approvals.request.previousinvalid", null),
        ];
        foreach ((ApprovalRequest request, string code, string? field) in refusals)
        {
            Result<CreatedRequest> refused = await host.CreateAsync(request);

            Assert.Equal((code, field), (refused.Failure?.Code, refused.Failure?.Details.GetValueOrDefault("field")));
        }

        Assert.Equal("0,0,0,0,0", await host.CountsAsync());
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_001_AC3_SnapshotValueOfTheWrongKindIsRefusedNamingTheField()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(OneStep);
        ApprovalRequest request = Request() with { Snapshot = new Dictionary<string, FieldValue> { ["amount"] = new FieldValue.WholeNumber(500), ["region"] = new FieldValue.PlainText(Region) } };

        Result<CreatedRequest> refused = await host.CreateAsync(request);

        Assert.Equal(("approvals.request.snapshotinvalid", "amount"), (refused.Failure?.Code, refused.Failure?.Details["field"]));
        Assert.Equal("0,0,0,0,0", await host.CountsAsync());
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_002_AC2_AfterTheFirstRequestIsRejectedANewOneForTheTargetIsRecorded()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(OneStep);
        host.Directory.Grant(Approve, A);
        RequestId first = await host.CreatedAsync(Request());

        Result<CreatedRequest> whileOpen = await host.CreateAsync(Request());
        await host.DecideAsync(first, A, DecisionOutcome.Reject, Reason);
        Result<CreatedRequest> after = await host.CreateAsync(Request());

        Assert.Equal("approvals.request.open", whileOpen.Failure?.Code);
        Assert.Equal(RequestStatus.Pending, after.Value?.Status);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_003_AC1_StepWhosePermissionTheDirectoryAnswersForThreePersonsOneTheRequesterHasTwoAssignments()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(OneStep);
        host.Directory.Grant(Approve, A);
        host.Directory.Grant(Approve, Requester);
        host.Directory.Grant(Approve, B);

        RequestRecord record = await host.ReadAsync(await host.CreatedAsync(Request()));

        Assert.Equal([A, B], Assigned(record, 1));
        Assert.All(record.Steps[0].Assignments, assignment => Assert.Equal(AssignmentOrigin.Approver, assignment.Origin));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_003_AC2_PersonGivenThePermissionAfterTheStepStartedIsNotAssignedToIt()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(OneStep);
        host.Directory.Grant(Approve, A);
        RequestId request = await host.CreatedAsync(Request());

        host.Directory.Grant(Approve, Late);
        Result<DecisionReceipt> late = await host.DecideAsync(request, Late);

        Assert.Equal([A], Assigned(await host.ReadAsync(request), 1));
        Assert.Equal("approvals.decision.notassigned", late.Failure?.Code);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_003_AC3_StepWhoseConditionIsFalseIsRecordedSkippedAndTheNextOneStarts()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        StepDefinition large = Step("large", ApprovalMode.AnyOne, Person(A)) with { Condition = new Condition.Compare("amount", CompareOperator.Greater, new FieldValue.DecimalNumber(1000m)) };
        await host.PublishAsync(Type(large, Step("lead", ApprovalMode.AnyOne, Person(B))));

        RequestId request = await host.CreatedAsync(Request(amount: 500m));
        RequestRecord record = await host.ReadAsync(request);

        Assert.Equal([StepStatus.Skipped, StepStatus.Current], record.Steps.Select(step => step.Status));
        Assert.Equal([B], Assigned(record, 2));
        RecordedEvent requested = Assert.Single(await host.EventsAsync(request));
        Assert.Equal((EventKind.Requested, 2), (requested.Kind, requested.Step));
        Assert.Equal([B, Watcher], requested.Notify);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_007_AC1_StepWhosePermissionResolvesToTheRequesterAloneIsAssignedToTheStepsFallbackPerson()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(OneStep);
        host.Directory.Grant(Approve, Requester);

        RequestRecord record = await host.ReadAsync(await host.CreatedAsync(Request()));

        AssignmentRecord fallback = Assert.Single(record.Steps[0].Assignments);
        Assert.Equal((StepFallback, AssignmentOrigin.Fallback), (fallback.Person, fallback.Origin));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task Create_StepsFallbackExcluded_AssignsTheTypesFallback()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(Type(Step("review", ApprovalMode.AnyOne, Person(Requester)) with { Fallback = Subject }));

        RequestRecord record = await host.ReadAsync(await host.CreatedAsync(Request(subject: Subject)));

        Assert.Equal([TypeFallback], Assigned(record, 1));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task Create_ApproversOver200Persons_IsRefusedWithTooManyAndRecordsNothing()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(Type(Step("review", ApprovalMode.AnyOne, Permission(Approve), Person(A))));
        foreach (int person in Enumerable.Range(0, 200))
        {
            host.Directory.Grant(Approve, $"person-many-{person}");
        }

        Result<CreatedRequest> refused = await host.CreateAsync(Request());

        Assert.Equal("approvals.directory.toomany", refused.Failure?.Code);
        Assert.Equal("0,0,0,0,0", await host.CountsAsync());
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_LIB_005_AC3_DirectoryThatAnswersAnEmptyPersonReferenceMakesTheCreateThrowAndNothingIsRecorded()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(OneStep);
        host.Directory.Grant(Approve, string.Empty);

        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => host.CreateAsync(Request()));

        Assert.Contains("IApproverDirectory", thrown.Message, StringComparison.Ordinal);
        Assert.Equal("0,0,0,0,0", await host.CountsAsync());
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_TYPE_001_AC2_RequestCreatedUnderVersion1DecidesUnderItsStepsAfterVersion2AndOneCreatedAfterUsesVersion2()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(Type(Step("review", ApprovalMode.AnyOne, Person(A))));
        RequestId first = await host.CreatedAsync(Request());
        await host.PublishAsync(Type(Step("review", ApprovalMode.AnyOne, Person(B)), Step("finance", ApprovalMode.AnyOne, Person(C))) with { Key = Key });

        Result<DecisionReceipt> decided = await host.DecideAsync(first, A);
        RequestId second = await host.CreatedAsync(Request() with { Target = new Target { Kind = "order", Id = "order-other" } });
        RequestRecord later = await host.ReadAsync(second);

        Assert.Equal(RequestStatus.Approved, decided.Value?.Status);
        Assert.Equal(1, (await host.ReadAsync(first)).Version);
        Assert.Equal((2, 2), (later.Version, later.Steps.Count));
        Assert.Equal([B], Assigned(later, 1));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_TYPE_001_AC4_RetiredTypeRefusesANewRequestAndLetsItsOpenRequestsEnd()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(Type(Step("review", ApprovalMode.AnyOne, Person(A))));
        RequestId open = await host.CreatedAsync(Request());

        await host.RetireAsync(Key);
        Result<CreatedRequest> refused = await host.CreateAsync(Request() with { Target = new Target { Kind = "order", Id = "order-other" } });
        Result<DecisionReceipt> decided = await host.DecideAsync(open, A);

        Assert.Equal("approvals.type.retired", refused.Failure?.Code);
        Assert.Equal(RequestStatus.Approved, decided.Value?.Status);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_TYPE_003_AC1_WithTwoPoliciesWhoseStartConditionsBothHoldTheRequestTakesTheFirst()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(Typed(
        [
            new PolicyDefinition { Name = "first", StartCondition = new Condition.RequesterHas(Buyer), Steps = [Step("review", ApprovalMode.AnyOne, Person(A))] },
            new PolicyDefinition { Name = "second", Steps = [Step("review", ApprovalMode.AnyOne, Person(B))] },
        ]));

        RequestRecord record = await host.ReadAsync(await host.CreatedAsync(Request()));

        Assert.Equal(("first", A), (record.Policy, Assert.Single(Assigned(record, 1))));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_TYPE_003_AC2_PolicyWhoseStartConditionHoldsAndWhoseEveryStepsConditionIsFalseIsPassedOver()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        Condition never = new Condition.Compare("amount", CompareOperator.Greater, new FieldValue.DecimalNumber(1000000m));
        await host.PublishAsync(Typed(
        [
            new PolicyDefinition { Name = "first", Steps = [Step("review", ApprovalMode.AnyOne, Person(A)) with { Condition = never }] },
            new PolicyDefinition { Name = "second", Steps = [Step("review", ApprovalMode.AnyOne, Person(B))] },
        ]));

        RequestRecord record = await host.ReadAsync(await host.CreatedAsync(Request()));

        Assert.Equal(("second", B), (record.Policy, Assert.Single(Assigned(record, 1))));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_TYPE_003_AC3_WithNoPolicyHoldingATypeThatDoesNotAllowItSendsTheRequestToItsFallbackPersonAndOneThatAllowsItApprovesAtOnce()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        PolicyDefinition never = new() { Name = "never", StartCondition = new Condition.SubjectHas(Buyer), Steps = [Step("review", ApprovalMode.AnyOne, Person(A))] };
        await host.PublishAsync(Typed([never]));
        RequestId toFallback = await host.CreatedAsync(Request());
        await host.PublishAsync(Typed([never]) with { Key = "purchase.auto", AllowAutoApprove = true });

        Result<CreatedRequest> atOnce = await host.CreateAsync(Request() with { Type = "purchase.auto" });
        RequestRecord fallback = await host.ReadAsync(toFallback);

        Assert.Equal((null, "fallback", TypeFallback), (fallback.Policy, fallback.Steps.Single().Name, Assert.Single(Assigned(fallback, 1))));
        Assert.Equal(RequestStatus.Approved, atOnce.Value?.Status);
        Assert.Equal([EventKind.Requested, EventKind.Approved], (await host.EventsAsync(atOnce.Value!.Request)).Select(found => found.Kind));
        Assert.Empty((await host.ReadAsync(atOnce.Value.Request)).Steps);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task Create_RequestedEvent_NamesTheFirstStepsAssigneesAndTheWatchers()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(OneStep);
        host.Directory.Grant(Approve, A);
        host.Directory.Grant(Approve, B);

        RequestId request = await host.CreatedAsync(Request());

        RecordedEvent requested = Assert.Single(await host.EventsAsync(request));
        Assert.Equal((EventKind.Requested, 1L, 1), (requested.Kind, requested.Sequence, requested.Step));
        Assert.Equal([A, B, Watcher], requested.Notify);
        host.AssertNoLogHoldsAValueUsed();
    }
}