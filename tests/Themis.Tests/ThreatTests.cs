using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Themis.Core;

using Xunit;

using static Themis.Tests.Requests;

namespace Themis.Tests;

/// <summary>The paths by which a request becomes approved, and the moves that never approve one, against a real database in a container (<c>07</c> APR-THR-002).</summary>
[Trait("Category", "integration")]
public sealed class ThreatTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Midnight = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>A condition no request of <see cref="Request"/>'s amount meets.</summary>
    private static readonly Condition Large = new Condition.Compare("amount", CompareOperator.Greater, new FieldValue.DecimalNumber(1000m));

    [Fact]
    public async Task APR_THR_002_AC1_OnlyTheStepsApprovalsOrTheTypesApprovalAtOnceApproveARequestAndNoDeadlineCancelVoidOrRecordedChangeDoes()
    {
        ClockFake clock = new(Midnight);
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: clock);
        await host.PublishAsync(Keyed("purchase.last", Step("review", ApprovalMode.AnyOne, Person(A)), Step("finance", ApprovalMode.AnyOne, Person(B))));
        await host.PublishAsync(Keyed("purchase.escalated", Due(Step("review", ApprovalMode.All, Person(A), Person(B)), DeadlineAction.Escalate, Person(C))));
        await host.PublishAsync(Keyed("purchase.skipped", Step("review", ApprovalMode.AnyOne, Person(A)), Step("finance", ApprovalMode.AnyOne, Person(B)) with { Condition = Large }));
        await host.PublishAsync(Keyed("purchase.allskipped", Step("review", ApprovalMode.AnyOne, Person(A)) with { Condition = Large }));
        await host.PublishAsync(Keyed("purchase.atonce", Step("review", ApprovalMode.AnyOne, Person(A))) with { Policies = [new PolicyDefinition { Name = "large", StartCondition = Large, Steps = [Step("review", ApprovalMode.AnyOne, Person(A))] }], AllowAutoApprove = true });
        await host.PublishAsync(Keyed("purchase.rejected", Due(Step("review", ApprovalMode.AnyOne, Person(A)), DeadlineAction.Reject)));
        await host.PublishAsync(Keyed("purchase.cancelled", Due(Step("review", ApprovalMode.AnyOne, Person(A)), DeadlineAction.Cancel)));
        await host.PublishAsync(Keyed("purchase.escalating", Due(Step("review", ApprovalMode.AnyOne, Person(A)), DeadlineAction.Escalate, Person(C))));

        RequestId last = await host.CreatedAsync(Of("purchase.last"));
        RequestId escalated = await host.CreatedAsync(Of("purchase.escalated"));
        RequestId skipped = await host.CreatedAsync(Of("purchase.skipped"));
        RequestId allSkipped = await host.CreatedAsync(Of("purchase.allskipped"));
        Result<CreatedRequest> atOnce = await host.CreateAsync(Of("purchase.atonce"));
        RequestId rejected = await host.CreatedAsync(Of("purchase.rejected"));
        RequestId cancelled = await host.CreatedAsync(Of("purchase.cancelled"));
        RequestId escalating = await host.CreatedAsync(Of("purchase.escalating"));
        RequestId withdrawn = await host.CreatedAsync(Of("purchase.last") with { Target = new Target { Kind = "order", Id = "order-withdrawn" } });

        Result<DecisionReceipt> firstOfTwo = await host.DecideAsync(last, A);
        Result<DecisionReceipt> lastOfTwo = await host.DecideAsync(last, B);
        Result<DecisionReceipt> onlyNeeded = await host.DecideAsync(skipped, A);
        clock.Advance(TimeSpan.FromHours(1));
        await host.RunAsync(ApprovalJob.Deadlines);
        Result<DecisionReceipt> byEscalation = await host.DecideAsync(escalated, C);
        Result cancel = await host.CancelAsync(withdrawn, Requester);
        Result<IReadOnlyList<RequestId>> lockedChange = await host.RecordChangeAsync(On("purchase.escalating"), "amount", C);
        Result<IReadOnlyList<RequestId>> voidingChange = await host.RecordChangeAsync(On("purchase.last"), "amount", C);

        Assert.Equal(
            (RequestStatus.Pending, RequestStatus.Approved, RequestStatus.Approved, RequestStatus.Approved, RequestStatus.Approved),
            (firstOfTwo.Value?.Status, lastOfTwo.Value?.Status, onlyNeeded.Value?.Status, byEscalation.Value?.Status, atOnce.Value?.Status));
        Assert.True(cancel.IsSuccess);
        Assert.Equal("approvals.field.locked", lockedChange.Failure?.Code);
        Assert.Equal([last], voidingChange.Value!);
        Assert.Equal(
            (RequestStatus.Void, RequestStatus.Pending, RequestStatus.Rejected, RequestStatus.Cancelled, RequestStatus.Pending, RequestStatus.Cancelled),
            ((await host.ReadAsync(last)).Status, (await host.ReadAsync(allSkipped)).Status, (await host.ReadAsync(rejected)).Status, (await host.ReadAsync(cancelled)).Status, (await host.ReadAsync(escalating)).Status, (await host.ReadAsync(withdrawn)).Status));
        Assert.Equal([TypeFallback], Assigned(await host.ReadAsync(allSkipped), 1));
        RequestId[] everyRequest = [last, escalated, skipped, allSkipped, atOnce.Value!.Request, rejected, cancelled, escalating, withdrawn];
        List<RequestId> approved = [];
        foreach (RequestId request in everyRequest)
        {
            int approvals = (await host.EventsAsync(request)).Count(found => found.Kind == EventKind.Approved);
            approved.AddRange(Enumerable.Repeat(request, approvals));
        }

        Assert.Equal(new[] { last, escalated, skipped, atOnce.Value.Request }.OrderBy(request => request.Value), approved.OrderBy(request => request.Value));
        host.AssertNoLogHoldsAValueUsed();
    }

    /// <summary>The type <paramref name="key"/> of one policy <c>main</c> of <paramref name="steps"/>, as <see cref="Requests.Type"/> makes it.</summary>
    private static ApprovalTypeDefinition Keyed(string key, params StepDefinition[] steps)
    {
        return Type(steps) with { Key = key };
    }

    /// <summary>A request of the type <paramref name="key"/>, on a target of its own.</summary>
    private static ApprovalRequest Of(string key)
    {
        return Request() with { Type = key, Target = On(key) };
    }

    private static Target On(string key)
    {
        return new Target { Kind = "order", Id = "order-" + key[(key.IndexOf('.', StringComparison.Ordinal) + 1)..] };
    }

    /// <summary><paramref name="step"/> with a one-hour deadline of <paramref name="action"/>, escalating to <paramref name="escalateTo"/> where given.</summary>
    private static StepDefinition Due(StepDefinition step, DeadlineAction action, ApproverReference? escalateTo = null)
    {
        return step with { Deadline = new Deadline { After = TimeSpan.FromHours(1), Action = action, EscalateTo = escalateTo } };
    }
}