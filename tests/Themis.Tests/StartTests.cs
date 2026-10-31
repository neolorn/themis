using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Npgsql;

using Themis.Core;
using Themis.Operations;

using Xunit;

namespace Themis.Tests;

/// <summary>Starts against a real database in a container, each test on a database of its own.</summary>
[Trait("Category", "integration")]
public sealed class StartTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task APR_LIB_003_AC1_HostWithValidConfigurationAndItsSeamsStartsWithTheOneCall()
    {
        (_, string connection) = await postgres.NewMigratedDatabaseAsync();
        await using TestHost host = TestHost.Build(before: TestHost.Seams(SecretSourceFake.WithConnection(connection)));

        await host.StartAsync();

        Assert.True(host.Services.GetRequiredService<ApprovalsReady>().IsReady);
    }

    [Fact]
    public async Task APR_LIB_003_AC3_HostedServiceRegisteredBeforeAddThemisThatCreatesARequestInItsStartAsyncRecordsItOnlyAfterTheStartupChecksRan()
    {
        (_, string connection) = await postgres.NewMigratedDatabaseAsync();
        using LogCapture logs = new(everyCategory: true);
        CreatingService creating = new();
        await using TestHost host = TestHost.Build(
            before: services =>
            {
                services.AddSingleton<IHostedService>(provider => creating.With(provider));
                TestHost.Seams(SecretSourceFake.WithConnection(connection))(services);
            },
            logs: logs);

        await host.StartAsync();

        Assert.Equal(RequestStatus.Pending, creating.Created?.Value?.Status);
        List<string> messages = [.. logs.Entries.Select(entry => entry.Message)];
        int checkedAt = messages.FindIndex(message => message == "The library's startup checks passed.");
        int recordedAt = messages.FindIndex(message => message.StartsWith("Request ", StringComparison.Ordinal) && message.Contains(" recorded, pending.", StringComparison.Ordinal));
        Assert.True(checkedAt >= 0 && recordedAt > checkedAt, $"The startup checks passed at log line {checkedAt} and the request was recorded at {recordedAt}.");
        string used = string.Join('|', Requests.Unloggable.Concat(Definitions.Unloggable).Select(System.Text.RegularExpressions.Regex.Escape));
        Assert.All(logs.Entries, entry => Assert.DoesNotMatch(used, entry.Message));
    }

    [Fact]
    public async Task APR_OPS_002_AC1_DatabaseMissingTheReleasesLastMigrationStopsTheStart()
    {
        (string name, string connection) = await postgres.NewMigratedDatabaseAsync();
        string last = SchemaCheck.Migrations[^1];
        await postgres.RunAsync($"DELETE FROM approvals.__migrations_history WHERE \"MigrationId\" = '{last}';", name);
        await using TestHost host = TestHost.Build(before: TestHost.Seams(SecretSourceFake.WithConnection(connection)));

        ApprovalsStartupException failure = await host.StartFailureAsync();

        Assert.Equal("approvals.startup.schemamismatch", failure.Code);
        Assert.Equal(new Dictionary<string, string> { ["migration"] = last }, failure.Details);
        Assert.False(host.Services.GetRequiredService<ApprovalsReady>().IsReady);
    }

    [Fact]
    public async Task Start_EmptyDatabase_StopsNamingTheFirstMigration()
    {
        (_, string connection) = await postgres.NewDatabaseAsync();
        await using TestHost host = TestHost.Build(before: TestHost.Seams(SecretSourceFake.WithConnection(connection)));

        ApprovalsStartupException failure = await host.StartFailureAsync();

        Assert.Equal(("approvals.startup.schemamismatch", SchemaCheck.Migrations[0]), (failure.Code, failure.Details["migration"]));
    }

    [Fact]
    public async Task Start_MigrationOfALaterRelease_IsAccepted()
    {
        (string name, string connection) = await postgres.NewMigratedDatabaseAsync();
        await postgres.RunAsync("INSERT INTO approvals.__migrations_history VALUES ('29991231000000_Later', '99.0.0');", name);
        await using TestHost host = TestHost.Build(before: TestHost.Seams(SecretSourceFake.WithConnection(connection)));

        await host.StartAsync();

        Assert.True(host.Services.GetRequiredService<ApprovalsReady>().IsReady);
    }

    [Fact]
    public async Task APR_OPS_001_AC2_KeyChangedWhileTheApplicationRunsTakesEffectOnlyAfterARestart()
    {
        (_, string connection) = await postgres.NewMigratedDatabaseAsync();
        await using TestHost running = TestHost.Build(new Dictionary<string, string?> { ["Themis:Timeout"] = "00:00:07" }, before: TestHost.Seams(SecretSourceFake.WithConnection(connection)));
        await running.StartAsync();

        running.Configuration["Themis:Timeout"] = "00:00:09";
        running.Configuration.Reload();

        Assert.Equal(TimeSpan.FromSeconds(7), running.Services.GetRequiredService<IOptions<ApprovalsOptions>>().Value.Timeout);
        await using TestHost restarted = TestHost.Build(running.Configuration, before: TestHost.Seams(SecretSourceFake.WithConnection(connection)));
        await restarted.StartAsync();
        Assert.Equal(TimeSpan.FromSeconds(9), restarted.Services.GetRequiredService<IOptions<ApprovalsOptions>>().Value.Timeout);
    }

    [Fact]
    public async Task APR_OPS_008_AC2_NoLogEntryOfAStartOrOfItsSchemaFailureHoldsTheConnectionString()
    {
        (string name, string connection) = await postgres.NewMigratedDatabaseAsync();
        string password = Guid.NewGuid().ToString("N");
        string role = "app_" + password[..12];
        await postgres.RunAsync($"CREATE ROLE {role} LOGIN PASSWORD '{password}'; GRANT USAGE ON SCHEMA approvals TO {role}; GRANT SELECT ON approvals.__migrations_history TO {role};", name);
        string own = new NpgsqlConnectionStringBuilder(connection) { Username = role, Password = password }.ConnectionString;
        using LogCapture logs = new(everyCategory: true);
        await using (TestHost started = TestHost.Build(before: TestHost.Seams(SecretSourceFake.WithConnection(own)), logs: logs))
        {
            await started.StartAsync();
        }

        await postgres.RunAsync($"DELETE FROM approvals.__migrations_history;", name);
        await using (TestHost failed = TestHost.Build(before: TestHost.Seams(SecretSourceFake.WithConnection(own)), logs: logs))
        {
            await failed.StartFailureAsync();
        }

        Assert.Contains(logs.Entries, entry => entry.Message.Contains("approvals.startup.schemamismatch", StringComparison.Ordinal));
        Assert.All(logs.Entries, entry => Assert.DoesNotMatch($"{password}|{role}|{name}", entry.Message));
    }

    [Fact]
    public async Task APR_OVR_002_AC2_EveryTableTheLibraryUsesLivesInItsOwnSchema()
    {
        (_, string connection) = await postgres.NewMigratedDatabaseAsync();

        await using NpgsqlConnection database = new(connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand tables = new(
            """
            SELECT n.nspname, c.relname FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relkind IN ('r', 'p', 'v', 'm', 'S') AND n.nspname NOT IN ('pg_catalog', 'information_schema') AND n.nspname NOT LIKE 'pg_toast%'
            """,
            database);
        List<(string Schema, string Table)> found = [];
        await using (NpgsqlDataReader reader = await tables.ExecuteReaderAsync(TestContext.Current.CancellationToken))
        {
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                found.Add((reader.GetString(0), reader.GetString(1)));
            }
        }

        Assert.Contains(("approvals", "__migrations_history"), found);
        Assert.Contains(("approvals", "alerts"), found);
        Assert.Contains(("approvals", "types"), found);
        Assert.Contains(("approvals", "versions"), found);
        Assert.Contains(("approvals", "requests"), found);
        Assert.Contains(("approvals", "steps"), found);
        Assert.Contains(("approvals", "assignments"), found);
        Assert.Contains(("approvals", "decisions"), found);
        Assert.Contains(("approvals", "events"), found);
        Assert.Contains(("approvals", "jobs"), found);
        Assert.All(found, table => Assert.Equal("approvals", table.Schema));
    }

    [Fact]
    public async Task Alerts_ConditionOutsideItsClosedSet_IsRefusedByTheDatabase()
    {
        (_, string connection) = await postgres.NewMigratedDatabaseAsync();

        await using NpgsqlConnection database = new(connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand insert = new("INSERT INTO approvals.alerts (condition, scope, holding) VALUES ('disk-full', '', true)", database);

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

        Assert.Equal((PostgresErrorCodes.CheckViolation, "ck_alerts_condition"), (refused.SqlState, refused.ConstraintName));
        Assert.Equal(3, AlertState.Conditions.Count);
        Assert.Equal(Reference.FirstColumn(5), AlertState.Conditions.ToList());
    }
}