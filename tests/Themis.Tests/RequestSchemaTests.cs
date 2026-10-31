using System.Threading.Tasks;

using Npgsql;

using Xunit;
using Xunit.Sdk;

namespace Themis.Tests;

/// <summary>The tables of requests, steps, assignments, decisions, events and jobs, as the migrations make them, each test on a database of its own.</summary>
[Trait("Category", "integration")]
public sealed class RequestSchemaTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Version = """
        INSERT INTO approvals.types (key, retired) VALUES ('purchase', false);
        INSERT INTO approvals.versions (key, version, definition, hash, published_at) VALUES ('purchase', 1, '{}', sha256('{}'), '2026-01-01T00:00:00Z');
        """;

    [Fact]
    public async Task APR_OPS_003_AC3_StatusOutsideItsClosedSetIsRefusedByTheDatabase()
    {
        string refused = await RefusedAsync(Request("018f0000-0000-7000-8000-000000000001", "open"));

        Assert.Matches($"{PostgresErrorCodes.CheckViolation}: .*\"ck_requests_status\"", refused);
    }

    [Fact]
    public async Task Steps_StatusOutsideItsClosedSet_IsRefusedByTheDatabase()
    {
        string refused = await RefusedAsync(
            Request("018f0000-0000-7000-8000-000000000001", "pending")
            + "INSERT INTO approvals.steps (request, number, name, mode, status) VALUES ('018f0000-0000-7000-8000-000000000001', 1, 'review', 'anyone', 'started');");

        Assert.Matches($"{PostgresErrorCodes.CheckViolation}: .*\"ck_steps_status\"", refused);
    }

    [Fact]
    public async Task Requests_TwoOpenOfOneTypeAndTarget_AreRefusedByTheDatabase()
    {
        string refused = await RefusedAsync(Request("018f0000-0000-7000-8000-000000000001", "pending") + Request("018f0000-0000-7000-8000-000000000002", "approved"));

        Assert.Matches($"{PostgresErrorCodes.UniqueViolation}: .*\"ux_requests_open\"", refused);
    }

    [Fact]
    public async Task Requests_OneOpenAndOneClosedOfOneTypeAndTarget_AreAccepted()
    {
        (string name, _) = await postgres.NewMigratedDatabaseAsync();

        await postgres.RunAsync(Version + Request("018f0000-0000-7000-8000-000000000001", "rejected") + Request("018f0000-0000-7000-8000-000000000002", "pending"), name);
    }

    [Fact]
    public async Task Jobs_EachJob_HasItsRow()
    {
        (_, string connection) = await postgres.NewMigratedDatabaseAsync();
        await using NpgsqlConnection database = new(connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand names = new("SELECT string_agg(name, ',' ORDER BY name) FROM approvals.jobs", database);

        Assert.Equal("deadlines,observer,watch", await names.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    private static string Request(string id, string status)
    {
        return $"""
            INSERT INTO approvals.requests (id, type, version, target_kind, target_id, snapshot, requester, requester_roles, subject_roles, status, created_at, sequence)
            VALUES ('{id}', 'purchase', 1, 'order', 'order-1', '{"{}"}', 'person-1', '{"{}"}', '{"{}"}', '{status}', '2026-01-01T00:00:00Z', 0);
            """;
    }

    /// <summary>Runs <paramref name="script"/> after a type's version on a new migrated database, and answers the error <c>psql</c> writes for it, its SQLSTATE included.</summary>
    private async Task<string> RefusedAsync(string script)
    {
        (string name, _) = await postgres.NewMigratedDatabaseAsync();

        return (await Assert.ThrowsAsync<TrueException>(() => postgres.RunAsync("\\set VERBOSITY verbose\n" + Version + script, name))).Message;
    }
}