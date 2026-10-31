using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Themis.Core;
using Themis.Testing;

using Xunit;

namespace Themis.Tests;

/// <summary>
/// A started host on a migrated database, with the testing package's in-memory directory and
/// recording observer, a recording alert sink and, where given, a clock of the test's, whose
/// services a test calls, capturing every log line so the test can assert that none holds a
/// value it used (<c>04</c> APR-OPS-005).
/// </summary>
internal sealed class RequestHost : IAsyncDisposable
{
    private readonly TestHost host;

    private RequestHost(TestHost host, string connection, LogCapture logs, AlertSinkRecorder alerts)
    {
        this.host = host;
        Connection = connection;
        Logs = logs;
        Alerts = alerts;
        Directory = host.Services.GetRequiredService<InMemoryApproverDirectory>();
        Observer = host.Services.GetRequiredService<RecordingObserver>();
    }

    /// <summary>The superuser's connection string to the database.</summary>
    internal string Connection { get; }

    internal LogCapture Logs { get; }

    /// <summary>The in-memory directory the host's directory answers from.</summary>
    internal InMemoryApproverDirectory Directory { get; }

    internal RecordingObserver Observer { get; }

    internal AlertSinkRecorder Alerts { get; }

    internal IServiceProvider Services => host.Services;

    /// <summary>Starts a host on a new migrated database, as <see cref="StartOnAsync"/> does.</summary>
    internal static async Task<RequestHost> StartAsync(
        PostgresFixture postgres,
        IReadOnlyDictionary<string, string?>? configuration = null,
        Func<IApproverDirectory, IApproverDirectory>? wrap = null,
        TimeProvider? clock = null,
        bool hosted = false)
    {
        (_, string connection) = await postgres.NewMigratedDatabaseAsync();
        return await StartOnAsync(connection, configuration, wrap, clock, hosted);
    }

    /// <summary>
    /// Starts a host on the migrated database of <paramref name="connection"/>, with
    /// <paramref name="configuration"/>, its directory the in-memory one or what
    /// <paramref name="wrap"/> makes of it, on <paramref name="clock"/> where given; through
    /// <c>AddThemisTesting</c>, so no hosted job runs, unless <paramref name="hosted"/>.
    /// </summary>
    internal static async Task<RequestHost> StartOnAsync(
        string connection,
        IReadOnlyDictionary<string, string?>? configuration = null,
        Func<IApproverDirectory, IApproverDirectory>? wrap = null,
        TimeProvider? clock = null,
        bool hosted = false)
    {
        LogCapture logs = new(everyCategory: true);
        AlertSinkRecorder alerts = new();
        SecretVersions secret = new() { Versions = [new SecretVersion { Version = 1, Value = Encoding.UTF8.GetBytes(connection) }] };
        TestHost host = TestHost.Build(
            configuration,
            before: services =>
            {
                if (clock is not null)
                {
                    services.AddSingleton(clock);
                }

                services.AddSingleton<IAlertSink>(alerts);
                if (hosted)
                {
                    services.AddSingleton<ISecretSource>(SecretSourceFake.WithConnection(connection));
                    services.AddSingleton(new InMemoryApproverDirectory());
                    services.AddSingleton<IApproverDirectory>(provider => provider.GetRequiredService<InMemoryApproverDirectory>());
                    services.AddSingleton(new RecordingObserver());
                    services.AddSingleton<IApprovalObserver>(provider => provider.GetRequiredService<RecordingObserver>());
                }
                else
                {
                    services.AddThemisTesting(secrets => secrets.SetSecret("connection", secret));
                }
            },
            after: wrap is null ? null : services => services.AddSingleton(provider => wrap(provider.GetRequiredService<InMemoryApproverDirectory>())),
            logs: logs);
        await host.StartAsync();
        host.Services.GetRequiredService<IApproverDirectory>();
        return new RequestHost(host, connection, logs, alerts);
    }

    /// <summary>Runs one run of <paramref name="job"/> through <see cref="IApprovalJobs"/>.</summary>
    internal Task<int> RunAsync(ApprovalJob job)
    {
        return host.Services.GetRequiredService<IApprovalJobs>().RunAsync(job, TestContext.Current.CancellationToken).AsTask();
    }

    /// <summary>Runs <paramref name="call"/> on a service of a scope of its own, as a host's request would.</summary>
    internal async Task<T> CallAsync<TService, T>(Func<TService, ValueTask<T>> call)
        where TService : notnull
    {
        AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using (scope)
        {
            return await call(scope.ServiceProvider.GetRequiredService<TService>());
        }
    }

    internal async Task<PublishedVersion> PublishAsync(ApprovalTypeDefinition definition)
    {
        Result<PublishedVersion> published = await CallAsync<IApprovalTypes, Result<PublishedVersion>>(types => types.PublishAsync(definition, TestContext.Current.CancellationToken));
        return published.Value ?? throw new InvalidOperationException($"The publish was refused: {published.Failure?.Code} {string.Join(',', published.Failure?.Details.Values ?? [])}");
    }

    internal Task<Result> RetireAsync(string key)
    {
        return CallAsync<IApprovalTypes, Result>(types => types.RetireAsync(key, TestContext.Current.CancellationToken));
    }

    internal Task<Result<CreatedRequest>> CreateAsync(ApprovalRequest request)
    {
        return CallAsync<IApprovals, Result<CreatedRequest>>(approvals => approvals.CreateAsync(request, TestContext.Current.CancellationToken));
    }

    /// <summary>Creates <paramref name="request"/>, which must be recorded, and answers its identifier.</summary>
    internal async Task<RequestId> CreatedAsync(ApprovalRequest request)
    {
        Result<CreatedRequest> created = await CreateAsync(request);
        return created.Value?.Request ?? throw new InvalidOperationException($"The create was refused: {created.Failure?.Code}");
    }

    internal Task<Result<DecisionReceipt>> DecideAsync(RequestId request, string decider, DecisionOutcome outcome = DecisionOutcome.Approve, string? reason = null, string? actingFor = null)
    {
        DecisionRequest decision = new() { Request = request, Decider = decider, Outcome = outcome, Reason = reason, ActingFor = actingFor };
        return CallAsync<IApprovals, Result<DecisionReceipt>>(approvals => approvals.DecideAsync(decision, TestContext.Current.CancellationToken));
    }

    internal Task<Result> CancelAsync(RequestId request, string actor, string? onBehalfOf = null, string? reason = null)
    {
        CancelRequest cancel = new() { Request = request, Actor = actor, OnBehalfOf = onBehalfOf, Reason = reason };
        return CallAsync<IApprovals, Result>(approvals => approvals.CancelAsync(cancel, TestContext.Current.CancellationToken));
    }

    internal Task<Result<CreatedRequest>> CreateAsync(ApprovalRequest request, DbTransaction transaction)
    {
        return CallAsync<IApprovals, Result<CreatedRequest>>(approvals => approvals.CreateAsync(request, transaction, TestContext.Current.CancellationToken));
    }

    internal Task<Result> CancelAsync(CancelRequest cancel, DbTransaction transaction)
    {
        return CallAsync<IApprovals, Result>(approvals => approvals.CancelAsync(cancel, transaction, TestContext.Current.CancellationToken));
    }

    internal Task<Result<DelegationId>> DelegateAsync(DelegationDefinition delegation)
    {
        return CallAsync<IDelegations, Result<DelegationId>>(delegations => delegations.CreateAsync(delegation, TestContext.Current.CancellationToken));
    }

    /// <summary>Records <paramref name="delegation"/>, which must be recorded, and answers its identifier.</summary>
    internal async Task<DelegationId> DelegatedAsync(DelegationDefinition delegation)
    {
        Result<DelegationId> created = await DelegateAsync(delegation);
        return created.IsSuccess ? created.Value : throw new InvalidOperationException($"The delegation was refused: {created.Failure?.Code}");
    }

    internal Task<Result> EndDelegationAsync(DelegationId delegation, string actor)
    {
        return CallAsync<IDelegations, Result>(delegations => delegations.EndAsync(delegation, actor, TestContext.Current.CancellationToken));
    }

    internal Task<IReadOnlyList<DelegationRecord>> DelegationsAsync(string person)
    {
        return CallAsync<IDelegations, IReadOnlyList<DelegationRecord>>(delegations => delegations.ListAsync(person, TestContext.Current.CancellationToken));
    }

    /// <summary>The instant and actor of the delegation's ending as the database holds them, or none where it was not ended.</summary>
    internal async Task<(DateTimeOffset? At, string? By)> DelegationEndAsync(DelegationId delegation)
    {
        await using NpgsqlConnection database = new(Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand select = new("SELECT ended_at, ended_by FROM approvals.delegations WHERE id = @delegation", database);
        select.Parameters.AddWithValue("delegation", delegation.Value);
        await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        await reader.ReadAsync(TestContext.Current.CancellationToken);
        return (
            await reader.IsDBNullAsync(0, TestContext.Current.CancellationToken) ? null : await reader.GetFieldValueAsync<DateTimeOffset>(0, TestContext.Current.CancellationToken),
            await reader.IsDBNullAsync(1, TestContext.Current.CancellationToken) ? null : reader.GetString(1));
    }

    /// <summary>Opens a connection of the host's own to the database, with the table <c>host_orders</c> it writes beside the library.</summary>
    internal async Task<NpgsqlConnection> OpenHostAsync()
    {
        NpgsqlConnection database = new(Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand table = new("CREATE TABLE IF NOT EXISTS public.host_orders (id text PRIMARY KEY)", database);
        await table.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        return database;
    }

    /// <summary>The number of rows of the host's table <c>host_orders</c>.</summary>
    internal async Task<long> HostRowsAsync()
    {
        await using NpgsqlConnection database = await OpenHostAsync();
        await using NpgsqlCommand count = new("SELECT count(*) FROM public.host_orders", database);
        return (long)(await count.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    /// <summary>Opens a transaction that holds the request's row locked, <c>FOR UPDATE</c>, until it ends.</summary>
    internal static async Task<NpgsqlTransaction> LockAsync(NpgsqlConnection database, RequestId request)
    {
        NpgsqlTransaction transaction = await database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand select = new("SELECT id FROM approvals.requests WHERE id = @request FOR UPDATE", database, transaction);
        select.Parameters.AddWithValue("request", request.Value);
        await select.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        return transaction;
    }

    /// <summary>Waits until <paramref name="count"/> sessions on the database wait for a lock.</summary>
    internal async Task WaitForLockWaitersAsync(int count)
    {
        await using NpgsqlConnection database = new(Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        for (int round = 0; round < 400; round++)
        {
            await using NpgsqlCommand waiting = new("SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock'", database);
            if ((long)(await waiting.ExecuteScalarAsync(TestContext.Current.CancellationToken))! >= count)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"Fewer than {count} sessions waited for a lock.");
    }

    /// <summary>Reads the request's record, which must exist.</summary>
    internal async Task<RequestRecord> ReadAsync(RequestId request)
    {
        Result<RequestRecord> read = await CallAsync<IApprovals, Result<RequestRecord>>(approvals => approvals.ReadAsync(request, TestContext.Current.CancellationToken));
        return read.Value ?? throw new InvalidOperationException($"The read was refused: {read.Failure?.Code}");
    }

    internal Task<Result<PendingPage>> PendingAsync(string person, string? cursor = null, int? size = null)
    {
        return CallAsync<IApprovals, Result<PendingPage>>(approvals => approvals.PendingAsync(person, cursor, size, TestContext.Current.CancellationToken));
    }

    /// <summary>The events the database holds, in the order of their requests' creation and their sequence.</summary>
    internal async Task<IReadOnlyList<RecordedEvent>> EventsAsync(RequestId request)
    {
        await using NpgsqlConnection database = new(Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand select = new("SELECT kind, sequence, step, actor, acting_for, reason_code, notify FROM approvals.events WHERE request = @request ORDER BY sequence", database);
        select.Parameters.AddWithValue("request", request.Value);
        await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        List<RecordedEvent> events = [];
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            events.Add(new RecordedEvent(
                Spelling<EventKind>.Read(reader.GetString(0)),
                reader.GetInt64(1),
                await reader.IsDBNullAsync(2, TestContext.Current.CancellationToken) ? null : reader.GetInt32(2),
                await reader.IsDBNullAsync(3, TestContext.Current.CancellationToken) ? null : reader.GetString(3),
                await reader.IsDBNullAsync(4, TestContext.Current.CancellationToken) ? null : reader.GetString(4),
                await reader.IsDBNullAsync(5, TestContext.Current.CancellationToken) ? null : Spelling<ReasonCode>.Read(reader.GetString(5)),
                (string[])reader.GetValue(6)));
        }

        return events;
    }

    /// <summary>The request of the last occurrence of <c>request-unassignable</c> for <paramref name="type"/>, and the step, or none where none occurred.</summary>
    internal async Task<(Guid Request, int Step)?> UnassignableAsync(string type)
    {
        await using NpgsqlConnection database = new(Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand select = new("SELECT request, step FROM approvals.alerts WHERE condition = 'request-unassignable' AND scope = @type", database);
        select.Parameters.AddWithValue("type", type);
        await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        return await reader.ReadAsync(TestContext.Current.CancellationToken) ? (reader.GetGuid(0), reader.GetInt32(1)) : null;
    }

    /// <summary>The number of rows of each of the request tables, as one text: requests, steps, assignments, decisions, events.</summary>
    internal async Task<string> CountsAsync()
    {
        await using NpgsqlConnection database = new(Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand count = new(
            """
            SELECT concat_ws(',', (SELECT count(*) FROM approvals.requests), (SELECT count(*) FROM approvals.steps), (SELECT count(*) FROM approvals.assignments),
                (SELECT count(*) FROM approvals.decisions), (SELECT count(*) FROM approvals.events))
            """,
            database);
        return (string)(await count.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    /// <summary>Asserts that no log line holds a person reference, role, target or value of <see cref="Requests"/> or <see cref="Definitions"/> (<c>04</c> APR-OPS-005).</summary>
    internal void AssertNoLogHoldsAValueUsed()
    {
        string used = string.Join('|', Requests.Unloggable.Concat(Definitions.Unloggable).Select(Regex.Escape));
        Assert.All(Logs.Entries, entry => Assert.DoesNotMatch(used, entry.Message));
    }

    public async ValueTask DisposeAsync()
    {
        await host.DisposeAsync();
        Logs.Dispose();
    }
}

/// <summary>An event as the database holds it.</summary>
internal sealed record RecordedEvent(EventKind Kind, long Sequence, int? Step, string? Actor, string? ActingFor, ReasonCode? ReasonCode, string[] Notify);