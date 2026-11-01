using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Themis.Core;
using Themis.Operations;

using Xunit;

using static Themis.Tests.Requests;

namespace Themis.Tests;

/// <summary>The reminders the deadlines job sends before a step's deadline, against a real database in a container, each test on a database of its own (<c>01</c> APR-REQ-009; T-025).</summary>
[Trait("Category", "integration")]
public sealed class ReminderTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Midnight = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Day = TimeSpan.FromDays(1);

    private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);

    [Fact]
    public async Task APR_REQ_009_AC6_EachReminderFiresOnceAtItsTimeNamingTheStepsOpenAssignmentsEachWithItsDelegate()
    {
        ClockFake clock = new(Midnight);
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: clock);
        await host.PublishAsync(Type(Reminding(Step("review", ApprovalMode.All, Person(A), Person(B)), Day, TimeSpan.FromHours(2))));
        await host.DelegatedAsync(new DelegationDefinition { From = B, To = C, Starts = Midnight, Ends = Midnight + TimeSpan.FromDays(10), CreatedBy = B });
        RequestId request = await host.CreatedAsync(Request());
        await host.DecideAsync(request, A);

        clock.Advance(Day - Second);
        await host.RunAsync(ApprovalJob.Deadlines);
        int early = (await RemindedAsync(host, request)).Count;
        clock.Advance(Second);
        await host.RunAsync(ApprovalJob.Deadlines);
        await host.RunAsync(ApprovalJob.Deadlines);
        int first = (await RemindedAsync(host, request)).Count;
        clock.Advance(Day - TimeSpan.FromHours(2));
        await host.RunAsync(ApprovalJob.Deadlines);
        List<RecordedEvent> reminded = await RemindedAsync(host, request);

        Assert.Equal((0, 1, 2), (early, first, reminded.Count));
        Assert.All(reminded, found => Assert.Equal(((int?)1, (string?)null, $"{B},{C}"), (found.Step, found.Actor, string.Join(',', found.Notify))));
        Assert.Equal(RequestStatus.Pending, (await host.ReadAsync(request)).Status);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_009_AC7_NoReminderFiresAfterTheStepCloses()
    {
        ClockFake clock = new(Midnight);
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: clock);
        await host.PublishAsync(Type(Reminding(Step("review", ApprovalMode.AnyOne, Person(A)), Day)));
        RequestId rejected = await host.CreatedAsync(Request());
        await host.DecideAsync(rejected, A, DecisionOutcome.Reject, Reason);
        RequestId approved = await host.CreatedAsync(Request() with { Target = new Target { Kind = "order", Id = "order-approved" } });
        await host.DecideAsync(approved, A);
        RequestId cancelled = await host.CreatedAsync(Request() with { Target = new Target { Kind = "order", Id = "order-cancelled" } });
        await host.CancelAsync(cancelled, Requester);

        clock.Advance(Day + TimeSpan.FromHours(1));
        await host.RunAsync(ApprovalJob.Deadlines);

        int[] reminded = await Task.WhenAll(new[] { rejected, approved, cancelled }.Select(async request => (await RemindedAsync(host, request)).Count));

        Assert.Equal([0, 0, 0], reminded);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_009_AC8_RestartSendsNoReminderTwiceAndAfterAGapTheLatestDueOnceWhileTheDeadlineIsAhead()
    {
        ClockFake clock = new(Midnight);
        RequestHost first = await RequestHost.StartAsync(postgres, clock: clock);
        string connection = first.Connection;
        await first.PublishAsync(Type(Reminding(Step("review", ApprovalMode.AnyOne, Person(A)), Day, TimeSpan.FromHours(12), TimeSpan.FromHours(2))));
        RequestId request = await first.CreatedAsync(Request());
        clock.Advance(Day);
        await first.RunAsync(ApprovalJob.Deadlines);
        int beforeRestart = (await RemindedAsync(first, request)).Count;
        first.AssertNoLogHoldsAValueUsed();
        await first.DisposeAsync();

        await using RequestHost second = await RequestHost.StartOnAsync(connection, clock: clock);
        await second.RunAsync(ApprovalJob.Deadlines);
        int afterRestart = (await RemindedAsync(second, request)).Count;
        clock.Advance(TimeSpan.FromHours(23));
        await second.RunAsync(ApprovalJob.Deadlines);
        await second.RunAsync(ApprovalJob.Deadlines);
        int afterGap = (await RemindedAsync(second, request)).Count;
        clock.Advance(TimeSpan.FromMinutes(59));
        await second.RunAsync(ApprovalJob.Deadlines);

        Assert.Equal((1, 1, 2), (beforeRestart, afterRestart, afterGap));
        Assert.Equal(2, (await RemindedAsync(second, request)).Count);
        Assert.Equal(RequestStatus.Pending, (await second.ReadAsync(request)).Status);
        second.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_009_AC8_LateRunPastTheDeadlineDropsTheRemindersAndRunsTheDeadlinesActionAlone()
    {
        ClockFake clock = new(Midnight);
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: clock);
        await host.PublishAsync(Type(Reminding(Step("review", ApprovalMode.AnyOne, Person(A)), Day, TimeSpan.FromHours(2))));
        RequestId request = await host.CreatedAsync(Request());

        clock.Advance(TimeSpan.FromDays(2) + TimeSpan.FromHours(1));
        await host.RunAsync(ApprovalJob.Deadlines);
        await host.RunAsync(ApprovalJob.Deadlines);

        Assert.Equal([EventKind.Requested, EventKind.Rejected], (await host.EventsAsync(request)).Select(found => found.Kind));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task RunAsync_Deadlines_CountsEachReminderSentTaggedWithItsType()
    {
        ClockFake clock = new(Midnight);
        List<(long Value, string Tags)> measured = [];
        Meter? own = null;
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, subscribed) =>
        {
            if (instrument.Name == "approvals.reminders")
            {
                subscribed.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            if (ReferenceEquals(instrument.Meter, Volatile.Read(ref own)))
            {
                lock (measured)
                {
                    measured.Add((value, string.Join(',', tags.ToArray().Select(tag => $"{tag.Key}={tag.Value}"))));
                }
            }
        });
        listener.Start();
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: clock);
        Volatile.Write(ref own, host.Services.GetRequiredService<ApprovalsMetrics>().Reminders.Meter);
        await host.PublishAsync(Type(Reminding(Step("review", ApprovalMode.AnyOne, Person(A)), Day, TimeSpan.FromHours(2))));
        await host.CreatedAsync(Request());

        clock.Advance(Day);
        await host.RunAsync(ApprovalJob.Deadlines);
        clock.Advance(TimeSpan.FromHours(22));
        await host.RunAsync(ApprovalJob.Deadlines);

        lock (measured)
        {
            Assert.Equal([(1L, $"type={Key}"), (1L, $"type={Key}")], measured);
        }

        host.AssertNoLogHoldsAValueUsed();
    }

    /// <summary><paramref name="step"/> with a deadline of two days that rejects, and the reminders <paramref name="before"/> it.</summary>
    private static StepDefinition Reminding(StepDefinition step, params TimeSpan[] before)
    {
        return step with { Deadline = new Deadline { After = TimeSpan.FromDays(2), Action = DeadlineAction.Reject, RemindBefore = before } };
    }

    private static async Task<List<RecordedEvent>> RemindedAsync(RequestHost host, RequestId request)
    {
        return [.. (await host.EventsAsync(request)).Where(found => found.Kind == EventKind.Reminded)];
    }
}