using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using Npgsql;

using Themis.Core;
using Themis.Operations;
using Themis.Testing;

using Xunit;

using static Themis.Tests.Requests;

namespace Themis.Tests;

/// <summary>The jobs, the observer's deliveries and the alerts against a real database in a container, each test on a database of its own (<c>04</c> APR-OPS-004 to APR-OPS-006; <c>01</c> APR-REQ-014; <c>03</c> APR-LIB-007).</summary>
[Trait("Category", "integration")]
public sealed class JobTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Midnight = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly ApprovalTypeDefinition OneStep = Type(Step("review", ApprovalMode.AnyOne, Person(A)));

    [Fact]
    public async Task APR_LIB_007_AC1_HostTestPublishesCreatesDecidesAndReadsTheApprovedEventFromTheRecordingObserverAfterRunUntilIdleAsync()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        TestJobs jobs = new(host.Services.GetRequiredService<IApprovalJobs>());
        await host.PublishAsync(OneStep);
        RequestId request = await host.CreatedAsync(Request());
        await host.DecideAsync(request, A);

        int handled = await jobs.RunUntilIdleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, handled);
        ApprovalEvent approved = host.Observer.Received[^1];
        Assert.Equal((EventKind.Approved, request, 2L), (approved.Kind, approved.Request, approved.Sequence));
        Assert.Equal([Requester, Watcher], approved.Notify);
        Assert.Equal(0, await jobs.RunUntilIdleAsync(TestContext.Current.CancellationToken));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_014_AC1_EventWrittenInAHostTransactionThatRollsBackIsNeverDelivered()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(OneStep);
        await using NpgsqlConnection database = await host.OpenHostAsync();
        await using (NpgsqlTransaction transaction = await database.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            await host.CreateAsync(Request(), transaction);
            Assert.Equal(0, await host.RunAsync(ApprovalJob.Observer));
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(0, await host.RunAsync(ApprovalJob.Observer));
        Assert.Empty(host.Observer.Received);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_014_AC2_ObserverThatThrowsIsCalledAgainWithTheSameEventAndARequestsEventsReachItInOrder()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(Type(Step("review", ApprovalMode.AnyOne, Person(A)), Step("finance", ApprovalMode.AnyOne, Person(B))));
        RequestId request = await host.CreatedAsync(Request());
        await host.DecideAsync(request, A);
        await host.DecideAsync(request, B);
        List<ApprovalEventId> tried = [];
        host.Observer.FailWith(approvalEvent =>
        {
            tried.Add(approvalEvent.Event);
            return approvalEvent.Sequence == 2 && tried.Count(found => found == approvalEvent.Event) == 1;
        });

        int first = await host.RunAsync(ApprovalJob.Observer);
        int second = await host.RunAsync(ApprovalJob.Observer);

        Assert.Equal((1, 2), (first, second));
        Assert.Equal([1L, 2L, 3L], host.Observer.Received.Select(found => found.Sequence));
        Assert.Equal(2, tried.Count(found => found == host.Observer.Received[1].Event));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_OPS_004_AC4_ObserverFailingOnOneRequestsEventStillReceivesTheEventsOfOtherRequestsInTheSameRun()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(OneStep);
        RequestId failing = await host.CreatedAsync(Request());
        RequestId other = await host.CreatedAsync(Request() with { Target = new Target { Kind = "order", Id = "order-other" } });
        await host.DecideAsync(other, A);
        host.Observer.FailWith(approvalEvent => approvalEvent.Request == failing);

        int handled = await host.RunAsync(ApprovalJob.Observer);

        Assert.Equal(2, handled);
        Assert.All(host.Observer.Received, found => Assert.Equal(other, found.Request));
        Assert.Equal([EventKind.Requested, EventKind.Approved], host.Observer.Received.Select(found => found.Kind));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_OPS_004_AC3_ProcessThatDiesHoldingAJobsClaimLeavesItToAnotherProcessAfterTheClaimTimeout()
    {
        ClockFake clock = new(Midnight);
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: clock);
        await host.PublishAsync(OneStep);
        await host.CreatedAsync(Request());
        await HoldClaimAsync(host, "observer", "process-dead", Midnight + TimeSpan.FromMinutes(5));

        int held = await host.RunAsync(ApprovalJob.Observer);
        clock.Advance(TimeSpan.FromMinutes(4));
        int before = await host.RunAsync(ApprovalJob.Observer);
        clock.Advance(TimeSpan.FromMinutes(1));
        int after = await host.RunAsync(ApprovalJob.Observer);

        Assert.Equal((0, 0, 1), (held, before, after));
        Assert.Single(host.Observer.Received);
        Assert.Equal(("none", Midnight + TimeSpan.FromMinutes(5)), await JobAsync(host, "observer"));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task RunAsync_Deadlines_TakesAndReleasesItsClaimRecordingItsCompletionAndHandlesNothing()
    {
        ClockFake clock = new(Midnight);
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: clock);

        int handled = await host.RunAsync(ApprovalJob.Deadlines);

        Assert.Equal(0, handled);
        Assert.Equal(("none", Midnight), await JobAsync(host, "deadlines"));
    }

    [Fact]
    public async Task RunAsync_ClaimHeldByAnother_AnswersNothingAndLeavesTheClaim()
    {
        ClockFake clock = new(Midnight);
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: clock);
        await HoldClaimAsync(host, "deadlines", "process-other", Midnight + TimeSpan.FromMinutes(5));

        int handled = await host.RunAsync(ApprovalJob.Deadlines);

        Assert.Equal(0, handled);
        Assert.Equal(("process-other", (DateTimeOffset?)null), await JobAsync(host, "deadlines"));
    }

    [Fact]
    public async Task RunAsync_JobsDisabled_ThrowsInvalidOperationException()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres, new Dictionary<string, string?> { ["Themis:Jobs:Enabled"] = "false" });

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.RunAsync(ApprovalJob.Deadlines));
        await Assert.ThrowsAsync<InvalidOperationException>(() => host.RunAsync(ApprovalJob.Observer));
    }

    [Fact]
    public async Task RunAsync_ObserverDisabled_CompletesAtOnceDeliveringNothing()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres, new Dictionary<string, string?> { ["Themis:Observer:Enabled"] = "false" });
        await host.PublishAsync(OneStep);
        await host.CreatedAsync(Request());

        int handled = await host.RunAsync(ApprovalJob.Observer);

        Assert.Equal(0, handled);
        Assert.Empty(host.Observer.Received);
        Assert.NotNull((await JobAsync(host, "observer")).Completed);
    }

    [Fact]
    public async Task HostedJobs_JobsEnabledAndHosted_RunEachJobOnTheTimerAndDeliverTheEvents()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres, new Dictionary<string, string?> { ["Themis:Jobs:Interval"] = "00:00:05" }, hosted: true);
        await host.PublishAsync(OneStep);
        await host.CreatedAsync(Request());

        await WaitAsync(async () => host.Observer.Received.Count == 1 && (await Task.WhenAll(JobAsync(host, "deadlines"), JobAsync(host, "observer"), JobAsync(host, "watch"))).All(job => job.Completed is not null));

        Assert.Equal(EventKind.Requested, Assert.Single(host.Observer.Received).Kind);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HostedJobs_AddThemisTestingOrHostedOff_RunNoJob(bool hosted)
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres, new Dictionary<string, string?> { ["Themis:Jobs:Hosted"] = hosted ? "false" : "true" }, hosted: hosted);

        await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.Equal("0", await CountStartedAsync(host));
    }

    [Fact]
    public async Task APR_OPS_006_AC1_WithRepeatOneHourAConditionHoldingFromMidnightTo0230IsRaisedAt00000100And0200AndClearedOnce()
    {
        ClockFake clock = new(Midnight - TimeSpan.FromMinutes(6));
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: clock);
        await host.PublishAsync(OneStep);
        await host.CreatedAsync(Request());

        List<string> raised = [];
        for (TimeSpan at = TimeSpan.Zero; at <= TimeSpan.FromHours(3); at += TimeSpan.FromMinutes(30))
        {
            clock.Now = Midnight + at;
            if (at == TimeSpan.FromMinutes(150))
            {
                await host.RunAsync(ApprovalJob.Observer);
            }

            int before = host.Alerts.Calls.Length;
            await host.RunAsync(ApprovalJob.Watch);
            raised.AddRange(host.Alerts.Calls.Skip(before).Select(call => $"{(call.Raised ? "raised" : "cleared")} {at:hh\\:mm}"));
        }

        Assert.Equal(["raised 00:00", "raised 01:00", "raised 02:00", "cleared 02:30"], raised);
        AlertCall first = host.Alerts.Calls[0];
        Assert.Equal(("observer-failing", (string?)null), (first.Condition, first.Scope));
        Assert.Equal((Midnight - TimeSpan.FromMinutes(6)).ToString("O", CultureInfo.InvariantCulture), Assert.Single(first.Details, detail => detail.Key == "oldest").Value);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_OPS_006_AC2_TwoProcessesSeeingTheSameConditionRaiseItOnce()
    {
        ClockFake clock = new(Midnight - TimeSpan.FromMinutes(6));
        await using RequestHost first = await RequestHost.StartAsync(postgres, clock: clock);
        await using RequestHost second = await RequestHost.StartOnAsync(first.Connection, clock: clock);
        await first.PublishAsync(OneStep);
        await first.CreatedAsync(Request());
        clock.Now = Midnight;

        await Task.WhenAll(Task.Run(() => first.RunAsync(ApprovalJob.Watch), TestContext.Current.CancellationToken), Task.Run(() => second.RunAsync(ApprovalJob.Watch), TestContext.Current.CancellationToken));
        await Task.WhenAll(first.RunAsync(ApprovalJob.Watch), second.RunAsync(ApprovalJob.Watch));

        Assert.Single(first.Alerts.Calls.Concat(second.Alerts.Calls));
        first.AssertNoLogHoldsAValueUsed();
        second.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_OPS_006_AC3_RequestCancelledWithNoApproverRaisesRequestUnassignableForItsTypeClearedOnceTheRepeatPassesWithNoNewOccurrence()
    {
        ClockFake clock = new(Midnight);
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: clock);
        await host.PublishAsync(Type(Step("review", ApprovalMode.AnyOne, Person(Requester)) with { Fallback = Requester }) with { Fallback = Requester });
        RequestId request = (await host.CreateAsync(Request())).Value!.Request;

        await host.RunAsync(ApprovalJob.Watch);
        clock.Advance(TimeSpan.FromMinutes(59));
        await host.RunAsync(ApprovalJob.Watch);
        clock.Advance(TimeSpan.FromMinutes(1));
        await host.RunAsync(ApprovalJob.Watch);
        clock.Advance(TimeSpan.FromHours(2));
        await host.RunAsync(ApprovalJob.Watch);

        AlertCall[] unassignable = [.. host.Alerts.Calls.Where(call => call.Condition == "request-unassignable")];
        Assert.Equal([(true, "request-unassignable", Key), (false, "request-unassignable", Key)], unassignable.Select(call => (call.Raised, call.Condition, call.Scope)));
        Assert.Equal([new KeyValuePair<string, string>("request", request.Value.ToString("D")), new KeyValuePair<string, string>("step", "1")], unassignable[0].Details.OrderBy(detail => detail.Key, StringComparer.Ordinal));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_OPS_005_AC2_MeterCountsRequestsCreatedAndClosedByStatusAndTheHealthCheckTurnsUnhealthyPastTheLagThreshold()
    {
        ClockFake clock = new(Midnight);
        List<(string Name, long Value, string Tags)> measured = [];
        Meter? own = null;
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, subscribed) =>
        {
            if (instrument.Meter.Name == "Themis")
            {
                subscribed.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            if (ReferenceEquals(instrument.Meter, Volatile.Read(ref own)))
            {
                string tagged = string.Join(',', tags.ToArray().Select(tag => $"{tag.Key}={tag.Value}").Order(StringComparer.Ordinal));
                lock (measured)
                {
                    measured.Add((instrument.Name, value, tagged));
                }
            }
        });
        listener.Start();
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: clock);
        Volatile.Write(ref own, host.Services.GetRequiredService<ApprovalsMetrics>().RequestsCreated.Meter);
        HealthCheckService health = host.Services.GetRequiredService<HealthCheckService>();
        await host.PublishAsync(OneStep);
        RequestId rejected = await host.CreatedAsync(Request());
        RequestId cancelled = await host.CreatedAsync(Request() with { Target = new Target { Kind = "order", Id = "order-2" } });
        RequestId approved = await host.CreatedAsync(Request() with { Target = new Target { Kind = "order", Id = "order-3" } });
        await host.DecideAsync(rejected, A, DecisionOutcome.Reject, Reason);
        await host.CancelAsync(cancelled, Requester);
        await host.DecideAsync(approved, A);

        HealthStatus fresh = (await health.CheckHealthAsync(TestContext.Current.CancellationToken)).Status;
        clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        HealthStatus lagging = (await health.CheckHealthAsync(TestContext.Current.CancellationToken)).Status;
        await host.RunAsync(ApprovalJob.Watch);
        listener.RecordObservableInstruments();
        await host.RunAsync(ApprovalJob.Observer);
        HealthStatus delivered = (await health.CheckHealthAsync(TestContext.Current.CancellationToken)).Status;

        (string Name, long Value, string Tags)[] seen;
        lock (measured)
        {
            seen = [.. measured];
        }

        Assert.Equal((HealthStatus.Healthy, HealthStatus.Unhealthy, HealthStatus.Healthy), (fresh, lagging, delivered));
        Assert.Equal(3, seen.Where(found => found.Name == "approvals.requests.created" && found.Tags == $"type={Key}").Sum(found => found.Value));
        Assert.Equal(
            [$"status=cancelled,type={Key}", $"status=rejected,type={Key}"],
            seen.Where(found => found.Name == "approvals.requests.closed").Select(found => found.Tags).Order(StringComparer.Ordinal));
        Assert.Equal(6, seen.Single(found => found.Name == "approvals.events.pending").Value);
        Assert.Equal(6, seen.Where(found => found.Name == "approvals.events.delivered").Sum(found => found.Value));
        host.AssertNoLogHoldsAValueUsed();
    }

    private static async Task HoldClaimAsync(RequestHost host, string job, string holder, DateTimeOffset expires)
    {
        await using NpgsqlConnection database = new(host.Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand hold = new("UPDATE approvals.jobs SET holder = @holder, claim_expires_at = @expires WHERE name = @name", database);
        hold.Parameters.AddWithValue("holder", holder);
        hold.Parameters.AddWithValue("expires", expires);
        hold.Parameters.AddWithValue("name", job);
        await hold.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>The job's holder, "none" where it is free, and its last completion.</summary>
    private static async Task<(string Holder, DateTimeOffset? Completed)> JobAsync(RequestHost host, string job)
    {
        await using NpgsqlConnection database = new(host.Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand select = new("SELECT coalesce(holder, 'none'), last_completed_at FROM approvals.jobs WHERE name = @name", database);
        select.Parameters.AddWithValue("name", job);
        await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        await reader.ReadAsync(TestContext.Current.CancellationToken);
        return (reader.GetString(0), await reader.IsDBNullAsync(1, TestContext.Current.CancellationToken) ? null : await reader.GetFieldValueAsync<DateTimeOffset>(1, TestContext.Current.CancellationToken));
    }

    private static async Task<string> CountStartedAsync(RequestHost host)
    {
        await using NpgsqlConnection database = new(host.Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand count = new("SELECT count(*)::text FROM approvals.jobs WHERE last_started_at IS NOT NULL", database);
        return (string)(await count.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private static async Task WaitAsync(Func<Task<bool>> done)
    {
        for (int round = 0; round < 300; round++)
        {
            if (await done())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        }

        throw new TimeoutException("The hosted jobs did not run within fifteen seconds.");
    }
}