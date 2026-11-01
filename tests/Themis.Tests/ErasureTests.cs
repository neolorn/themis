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
using Themis.Types;

using Xunit;

using static Themis.Tests.Requests;

namespace Themis.Tests;

/// <summary>Erasure against a real database in a container, each test on a database of its own (<c>01</c> APR-REQ-015; T-025).</summary>
[Trait("Category", "integration")]
public sealed class ErasureTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string OtherReason = "reason-other-3c5a";

    /// <summary>Every person column the library holds, and each entry of the events' <c>notify</c>, as <c>value</c>.</summary>
    private const string PersonValues = """
        SELECT requester AS value FROM approvals.requests UNION ALL SELECT subject FROM approvals.requests
        UNION ALL SELECT closed_by FROM approvals.requests UNION ALL SELECT closed_for FROM approvals.requests
        UNION ALL SELECT person FROM approvals.assignments
        UNION ALL SELECT decider FROM approvals.decisions UNION ALL SELECT acting_for FROM approvals.decisions
        UNION ALL SELECT from_person FROM approvals.delegations UNION ALL SELECT to_person FROM approvals.delegations
        UNION ALL SELECT created_by FROM approvals.delegations UNION ALL SELECT ended_by FROM approvals.delegations
        UNION ALL SELECT actor FROM approvals.events UNION ALL SELECT acting_for FROM approvals.events
        UNION ALL SELECT unnest(notify) FROM approvals.events
        """;

    private static readonly DateTimeOffset Midnight = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task APR_REQ_015_AC5_ErasureReplacesEveryReferenceWithOnePseudonymClearsThePersonsReasonsAndASecondCallAnswersZero()
    {
        ClockFake clock = new(Midnight);
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: clock);
        await NameBAsync(host);
        (int naming, int reasons, int others) = await CountsAsync(host, B);

        Result<ErasureReceipt> erased = await EraseAsync(host, B);
        Result<ErasureReceipt> again = await EraseAsync(host, B);

        Assert.True(erased.IsSuccess, erased.Failure?.Code);
        Assert.Equal(naming, erased.Value!.Changed);
        Assert.True(naming >= 10 && reasons >= 2 && others == 2, $"The person was named in {naming} rows, with {reasons} reasons of theirs and {others} of others.");
        Assert.Equal([new TypeVersionReference { Type = Key, Version = 1 }], erased.Value.KeptVersions);
        Assert.Equal((0, 0, others), await CountsAsync(host, B));
        string pseudonym = Assert.Single(await PseudonymsAsync(host));
        Assert.Matches("^erased:[A-Za-z0-9_-]{22}$", pseudonym);
        Assert.Equal((naming, 0, others), await CountsAsync(host, pseudonym));
        Assert.True(again.IsSuccess, again.Failure?.Code);
        Assert.Equal(0, again.Value!.Changed);
        Assert.Single(await PseudonymsAsync(host));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_015_AC6_ErasureOfTheRequesterSubjectAssigneeOrDelegateOfAPendingRequestIsRefusedWithErasurePending()
    {
        ClockFake clock = new(Midnight);
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: clock);
        await host.PublishAsync(Type(Step("review", ApprovalMode.AnyOne, Person(A))));
        await host.DelegatedAsync(new DelegationDefinition { From = A, To = C, Starts = Midnight, Ends = Midnight + TimeSpan.FromDays(10), CreatedBy = A });
        RequestId pending = await host.CreatedAsync(Request(subject: Subject));

        string?[] refused = [.. await Task.WhenAll(new[] { Requester, Subject, A, C }.Select(async person => (await EraseAsync(host, person)).Failure?.Code))];
        Result<ErasureReceipt> uninvolved = await EraseAsync(host, B);
        await host.CancelAsync(pending, Requester);
        Result<ErasureReceipt> closed = await EraseAsync(host, Requester);

        Assert.All(refused, code => Assert.Equal("approvals.erasure.pending", code));
        Assert.Equal((true, 0), (uninvolved.IsSuccess, uninvolved.Value?.Changed));
        Assert.True(closed.IsSuccess, closed.Failure?.Code);
        Assert.True(closed.Value!.Changed > 0);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_015_AC6_ErasureOfAPersonTheNewestVersionOfATypeNamesIsRefusedWithErasureTypeVersionAndTheOlderVersionsAreAnsweredOnceItNoLongerDoes()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        ApprovalTypeDefinition first = Type(Step("review", ApprovalMode.AnyOne, Person(A)) with { Deadline = new Deadline { After = TimeSpan.FromHours(1), Action = DeadlineAction.Escalate, EscalateTo = Person(B) } });
        await host.PublishAsync(first);

        Failure?[] refused = [.. await Task.WhenAll(new[] { A, B, StepFallback, TypeFallback, Watcher }.Select(async person => (await EraseAsync(host, person)).Failure))];
        await host.PublishAsync(Type(Step("review", ApprovalMode.AnyOne, Person(C))));
        Result<ErasureReceipt> erased = await EraseAsync(host, A);

        Assert.All(refused, failure => Assert.Equal(("approvals.erasure.typeversion", Key), (failure?.Code, failure?.Details["type"])));
        Assert.True(erased.IsSuccess, erased.Failure?.Code);
        Assert.Equal(0, erased.Value!.Changed);
        Assert.Equal([new TypeVersionReference { Type = Key, Version = 1 }], erased.Value.KeptVersions);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_015_AC6_ErasureWithoutTheRetentionGrantIsRefusedWithRetentionNotGranted()
    {
        await using RequestHost host = await RetentionTests.ApplicationHostAsync(postgres, configuration: null, TimeProvider.System, retention: false);

        Result<ErasureReceipt> erased = await EraseAsync(host, B);

        Assert.Equal("approvals.retention.notgranted", erased.Failure?.Code);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_015_AC7_ExportOfAnErasedReferenceOrOfThePseudonymThatReplacedItIsEmpty()
    {
        ClockFake clock = new(Midnight);
        await using RequestHost host = await RetentionTests.ApplicationHostAsync(postgres, configuration: null, clock, retention: true);
        await NameBAsync(host);
        PersonExport before = await host.ExportAsync(B);

        Result<ErasureReceipt> erased = await EraseAsync(host, B);
        PersonExport after = await host.ExportAsync(B);
        PersonExport pseudonym = await host.ExportAsync(Assert.Single(await PseudonymsAsync(host)));

        Assert.True(erased.IsSuccess, erased.Failure?.Code);
        Assert.NotEmpty(before.Requests.Concat<object>(before.Assignments).Concat(before.Decisions).Concat(before.Delegations));
        Assert.Equal((0, 0, 0, 0), (after.Requests.Count, after.Assignments.Count, after.Decisions.Count, after.Delegations.Count));
        Assert.Equal((0, 0, 0, 0), (pseudonym.Requests.Count, pseudonym.Assignments.Count, pseudonym.Decisions.Count, pseudonym.Delegations.Count));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_015_AC10_ErasureRacingACreateNamingThePersonEndsWithOneAfterTheOtherNeverADeadlock()
    {
        ClockFake clock = new(Midnight);
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: clock);
        RequestId made = await NameBAsync(host);
        await using NpgsqlConnection holder = new(host.Connection);
        await holder.OpenAsync(TestContext.Current.CancellationToken);
        long deadlocksBefore = await DeadlocksAsync(holder);
        await using NpgsqlTransaction held = await holder.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await using (NpgsqlCommand take = new("SELECT pg_advisory_xact_lock(@lock)", holder, held))
        {
            take.Parameters.AddWithValue("lock", Themis.Requests.PersonLock.Key(B));
            await take.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        Task<Result<ErasureReceipt>> erase = EraseAsync(host, B);
        Task<Result<CreatedRequest>> create = host.CreateAsync(On("order-made") with { Requester = B, Previous = made });
        await host.WaitForLockWaitersAsync(2);
        await held.CommitAsync(TestContext.Current.CancellationToken);
        Result<ErasureReceipt> erased = await erase;
        Result<CreatedRequest> created = await create;
        await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.True(created.IsSuccess, created.Failure?.Code);
        Assert.True(erased.IsSuccess || erased.Failure?.Code == "approvals.erasure.pending", erased.Failure?.Code);
        RequestRecord record = await host.ReadAsync(created.Value!.Request);
        Assert.Equal(B, record.Requester);
        Assert.Equal(deadlocksBefore, await DeadlocksAsync(holder));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task EraseAsync_ChangingRows_CountsOneErasureAndNoneForAnAnswerOfZero()
    {
        ClockFake clock = new(Midnight);
        long counted = 0;
        Meter? own = null;
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, subscribed) =>
        {
            if (instrument.Name == "approvals.erasures")
            {
                subscribed.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, _, _) =>
        {
            if (ReferenceEquals(instrument.Meter, Volatile.Read(ref own)))
            {
                Interlocked.Add(ref counted, value);
            }
        });
        listener.Start();
        await using RequestHost host = await RequestHost.StartAsync(postgres, clock: clock);
        Volatile.Write(ref own, host.Services.GetRequiredService<ApprovalsMetrics>().Erasures.Meter);
        await NameBAsync(host);

        await EraseAsync(host, B);
        await EraseAsync(host, B);

        Assert.Equal(1, Interlocked.Read(ref counted));
        host.AssertNoLogHoldsAValueUsed();
    }

    private static Task<Result<ErasureReceipt>> EraseAsync(RequestHost host, string person)
    {
        return host.CallAsync<IRecordRetention, Result<ErasureReceipt>>(retention => retention.EraseAsync(person, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Records <see cref="Requests.B"/> in every place a person is named: as a requester who
    /// cancelled with a reason, a subject, an approver of version 1 by reference, a decider who
    /// rejected with a reason, a person decided for through a delegation, a delegation's lender,
    /// its ender and an event's notified; then publishes version 2, which names them no more,
    /// and answers the request they made.
    /// </summary>
    private static async Task<RequestId> NameBAsync(RequestHost host)
    {
        await host.PublishAsync(Type(Step("review", ApprovalMode.AnyOne, Person(A), Person(B))));
        DelegationId lent = await host.DelegatedAsync(new DelegationDefinition { From = B, To = C, Starts = Midnight, Ends = Midnight + TimeSpan.FromDays(10), CreatedBy = B });
        RequestId made = await host.CreatedAsync(On("order-made") with { Requester = B });
        await host.CancelAsync(made, B, reason: Reason);
        RequestId subject = await host.CreatedAsync(On("order-subject") with { Subject = B });
        await host.DecideAsync(subject, A, DecisionOutcome.Reject, OtherReason);
        RequestId decided = await host.CreatedAsync(On("order-decided"));
        await host.DecideAsync(decided, B, DecisionOutcome.Reject, Reason);
        RequestId forThem = await host.CreatedAsync(On("order-for"));
        await host.DecideAsync(forThem, C, DecisionOutcome.Reject, OtherReason, actingFor: B);
        await host.EndDelegationAsync(lent, B);
        await host.PublishAsync(Type(Step("review", ApprovalMode.AnyOne, Person(A), Person(C))));
        return made;
    }

    private static ApprovalRequest On(string target)
    {
        return Request() with { Target = new Target { Kind = "order", Id = target } };
    }

    /// <summary>
    /// The rows that name <paramref name="person"/>, each counted once, as an erasure counts them;
    /// the reasons of the decisions they made and of the requests they closed that are not
    /// cleared; and the decisions whose reason is <see cref="OtherReason"/>.
    /// </summary>
    private static async Task<(int Naming, int Reasons, int Others)> CountsAsync(RequestHost host, string person)
    {
        await using NpgsqlConnection database = new(host.Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand count = new(
            """
            SELECT ((SELECT count(*) FROM approvals.requests WHERE @value IN (requester, subject, closed_by, closed_for))
                + (SELECT count(*) FROM approvals.assignments WHERE person = @value OR via = @via)
                + (SELECT count(*) FROM approvals.decisions WHERE @value IN (decider, acting_for))
                + (SELECT count(*) FROM approvals.delegations WHERE @value IN (from_person, to_person, created_by, ended_by))
                + (SELECT count(*) FROM approvals.events WHERE @value IN (actor, acting_for) OR @value = ANY(notify)))::int,
                ((SELECT count(*) FROM approvals.decisions WHERE decider = @value AND reason IS NOT NULL)
                + (SELECT count(*) FROM approvals.requests WHERE closed_by = @value AND reason IS NOT NULL))::int,
                (SELECT count(*) FROM approvals.decisions WHERE reason = @other)::int
            """,
            database);
        count.Parameters.AddWithValue("value", person);
        count.Parameters.AddWithValue("via", CanonicalForm.WriteApprover(new ApproverReference.Person(person)));
        count.Parameters.AddWithValue("other", OtherReason);
        await using NpgsqlDataReader reader = await count.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        await reader.ReadAsync(TestContext.Current.CancellationToken);
        return (reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2));
    }

    /// <summary>The distinct pseudonyms the record holds.</summary>
    private static async Task<List<string>> PseudonymsAsync(RequestHost host)
    {
        await using NpgsqlConnection database = new(host.Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand read = new($"SELECT DISTINCT value FROM ({PersonValues}) AS named WHERE value LIKE 'erased:%' ORDER BY value", database);
        await using NpgsqlDataReader reader = await read.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        List<string> found = [];
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            found.Add(reader.GetString(0));
        }

        return found;
    }
    /// <summary>The deadlocks the server has counted in the database of <paramref name="database"/>, read afresh.</summary>
    private static async Task<long> DeadlocksAsync(NpgsqlConnection database)
    {
        await using NpgsqlCommand read = new("SELECT pg_stat_clear_snapshot(); SELECT deadlocks FROM pg_stat_database WHERE datname = current_database()", database);
        await using NpgsqlDataReader reader = await read.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        await reader.NextResultAsync(TestContext.Current.CancellationToken);
        await reader.ReadAsync(TestContext.Current.CancellationToken);
        return reader.GetInt64(0);
    }
}