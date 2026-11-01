using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Npgsql;

using Xunit;

namespace Themis.Cli.Tests;

[Trait("Category", "integration")]
public sealed class MigrateTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string History = "__migrations_history";

    [Fact]
    public async Task APR_OPS_002_AC2_MigrateTwiceAppliesNothingTheSecondTimeAndExitsZeroBothTimes()
    {
        (_, string connection) = await postgres.NewDatabaseAsync();

        Tool.ToolRun first = await Tool.RunAsync(Tool.Input(connection));
        IReadOnlyList<string> afterFirst = await HistoryAsync(connection);
        Tool.ToolRun second = await Tool.RunAsync(Tool.Input(connection));
        IReadOnlyList<string> afterSecond = await HistoryAsync(connection);

        Assert.Equal((0, string.Empty), (first.Exit, first.Error));
        Assert.Equal((0, string.Empty), (second.Exit, second.Error));
        Assert.NotEmpty(afterFirst);
        Assert.Equal(afterFirst, afterSecond);
    }

    [Fact]
    public async Task APR_OPS_002_AC3_RoleNamedInApplicationRolesCanReadInsertAndUpdateEveryTableButTheHistoryWhichItCanOnlyReadAndDeleteFromNone()
    {
        (_, string connection) = await postgres.NewDatabaseAsync();
        string role = NewName("app");
        await postgres.RunAsync($"CREATE ROLE {role} LOGIN PASSWORD 'test';");

        Tool.ToolRun run = await Tool.RunAsync(Tool.Input(connection, role));

        Assert.Equal((0, string.Empty), (run.Exit, run.Error));
        await using NpgsqlConnection database = new(new NpgsqlConnectionStringBuilder(connection) { Username = role, Password = "test" }.ConnectionString);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        Dictionary<string, (bool Read, bool Insert, bool Update, bool Delete)> granted = [];
        await using (NpgsqlCommand tables = new(
            """
            SELECT c.relname, has_table_privilege(c.oid, 'SELECT'), has_table_privilege(c.oid, 'INSERT'),
                   has_table_privilege(c.oid, 'UPDATE'), has_table_privilege(c.oid, 'DELETE')
            FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'approvals' AND c.relkind IN ('r', 'p')
            """,
            database))
        {
            await using NpgsqlDataReader reader = await tables.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                granted[reader.GetString(0)] = (reader.GetBoolean(1), reader.GetBoolean(2), reader.GetBoolean(3), reader.GetBoolean(4));
            }
        }

        Assert.Contains("alerts", granted.Keys);
        Assert.Equal((true, false, false, false), granted[History]);
        Assert.All(granted.Where(table => table.Key != History), table => Assert.True(table.Value == (true, true, true, false), $"{table.Key} is not granted as it should be"));
        await using (NpgsqlCommand write = new(
            """
            INSERT INTO approvals.alerts (condition, scope, holding) VALUES ('job-stalled', 'deadlines', true);
            UPDATE approvals.alerts SET holding = false WHERE condition = 'job-stalled';
            SELECT count(*) FROM approvals.alerts;
            SELECT count(*) FROM approvals.__migrations_history;
            """,
            database))
        {
            await write.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await using NpgsqlCommand delete = new("DELETE FROM approvals.alerts", database);
        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(() => delete.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
    }

    [Fact]
    public async Task APR_REQ_015_AC1_ApplicationRoleCannotDeleteOrTruncateTheRowsOfAnyOfTheLibrarysTables()
    {
        (_, string connection) = await postgres.NewDatabaseAsync();
        string role = NewName("app");
        await postgres.RunAsync($"CREATE ROLE {role} LOGIN PASSWORD 'test';");
        Tool.ToolRun run = await Tool.RunAsync(Tool.Input(connection, role));
        Assert.Equal((0, string.Empty), (run.Exit, run.Error));
        await using NpgsqlConnection database = new(new NpgsqlConnectionStringBuilder(connection) { Username = role, Password = "test" }.ConnectionString);
        await database.OpenAsync(TestContext.Current.CancellationToken);

        await using (NpgsqlCommand attempt = new(
            """
            DO $$
            DECLARE
                item record;
                verb text;
                refused text[] := '{}';
                allowed text[] := '{}';
            BEGIN
                FOR item IN SELECT c.relname FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'approvals' AND c.relkind IN ('r', 'p') ORDER BY c.relname LOOP
                    FOREACH verb IN ARRAY ARRAY['DELETE FROM', 'TRUNCATE'] LOOP
                        BEGIN
                            EXECUTE format('%s approvals.%I', verb, item.relname);
                            allowed := allowed || (verb || ' ' || item.relname);
                        EXCEPTION WHEN insufficient_privilege THEN
                            refused := refused || (verb || ' ' || item.relname);
                        END;
                    END LOOP;
                END LOOP;
                PERFORM set_config('themis.refused', array_to_string(refused, ','), false);
                PERFORM set_config('themis.allowed', array_to_string(allowed, ','), false);
            END
            $$;
            """,
            database))
        {
            await attempt.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await using NpgsqlCommand read = new("SELECT current_setting('themis.refused'), current_setting('themis.allowed')", database);
        await using NpgsqlDataReader reader = await read.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        await reader.ReadAsync(TestContext.Current.CancellationToken);
        string[] refused = reader.GetString(0).Split(',');
        Assert.Equal(string.Empty, reader.GetString(1));
        Assert.Contains("DELETE FROM delegations", refused);
        Assert.Contains("TRUNCATE requests", refused);
        Assert.Contains("DELETE FROM __migrations_history", refused);
        Assert.Equal(refused.Length / 2, refused.Count(entry => entry.StartsWith("TRUNCATE ", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task APR_OPS_002_AC4_RoleTheDatabaseLacksExitsOneWithInputInvalidAndAppliesNothing()
    {
        (_, string connection) = await postgres.NewDatabaseAsync();
        string present = NewName("app");
        await postgres.RunAsync($"CREATE ROLE {present};");

        Tool.ToolRun run = await Tool.RunAsync(Tool.Input(connection, present, NewName("missing")));

        AssertFailure(run, "approvals.migrate.inputinvalid", "member", "applicationRoles");
        Assert.Empty(await HistoryAsync(connection));
    }

    [Fact]
    public async Task APR_OPS_002_AC4_NameOf64BytesWhoseFirst63AreARolesNameExitsOneWithInputInvalidAndAppliesNothing()
    {
        (_, string connection) = await postgres.NewDatabaseAsync();
        string role = NewName("app").PadRight(63, 'r');
        await postgres.RunAsync($"CREATE ROLE {role};");

        Tool.ToolRun run = await Tool.RunAsync(Tool.Input(connection, role + "s"));

        AssertFailure(run, "approvals.migrate.inputinvalid", "member", "applicationRoles");
        Assert.Empty(await HistoryAsync(connection));
    }

    [Fact]
    public async Task Migrate_TableOwnedByAnotherRoleThatGrantsSelectWithoutTheRightToGrantIt_ExitsOneWithGrantFailedAndGrantsNothing()
    {
        (string name, string connection, string migrator) = await MigratedByAnOrdinaryRoleAsync();
        string other = NewName("owner");
        string role = NewName("app");
        await postgres.RunAsync(
            $"""
            CREATE ROLE {other};
            CREATE ROLE {role};
            CREATE TABLE approvals.extra (id int);
            ALTER TABLE approvals.extra OWNER TO {other};
            GRANT SELECT ON approvals.extra TO {migrator};
            """,
            name);

        Tool.ToolRun run = await Tool.RunAsync(Tool.Input(connection, role));

        AssertFailure(run, "approvals.migrate.grantfailed", "role", role);
        Assert.False(await HoldsAsync(name, "schema", role, "approvals", "USAGE"));
        Assert.False(await HoldsAsync(name, "table", role, "approvals.alerts", "SELECT"));
        Assert.False(await HoldsAsync(name, "table", role, $"approvals.{History}", "SELECT"));
    }

    [Fact]
    public async Task Migrate_GrantThatFails_ExitsOneWithGrantFailedNamingTheRoleAndGrantsNothing()
    {
        (string name, string connection, _) = await MigratedByAnOrdinaryRoleAsync();
        string other = NewName("owner");
        string role = NewName("app");
        await postgres.RunAsync(
            $"""
            CREATE ROLE {other};
            CREATE ROLE {role};
            CREATE TABLE approvals.extra (id int);
            ALTER TABLE approvals.extra OWNER TO {other};
            """,
            name);

        Tool.ToolRun run = await Tool.RunAsync(Tool.Input(connection, role));

        AssertFailure(run, "approvals.migrate.grantfailed", "role", role);
        Assert.False(await HoldsAsync(name, "schema", role, "approvals", "USAGE"));
    }

    [Fact]
    public async Task Migrate_SequenceOfTheSchema_IsGrantedUsage()
    {
        (string name, string connection) = await postgres.NewDatabaseAsync();
        Assert.Equal(0, (await Tool.RunAsync(Tool.Input(connection))).Exit);
        string role = NewName("app");
        await postgres.RunAsync($"CREATE ROLE {role}; CREATE SEQUENCE approvals.extra_numbers;", name);

        Tool.ToolRun run = await Tool.RunAsync(Tool.Input(connection, role));

        Assert.Equal((0, string.Empty), (run.Exit, run.Error));
        Assert.True(await HoldsAsync(name, "sequence", role, "approvals.extra_numbers", "USAGE"));
    }

    [Fact]
    public async Task Migrate_PrivilegeARoleAlreadyHolds_IsNotRevoked()
    {
        (string name, string connection) = await postgres.NewDatabaseAsync();
        Assert.Equal(0, (await Tool.RunAsync(Tool.Input(connection))).Exit);
        string role = NewName("app");
        await postgres.RunAsync($"CREATE ROLE {role}; GRANT INSERT ON approvals.{History} TO {role};", name);

        Tool.ToolRun run = await Tool.RunAsync(Tool.Input(connection, role));

        Assert.Equal((0, string.Empty), (run.Exit, run.Error));
        Assert.True(await HoldsAsync(name, "table", role, $"approvals.{History}", "INSERT"));
    }

    [Fact]
    public async Task Migrate_ConnectionThatCannotConnect_ExitsOneWithConnectionFailed()
    {
        Tool.ToolRun run = await Tool.RunAsync(Tool.Input("Host=127.0.0.1;Port=1;Timeout=2;Username=none;Database=none"));

        Assert.Equal(1, run.Exit);
        (string code, IReadOnlyDictionary<string, string> details) = run.Failure();
        Assert.Equal("approvals.migrate.connectionfailed", code);
        Assert.Empty(details);
    }

    [Fact]
    public async Task Migrate_MigrationThatFails_ExitsOneNamingIt()
    {
        (string name, string connection) = await postgres.NewDatabaseAsync();
        await postgres.RunAsync("CREATE SCHEMA approvals; CREATE TABLE approvals.alerts (other int);", name);

        Tool.ToolRun run = await Tool.RunAsync(Tool.Input(connection));

        Assert.Equal(1, run.Exit);
        (string code, IReadOnlyDictionary<string, string> details) = run.Failure();
        Assert.Equal("approvals.migrate.failed", code);
        Assert.EndsWith("_Foundations", details["migration"], StringComparison.Ordinal);
        Assert.Empty(await HistoryAsync(connection));
    }

    [Fact]
    public async Task APR_REQ_015_AC6_LoginNamedInRetentionRolesHoldsThemisRetentionWhichDeletesFromTheRequestAndDelegationTablesAndUpdatesTheirPersonAndReasonColumnsOnly()
    {
        (string name, string connection) = await postgres.NewDatabaseAsync();
        string login = NewName("app");
        await postgres.RunAsync($"CREATE ROLE {login} LOGIN PASSWORD 'test';");

        Tool.ToolRun first = await Tool.RunAsync(Tool.Input(connection, [login], [login]));
        Tool.ToolRun second = await Tool.RunAsync(Tool.Input(connection, [login], [login]));

        Assert.Equal((0, string.Empty), (first.Exit, first.Error));
        Assert.Equal((0, string.Empty), (second.Exit, second.Error));
        await using NpgsqlConnection database = new(postgres.Connection(name));
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand read = new(
            """
            SELECT
                (SELECT NOT rolcanlogin FROM pg_roles WHERE rolname = 'approvals_retention'),
                pg_has_role(@login::name, 'approvals_retention', 'USAGE'),
                (SELECT string_agg(c.relname, ',' ORDER BY c.relname) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                    WHERE n.nspname = 'approvals' AND c.relkind IN ('r', 'p') AND has_table_privilege('approvals_retention', c.oid, 'DELETE')),
                (SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                    WHERE n.nspname = 'approvals' AND c.relkind IN ('r', 'p') AND has_table_privilege('approvals_retention', c.oid, 'UPDATE')),
                (SELECT string_agg(c.relname || '.' || a.attname, ',' ORDER BY c.relname, a.attname) FROM pg_class c
                    JOIN pg_namespace n ON n.oid = c.relnamespace JOIN pg_attribute a ON a.attrelid = c.oid AND a.attnum > 0 AND NOT a.attisdropped
                    WHERE n.nspname = 'approvals' AND c.relkind IN ('r', 'p') AND has_column_privilege('approvals_retention', c.oid, a.attnum, 'UPDATE'))
            """,
            database);
        read.Parameters.AddWithValue("login", login);
        await using NpgsqlDataReader reader = await read.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        await reader.ReadAsync(TestContext.Current.CancellationToken);

        Assert.True(reader.GetBoolean(0), "approvals_retention can log in");
        Assert.True(reader.GetBoolean(1), "the login does not hold approvals_retention");
        Assert.Equal("assignments,decisions,delegations,events,requests,steps", reader.GetString(2));
        Assert.Equal(0L, reader.GetInt64(3));
        Assert.Equal(
            "assignments.person,assignments.via,decisions.acting_for,decisions.decider,decisions.reason,delegations.created_by,delegations.ended_by,"
                + "delegations.from_person,delegations.to_person,events.acting_for,events.actor,events.notify,requests.closed_by,requests.closed_for,"
                + "requests.reason,requests.requester,requests.subject",
            reader.GetString(4));
    }

    [Fact]
    public async Task APR_OPS_002_AC4_LoginInRetentionRolesTheDatabaseLacksExitsOneWithInputInvalidNamingRetentionRolesAndAppliesNothing()
    {
        (_, string connection) = await postgres.NewDatabaseAsync();
        string present = NewName("app");
        await postgres.RunAsync($"CREATE ROLE {present};");

        Tool.ToolRun run = await Tool.RunAsync(Tool.Input(connection, [present], [NewName("missing")]));

        AssertFailure(run, "approvals.migrate.inputinvalid", "member", "retentionRoles");
        Assert.Empty(await HistoryAsync(connection));
    }

    private static string NewName(string prefix)
    {
        return prefix + "_" + Guid.NewGuid().ToString("N")[..12];
    }

    private static void AssertFailure(Tool.ToolRun run, string code, string detail, string value)
    {
        Assert.Equal(1, run.Exit);
        (string failed, IReadOnlyDictionary<string, string> details) = run.Failure();
        Assert.Equal(code, failed);
        Assert.Equal(new Dictionary<string, string> { [detail] = value }, details);
    }

    /// <summary>
    /// Makes a database owned by a new login role that is no superuser, and applies the
    /// migrations as that role; answers the database, the role's connection string and the role.
    /// </summary>
    private async Task<(string Name, string Connection, string Migrator)> MigratedByAnOrdinaryRoleAsync()
    {
        (string name, string connection) = await postgres.NewDatabaseAsync();
        string migrator = NewName("migrator");
        await postgres.RunAsync($"CREATE ROLE {migrator} LOGIN PASSWORD 'test'; ALTER DATABASE {name} OWNER TO {migrator};");
        string own = new NpgsqlConnectionStringBuilder(connection) { Username = migrator, Password = "test" }.ConnectionString;
        Tool.ToolRun run = await Tool.RunAsync(Tool.Input(own));
        Assert.True(run.Exit == 0, run.Error);
        return (name, own, migrator);
    }

    /// <summary>Whether <paramref name="role"/> holds <paramref name="privilege"/> on <paramref name="target"/>, a schema, table or sequence, as the superuser reads it.</summary>
    private async Task<bool> HoldsAsync(string database, string kind, string role, string target, string privilege)
    {
        string query = kind switch
        {
            "schema" => "SELECT has_schema_privilege(@role::name, @target, @privilege)",
            "table" => "SELECT has_table_privilege(@role::name, @target, @privilege)",
            "sequence" => "SELECT has_sequence_privilege(@role::name, @target, @privilege)",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "A schema, table or sequence."),
        };
        await using NpgsqlConnection connection = new(postgres.Connection(database));
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new(query, connection);
        command.Parameters.AddWithValue("role", role);
        command.Parameters.AddWithValue("target", target);
        command.Parameters.AddWithValue("privilege", privilege);
        return (bool)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private static async Task<IReadOnlyList<string>> HistoryAsync(string connection)
    {
        await using NpgsqlConnection database = new(connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("SELECT \"MigrationId\" FROM approvals.__migrations_history ORDER BY 1", database);
        List<string> ids = [];
        try
        {
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                ids.Add(reader.GetString(0));
            }
        }
        catch (PostgresException missing) when (missing.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            return ids;
        }

        return ids;
    }
}