using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using Themis.Core;

using Xunit;

namespace Observatory.Tests;

/// <summary>
/// The sample host's requests through its own endpoints, against a real database (<c>05</c>
/// APR-ENG-001). Its persons are those of the sample's permission table; the requesters,
/// proposals, instruments and reasons are the tests' own, each distinctive, so a log line
/// holding one is found.
/// </summary>
[Trait("Category", "sample")]
public sealed class ObservatoryTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Astronomer = "astronomer-5b7e";

    private const string Engineer = "engineer-2c9a";

    private const string Scheduler = "scheduler-1";

    private const string Director = "director";

    private const string Deputy = "deputy-director";

    private const string Desk = "desk-1";

    private const string Custodian = "custodian";

    private const string Proposal = "proposal-8d1f";

    private const string Instrument = "spectrograph-4a6c";

    private const string Reason = "reason-clouded-3e0b";

    private static readonly string[] Unloggable = [Astronomer, Engineer, Scheduler, Director, Deputy, Desk, Custodian, Proposal, Instrument, Reason];

    [Fact]
    public async Task TelescopeTime_ScheduledThenDirected_IsApprovedAndTheObserverTakesTheApprovedEvent()
    {
        await using ObservatoryHost host = await ObservatoryHost.StartAsync(postgres);
        Guid request = await host.CreateAsync("/telescope-time", new { proposal = Proposal, hours = 6.5m }, Astronomer);

        Assert.Equal("pending", await host.DecideAsync(request, Scheduler, "approve"));
        Assert.Equal("approved", await host.DecideAsync(request, Director, "approve"));
        await host.Jobs.RunUntilIdleAsync(TestContext.Current.CancellationToken);

        ApprovalEvent approved = host.Observer.Received[^1];
        Assert.Equal((EventKind.Approved, new RequestId(request)), (approved.Kind, approved.Request));
        host.AssertNoLogHoldsAValueUsed(Unloggable);
    }

    [Fact]
    public async Task TelescopeTime_ScheduleDeadlinePassed_IsEscalatedToTheDeputyDirector()
    {
        await using ObservatoryHost host = await ObservatoryHost.StartAsync(postgres);
        Guid request = await host.CreateAsync("/telescope-time", new { proposal = Proposal, hours = 3m }, Astronomer);
        Assert.Empty(await host.PendingAsync(Deputy));

        host.Clock.Advance(TimeSpan.FromDays(2) + TimeSpan.FromMinutes(1));
        await host.Jobs.RunUntilIdleAsync(TestContext.Current.CancellationToken);

        Assert.Equal([(request, "schedule")], await host.PendingAsync(Deputy));
        Assert.Equal("pending", await host.DecideAsync(request, Deputy, "approve"));
        Assert.Equal([(request, "director")], await host.PendingAsync(Director));
        Assert.Contains(host.Observer.Received, found => found.Kind == EventKind.Escalated && found.Request == new RequestId(request));
        host.AssertNoLogHoldsAValueUsed(Unloggable);
    }

    [Fact]
    public async Task InstrumentLoan_ValueWithinTheDesksLimit_IsApprovedByTheDeskAlone()
    {
        await using ObservatoryHost host = await ObservatoryHost.StartAsync(postgres);
        Guid request = await host.CreateAsync("/instrument-loans", new { instrument = Instrument, value = 2500m }, Engineer);

        Assert.Equal("approved", await host.DecideAsync(request, Desk, "approve"));

        RequestRecord record = await host.ReadAsync(request);
        Assert.Equal([StepStatus.Done, StepStatus.Skipped], record.Steps.OrderBy(step => step.Number).Select(step => step.Status));
        host.AssertNoLogHoldsAValueUsed(Unloggable);
    }

    [Fact]
    public async Task InstrumentLoan_ValueAboveTheDesksLimit_WaitsForTheCustodian()
    {
        await using ObservatoryHost host = await ObservatoryHost.StartAsync(postgres);
        Guid request = await host.CreateAsync("/instrument-loans", new { instrument = Instrument, value = 25000m }, Engineer);

        Assert.Equal("pending", await host.DecideAsync(request, Desk, "approve"));
        Assert.Equal([(request, "custody")], await host.PendingAsync(Custodian));
        Assert.Equal("approved", await host.DecideAsync(request, Custodian, "approve"));
        host.AssertNoLogHoldsAValueUsed(Unloggable);
    }

    [Fact]
    public async Task InstrumentLoan_RejectedByTheDesk_IsRejected()
    {
        await using ObservatoryHost host = await ObservatoryHost.StartAsync(postgres);
        Guid request = await host.CreateAsync("/instrument-loans", new { instrument = Instrument, value = 2500m }, Engineer);

        Assert.Equal("rejected", await host.DecideAsync(request, Desk, "reject", Reason));
        host.AssertNoLogHoldsAValueUsed(Unloggable);
    }

    [Fact]
    public async Task Request_CancelledByItsRequester_IsCancelledAndLeavesThePendingList()
    {
        await using ObservatoryHost host = await ObservatoryHost.StartAsync(postgres);
        Guid request = await host.CreateAsync("/telescope-time", new { proposal = Proposal, hours = 2m }, Astronomer);

        using HttpResponseMessage cancelled = await host.SendAsync(HttpMethod.Post, $"/requests/{request}/cancel", Astronomer, new { reason = Reason });

        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
        Assert.Equal(RequestStatus.Cancelled, (await host.ReadAsync(request)).Status);
        Assert.Empty(await host.PendingAsync(Scheduler));
        host.AssertNoLogHoldsAValueUsed(Unloggable);
    }

    [Fact]
    public async Task Request_ApprovedThenCompleted_IsCompleted()
    {
        await using ObservatoryHost host = await ObservatoryHost.StartAsync(postgres);
        Guid request = await host.CreateAsync("/instrument-loans", new { instrument = Instrument, value = 900m }, Engineer);
        await host.DecideAsync(request, Desk, "approve");

        using HttpResponseMessage completed = await host.SendAsync(HttpMethod.Post, $"/requests/{request}/complete", Engineer);

        Assert.Equal(HttpStatusCode.NoContent, completed.StatusCode);
        Assert.Equal(RequestStatus.Completed, (await host.ReadAsync(request)).Status);
        host.AssertNoLogHoldsAValueUsed(Unloggable);
    }

    [Fact]
    public async Task Decision_ByAPersonNotAssigned_IsForbiddenWithTheLibrarysCode()
    {
        await using ObservatoryHost host = await ObservatoryHost.StartAsync(postgres);
        Guid request = await host.CreateAsync("/telescope-time", new { proposal = Proposal, hours = 1m }, Astronomer);

        using HttpResponseMessage refused = await host.SendAsync(HttpMethod.Post, $"/approvals/requests/{request}/decisions", Engineer, new { outcome = "approve" });

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        using JsonDocument body = await JsonDocument.ParseAsync(await refused.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("approvals.decision.notassigned", body.RootElement.GetProperty("code").GetString());
        host.AssertNoLogHoldsAValueUsed(Unloggable);
    }

    [Fact]
    public async Task APR_LIB_004_AC5_SampleHostServesTheLibrarysEndpointsForThePersonOfItsHeader()
    {
        await using ObservatoryHost host = await ObservatoryHost.StartAsync(postgres);
        Guid request = await host.CreateAsync("/telescope-time", new { proposal = Proposal, hours = 4m }, Astronomer);

        using HttpResponseMessage read = await host.SendAsync(HttpMethod.Get, $"/approvals/requests/{request}", Astronomer);
        using HttpResponseMessage hidden = await host.SendAsync(HttpMethod.Get, $"/approvals/requests/{request}", Engineer);
        using HttpResponseMessage created = await host.SendAsync(HttpMethod.Post, "/approvals/delegations", Scheduler, new { to = Deputy, starts = ObservatoryHost.Start, ends = ObservatoryHost.Start.AddDays(7) });

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.Created), (read.StatusCode, hidden.StatusCode, created.StatusCode));
        Assert.Equal([(request, "schedule")], await host.PendingAsync(Deputy));
        Assert.Equal("pending", await host.DecideAsync(request, Deputy, "approve", actingFor: Scheduler));
        Assert.Equal(Scheduler, (await host.ReadAsync(request)).Decisions.Single().ActingFor);
        host.AssertNoLogHoldsAValueUsed(Unloggable);
    }

    [Theory]
    [InlineData("POST", "/telescope-time")]
    [InlineData("GET", "/approvals/pending")]
    [InlineData("GET", "/approvals/delegations")]
    public async Task Endpoints_NoPerson_AreRefused(string method, string path)
    {
        await using ObservatoryHost host = await ObservatoryHost.StartAsync(postgres);

        using HttpResponseMessage refused = await host.SendAsync(new HttpMethod(method), path, person: null, method == "POST" ? new { proposal = Proposal, hours = 1m } : null);

        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        host.AssertNoLogHoldsAValueUsed(Unloggable);
    }

    [Fact]
    public async Task EventLog_EventsOfARequest_AreLoggedByKindAndRequestNamingNoPerson()
    {
        await using ObservatoryHost host = await ObservatoryHost.StartAsync(postgres, sampleObserver: true);
        Guid request = await host.CreateAsync("/instrument-loans", new { instrument = Instrument, value = 400m }, Engineer);
        await host.DecideAsync(request, Desk, "approve");

        await host.Jobs.RunUntilIdleAsync(TestContext.Current.CancellationToken);

        string[] logged = [.. host.Logs.Entries.Where(entry => entry.Category == "Observatory.EventLog").Select(entry => entry.Message)];
        Assert.Contains(logged, message => message.Contains("Approved", StringComparison.Ordinal) && message.Contains(request.ToString(), StringComparison.Ordinal));
        Assert.Contains(logged, message => message.Contains("Requested", StringComparison.Ordinal));
        host.AssertNoLogHoldsAValueUsed(Unloggable);
    }
}