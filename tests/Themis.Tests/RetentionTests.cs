using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Themis.Core;
using Themis.Operations;

using Xunit;

using static Themis.Tests.Requests;

namespace Themis.Tests;

/// <summary>The <c>retention</c> job against a real database in a container, each test on a database of its own (<c>01</c> APR-REQ-015; T-025).</summary>
[Trait("Category", "integration")]
public sealed class RetentionTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Midnight = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly ApprovalTypeDefinition OneStep = Type(Step("review", ApprovalMode.AnyOne, Person(A)));

    /// <summary>A period of 30 days.</summary>
    private static readonly Dictionary<string, string?> ThirtyDays = new() { ["Themis:Retention:ClosedRequests"] = "30.00:00:00" };

    [Fact]
    public async Task APR_REQ_015_AC3_RequestClosedADayLessThanThePeriodStaysAndOneClosedADayMoreGoesWithEveryRowOfItAndNothingElseDoes()
    {
        ClockFake clock = new(Midnight);
        await using RequestHost host = await RequestHost.StartAsync(postgres, ThirtyDays, clock: clock);
        await host.PublishAsync(OneStep);
        RequestId old = await host.CreatedAsync(On("order-old"));
        await host.DecideAsync(old, A, DecisionOutcome.Reject, Reason);
        clock.Advance(TimeSpan.FromDays(2));
        RequestId recent = await host.CreatedAsync(On("order-recent"));
        await host.CancelAsync(recent, Requester);
        RequestId pending = await host.CreatedAsync(On("order-pending"));
        RequestId approved = await host.CreatedAsync(On("order-approved"));
        await host.DecideAsync(approved, A);
        await DeliverAsync(host);
        clock.Advance(TimeSpan.FromDays(29));
        string oldRows = await RowsAsync(host, old);
        string[] others = await Task.WhenAll(RowsAsync(host, recent), RowsAsync(host, pending), RowsAsync(host, approved));
        string versions = await VersionsAsync(host);

        int deleted = await host.RunAsync(ApprovalJob.Retention);

        Assert.Equal("1,1,1,1,2", oldRows);
        Assert.Equal(1, deleted);
        Assert.Equal("0,0,0,0,0", await RowsAsync(host, old));
        Assert.Equal(others, await Task.WhenAll(RowsAsync(host, recent), RowsAsync(host, pending), RowsAsync(host, approved)));
        Assert.Equal(versions, await VersionsAsync(host));
        Assert.Equal(0, await host.RunAsync(ApprovalJob.Retention));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_015_AC9_DelegationEndedADayMoreThanThePeriodAgoGoesWithItsEventAndOneEndedADayLessStays()
    {
        ClockFake clock = new(Midnight);
        await using RequestHost host = await RequestHost.StartAsync(postgres, ThirtyDays, clock: clock);
        DelegationId ended = await host.DelegatedAsync(Lend(A, B, TimeSpan.FromDays(30)));
        await host.EndDelegationAsync(ended, A);
        DelegationId lapsed = await host.DelegatedAsync(Lend(A, C, TimeSpan.FromDays(2)));
        DelegationId live = await host.DelegatedAsync(Lend(B, C, TimeSpan.FromDays(300)));
        await DeliverAsync(host);
        clock.Advance(TimeSpan.FromDays(31));

        int deleted = await host.RunAsync(ApprovalJob.Retention);

        Assert.Equal(1, deleted);
        Assert.Equal("0,0", await DelegationRowsAsync(host, ended));
        Assert.Equal("1,1", await DelegationRowsAsync(host, lapsed));
        Assert.Equal("1,1", await DelegationRowsAsync(host, live));
        clock.Advance(TimeSpan.FromDays(2));
        Assert.Equal(1, await host.RunAsync(ApprovalJob.Retention));
        Assert.Equal("0,0", await DelegationRowsAsync(host, lapsed));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_015_AC4_UndeliveredEventHoldsARequestPastThePeriodUntilTheObserverTakesIt()
    {
        ClockFake clock = new(Midnight);
        await using RequestHost host = await RequestHost.StartAsync(postgres, ThirtyDays, clock: clock);
        await host.PublishAsync(OneStep);
        RequestId request = await host.CreatedAsync(Request());
        await host.CancelAsync(request, Requester);
        clock.Advance(TimeSpan.FromDays(40));

        int held = await host.RunAsync(ApprovalJob.Retention);
        string requestHeld = await RowsAsync(host, request);
        await DeliverAsync(host);
        int gone = await host.RunAsync(ApprovalJob.Retention);

        Assert.Equal((0, "1,1,1,0,2"), (held, requestHeld));
        Assert.Equal(1, gone);
        Assert.Equal("0,0,0,0,0", await RowsAsync(host, request));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_015_AC9_UndeliveredEventHoldsADelegationPastThePeriodUntilTheObserverTakesIt()
    {
        ClockFake clock = new(Midnight);
        await using RequestHost host = await RequestHost.StartAsync(postgres, ThirtyDays, clock: clock);
        DelegationId delegation = await host.DelegatedAsync(Lend(A, B, TimeSpan.FromDays(1)));
        clock.Advance(TimeSpan.FromDays(40));

        int held = await host.RunAsync(ApprovalJob.Retention);
        string delegationHeld = await DelegationRowsAsync(host, delegation);
        await DeliverAsync(host);
        int gone = await host.RunAsync(ApprovalJob.Retention);

        Assert.Equal((0, "1,1"), (held, delegationHeld));
        Assert.Equal(1, gone);
        Assert.Equal("0,0", await DelegationRowsAsync(host, delegation));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_015_AC4_LaterRequestNamingItAsPreviousThatIsNotDueHoldsItAndTheChainGoesTogether()
    {
        ClockFake clock = new(Midnight);
        await using RequestHost host = await RequestHost.StartAsync(postgres, ThirtyDays, clock: clock);
        await host.PublishAsync(OneStep);
        RequestId first = await host.CreatedAsync(Request());
        await host.CancelAsync(first, Requester);
        clock.Advance(TimeSpan.FromDays(10));
        RequestId second = await host.CreatedAsync(Request() with { Previous = first });
        await host.DecideAsync(second, A, DecisionOutcome.Reject, Reason);
        await DeliverAsync(host);
        clock.Advance(TimeSpan.FromDays(25));

        int held = await host.RunAsync(ApprovalJob.Retention);
        string firstHeld = await RowsAsync(host, first);
        clock.Advance(TimeSpan.FromDays(6));
        int gone = await host.RunAsync(ApprovalJob.Retention);

        Assert.Equal((0, "1,1,1,0,2"), (held, firstHeld));
        Assert.Equal(2, gone);
        Assert.Equal(("0,0,0,0,0", "0,0,0,0,0"), (await RowsAsync(host, first), await RowsAsync(host, second)));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_015_AC8_WithThePeriodUnsetNothingIsDeletedAndTheRunNeedsNoGrant()
    {
        ClockFake clock = new(Midnight);
        await using RequestHost host = await ApplicationHostAsync(postgres, configuration: null, clock, retention: false);
        await host.PublishAsync(OneStep);
        RequestId request = await host.CreatedAsync(Request());
        await host.CancelAsync(request, Requester);
        DelegationId delegation = await host.DelegatedAsync(Lend(A, B, TimeSpan.FromDays(1)));
        await DeliverAsync(host);
        clock.Advance(TimeSpan.FromDays(365 * 40));

        int deleted = await host.RunAsync(ApprovalJob.Retention);

        Assert.Equal(0, deleted);
        Assert.Equal(("1,1,1,0,2", "1,1"), (await RowsAsync(host, request), await DelegationRowsAsync(host, delegation)));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_015_AC6_RunWithoutTheRetentionGrantFailsNamingRetentionNotGrantedAndRunsOnceTheLoginHoldsThemisRetention()
    {
        ClockFake clock = new(Midnight);
        (_, string connection) = await postgres.NewMigratedDatabaseAsync();
        string login = await ApplicationLoginAsync(postgres, connection, retention: false);
        await using RequestHost host = await RequestHost.StartOnAsync(LoginConnection(connection, login), ThirtyDays, clock: clock);
        await host.PublishAsync(OneStep);
        RequestId request = await host.CreatedAsync(Request());
        await host.CancelAsync(request, Requester);
        await DeliverAsync(host);
        clock.Advance(TimeSpan.FromDays(31));

        InvalidOperationException refused = await Assert.ThrowsAsync<InvalidOperationException>(() => host.RunAsync(ApprovalJob.Retention));
        Assert.Null(await SchemaMigration.ApplyAsync(connection, [login], [login], TestContext.Current.CancellationToken));
        int deleted = await host.RunAsync(ApprovalJob.Retention);

        Assert.StartsWith("approvals.retention.notgranted", refused.Message, StringComparison.Ordinal);
        Assert.Equal(1, deleted);
        Assert.Equal("0,0,0,0,0", await RowsAsync(host, request));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task RunAsync_Retention_CountsEachRequestAndDelegationDeletedByKind()
    {
        ClockFake clock = new(Midnight);
        List<(long Value, string Tags)> measured = [];
        Meter? own = null;
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, subscribed) =>
        {
            if (instrument.Name == "approvals.retention.deleted")
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
        await using RequestHost host = await RequestHost.StartAsync(postgres, ThirtyDays, clock: clock);
        Volatile.Write(ref own, host.Services.GetRequiredService<ApprovalsMetrics>().RetentionDeleted.Meter);
        await host.PublishAsync(OneStep);
        RequestId first = await host.CreatedAsync(Request());
        await host.CancelAsync(first, Requester);
        RequestId second = await host.CreatedAsync(Request() with { Previous = first });
        await host.CancelAsync(second, Requester);
        await host.DelegatedAsync(Lend(A, B, TimeSpan.FromDays(1)));
        await DeliverAsync(host);
        clock.Advance(TimeSpan.FromDays(32));

        await host.RunAsync(ApprovalJob.Retention);

        lock (measured)
        {
            Assert.Equal(2, measured.Where(found => found.Tags == "kind=request").Sum(found => found.Value));
            Assert.Equal(1, measured.Where(found => found.Tags == "kind=delegation").Sum(found => found.Value));
        }

        host.AssertNoLogHoldsAValueUsed();
    }

    /// <summary>Makes a login role with the application's grants, and the retention grant where <paramref name="retention"/> is set, on the migrated database of <paramref name="connection"/>; answers its name.</summary>
    internal static async Task<string> ApplicationLoginAsync(PostgresFixture postgres, string connection, bool retention)
    {
        string login = "app_" + Guid.NewGuid().ToString("N")[..12];
        await postgres.RunAsync($"CREATE ROLE {login} LOGIN PASSWORD 'test';");
        Assert.Null(await SchemaMigration.ApplyAsync(connection, [login], retention ? [login] : [], TestContext.Current.CancellationToken));
        return login;
    }

    /// <summary>The connection string of <paramref name="login"/> to the database of <paramref name="connection"/>.</summary>
    internal static string LoginConnection(string connection, string login)
    {
        return new NpgsqlConnectionStringBuilder(connection) { Username = login, Password = "test" }.ConnectionString;
    }

    /// <summary>Starts a host on a new migrated database connecting as a login with the application's grants, and the retention grant where <paramref name="retention"/> is set.</summary>
    internal static async Task<RequestHost> ApplicationHostAsync(PostgresFixture postgres, IReadOnlyDictionary<string, string?>? configuration, TimeProvider clock, bool retention)
    {
        (_, string connection) = await postgres.NewMigratedDatabaseAsync();
        string login = await ApplicationLoginAsync(postgres, connection, retention);
        return await RequestHost.StartOnAsync(LoginConnection(connection, login), configuration, clock: clock);
    }

    /// <summary>Runs the observer until it takes nothing more.</summary>
    internal static async Task DeliverAsync(RequestHost host)
    {
        while (await host.RunAsync(ApprovalJob.Observer) > 0)
        {
        }
    }

    /// <summary>The rows of the request in each of the request tables, as one text: requests, steps, assignments, decisions, events.</summary>
    internal static async Task<string> RowsAsync(RequestHost host, RequestId request)
    {
        await using NpgsqlConnection database = new(host.Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand count = new(
            """
            SELECT concat_ws(',', (SELECT count(*) FROM approvals.requests WHERE id = @id), (SELECT count(*) FROM approvals.steps WHERE request = @id),
                (SELECT count(*) FROM approvals.assignments WHERE request = @id), (SELECT count(*) FROM approvals.decisions WHERE request = @id),
                (SELECT count(*) FROM approvals.events WHERE request = @id))
            """,
            database);
        count.Parameters.AddWithValue("id", request.Value);
        return (string)(await count.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private static ApprovalRequest On(string target)
    {
        return Request() with { Target = new Target { Kind = "order", Id = target } };
    }

    private static DelegationDefinition Lend(string from, string to, TimeSpan period)
    {
        return new DelegationDefinition { From = from, To = to, Starts = Midnight, Ends = Midnight + period, CreatedBy = Requester };
    }

    /// <summary>The delegation's rows and its events', as one text.</summary>
    private static async Task<string> DelegationRowsAsync(RequestHost host, DelegationId delegation)
    {
        await using NpgsqlConnection database = new(host.Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand count = new("SELECT concat_ws(',', (SELECT count(*) FROM approvals.delegations WHERE id = @id), (SELECT count(*) FROM approvals.events WHERE delegation = @id))", database);
        count.Parameters.AddWithValue("id", delegation.Value);
        return (string)(await count.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    /// <summary>The versions the database holds, each as its type, number and hash.</summary>
    private static async Task<string> VersionsAsync(RequestHost host)
    {
        await using NpgsqlConnection database = new(host.Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand read = new("SELECT coalesce(string_agg(key || ':' || version || ':' || encode(hash, 'hex'), ',' ORDER BY key, version), '') FROM approvals.versions", database);
        return (string)(await read.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}