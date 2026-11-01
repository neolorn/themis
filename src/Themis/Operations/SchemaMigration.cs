using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql;

namespace Themis.Operations;

/// <summary>Applies the release's migrations as <c>themis migrate</c> does (<c>04</c> APR-OPS-002).</summary>
internal static class SchemaMigration
{
    /// <summary>
    /// The first of the roles the database does not have, each name compared as text, so a
    /// name past PostgreSQL's 63 bytes matches no role rather than the one named by its first 63.
    /// </summary>
    private const string MissingRole = """
        SELECT wanted.name
        FROM unnest(@roles) WITH ORDINALITY AS wanted(name, place)
        WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE pg_roles.rolname::text = wanted.name)
        ORDER BY wanted.place
        LIMIT 1
        """;

    /// <summary>
    /// Grants the role the transaction names the use of the schema, of each table but the
    /// migration history, the reading of the history and the use of each sequence, revoking
    /// nothing; the server quotes each name.
    /// </summary>
    private const string Grant = """
        DO $$
        DECLARE
            grantee text := current_setting('approvals.grant_role');
            item record;
        BEGIN
            EXECUTE format('GRANT USAGE ON SCHEMA approvals TO %I', grantee);
            FOR item IN
                SELECT c.relname FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE n.nspname = 'approvals' AND c.relkind IN ('r', 'p')
            LOOP
                IF item.relname = '__migrations_history' THEN
                    EXECUTE format('GRANT SELECT ON approvals.%I TO %I', item.relname, grantee);
                ELSE
                    EXECUTE format('GRANT SELECT, INSERT, UPDATE ON approvals.%I TO %I', item.relname, grantee);
                END IF;
            END LOOP;
            EXECUTE format('GRANT USAGE ON ALL SEQUENCES IN SCHEMA approvals TO %I', grantee);
        END
        $$
        """;

    /// <summary>
    /// Whether the role holds each privilege <see cref="Grant"/> gives, as PostgreSQL warns and
    /// grants nothing where the migration's role holds a privilege without the right to grant it.
    /// </summary>
    private const string Holds = """
        SELECT EXISTS (
            SELECT 1 FROM pg_roles r
            WHERE r.rolname::text = @role
              AND has_schema_privilege(r.oid, 'approvals', 'USAGE')
              AND NOT EXISTS (
                  SELECT 1
                  FROM pg_class c
                  JOIN pg_namespace n ON n.oid = c.relnamespace
                  CROSS JOIN LATERAL unnest(CASE WHEN c.relname = '__migrations_history' THEN ARRAY['SELECT'] ELSE ARRAY['SELECT', 'INSERT', 'UPDATE'] END) AS wanted(privilege)
                  WHERE n.nspname = 'approvals' AND c.relkind IN ('r', 'p') AND NOT has_table_privilege(r.oid, c.oid, wanted.privilege))
              AND NOT EXISTS (
                  SELECT 1
                  FROM pg_class c
                  JOIN pg_namespace n ON n.oid = c.relnamespace
                  WHERE n.nspname = 'approvals' AND c.relkind = 'S' AND NOT has_sequence_privilege(r.oid, c.oid, 'USAGE')))
        """;

    /// <summary>
    /// Creates the role <c>approvals_retention</c> where it is absent, unable to log in, and grants
    /// it <c>DELETE</c> on the request and delegation tables and <c>UPDATE</c> on their person
    /// and reason columns (<c>01</c> APR-REQ-015).
    /// </summary>
    private const string RetentionGrantSql = """
        DO $$
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'approvals_retention') THEN
                CREATE ROLE approvals_retention NOLOGIN;
            END IF;
            GRANT DELETE ON approvals.requests, approvals.steps, approvals.assignments, approvals.decisions, approvals.events, approvals.delegations TO approvals_retention;
            GRANT UPDATE (requester, subject, closed_by, closed_for, reason) ON approvals.requests TO approvals_retention;
            GRANT UPDATE (person, via) ON approvals.assignments TO approvals_retention;
            GRANT UPDATE (decider, acting_for, reason) ON approvals.decisions TO approvals_retention;
            GRANT UPDATE (from_person, to_person, created_by, ended_by) ON approvals.delegations TO approvals_retention;
            GRANT UPDATE (actor, acting_for, notify) ON approvals.events TO approvals_retention;
        END
        $$
        """;

    /// <summary>Whether <c>approvals_retention</c> holds each privilege <see cref="RetentionGrantSql"/> gives, the tables and columns as <see cref="RetentionGrant"/> names them.</summary>
    private const string RetentionHolds = """
        SELECT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'approvals_retention')
            AND (SELECT bool_and(has_table_privilege('approvals_retention', format('approvals.%I', name), 'DELETE')) FROM unnest(@tables) AS name)
            AND (SELECT bool_and(has_column_privilege('approvals_retention', format('approvals.%I', split_part(name, '.', 1)), split_part(name, '.', 2), 'UPDATE')) FROM unnest(@columns) AS name)
        """;

    /// <summary>Whether the login holds the privileges of <c>approvals_retention</c>.</summary>
    private const string RetentionGranted = "SELECT pg_has_role(@login, 'approvals_retention', 'USAGE')";

    /// <summary>Grants the role <c>approvals_retention</c> to the login the transaction names, the server quoting its name.</summary>
    private const string GrantRetention = """
        DO $$
        BEGIN
            EXECUTE format('GRANT approvals_retention TO %I', current_setting('approvals.grant_role'));
        END
        $$
        """;

    /// <summary>
    /// Checks that the database has each of <paramref name="roles"/> and
    /// <paramref name="retentionLogins"/>, applies the pending migrations one at a time, each in
    /// its own transaction, then, in one transaction, grants each role the use of the schema,
    /// granting none <c>DELETE</c> or <c>TRUNCATE</c>, and, where a retention login is named,
    /// creates and grants the role <c>approvals_retention</c> and grants it to each such login;
    /// answers none on success, or the failure, the migrations before a failing one staying
    /// applied and no grant of a failed run staying.
    /// </summary>
    internal static async Task<MigrationFailure?> ApplyAsync(string connection, IReadOnlyList<string> roles, IReadOnlyList<string> retentionLogins, CancellationToken cancellationToken)
    {
        try
        {
            _ = new NpgsqlConnectionStringBuilder(connection);
        }
        catch (ArgumentException)
        {
            return Failure(Codes.MigrateInputInvalid, "member", "connection");
        }

        ApprovalsContext context = ApprovalsContext.Create(connection);
        await using (context.ConfigureAwait(false))
        {
            try
            {
                await context.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception failure) when (failure is DbException or SocketException or TimeoutException or InvalidOperationException or ArgumentException)
            {
                return Failure(Codes.MigrateConnectionFailed);
            }

            NpgsqlConnection database = (NpgsqlConnection)context.Database.GetDbConnection();
            IReadOnlyList<string> pending;
            try
            {
                if (roles.Count > 0 && await MissingRoleAsync(database, roles, cancellationToken).ConfigureAwait(false) is not null)
                {
                    return Failure(Codes.MigrateInputInvalid, "member", "applicationRoles");
                }

                if (retentionLogins.Count > 0 && await MissingRoleAsync(database, retentionLogins, cancellationToken).ConfigureAwait(false) is not null)
                {
                    return Failure(Codes.MigrateInputInvalid, "member", "retentionRoles");
                }

                pending = [.. await context.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)];
            }
            catch (DbException)
            {
                // Reading the roles and the history is part of reaching the database, before any migration.
                return Failure(Codes.MigrateConnectionFailed);
            }

            IMigrator migrator = context.GetService<IMigrator>();
            foreach (string migration in pending)
            {
                try
                {
                    await migrator.MigrateAsync(migration, cancellationToken).ConfigureAwait(false);
                }
                catch (DbException)
                {
                    return Failure(Codes.MigrateFailed, "migration", migration);
                }
            }

            return roles.Count > 0 || retentionLogins.Count > 0 ? await GrantAsync(database, roles, retentionLogins, cancellationToken).ConfigureAwait(false) : null;
        }
    }

    private static async Task<string?> MissingRoleAsync(NpgsqlConnection connection, IReadOnlyList<string> roles, CancellationToken cancellationToken)
    {
        NpgsqlCommand command = new(MissingRole, connection);
        await using (command.ConfigureAwait(false))
        {
            command.Parameters.AddWithValue("roles", roles.ToArray());
            return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        }
    }

    /// <summary>
    /// Grants each role, then, where a retention login is named, the role
    /// <c>approvals_retention</c>'s privileges and that role to each login, in one transaction,
    /// checking each holds what it was granted; answers none, or the role or login whose grant
    /// failed or is not found, named <c>approvals_retention</c> for that role's own, the transaction
    /// then rolling back. A failure opening or committing the transaction names the first, as no
    /// grant stays.
    /// </summary>
    private static async Task<MigrationFailure?> GrantAsync(NpgsqlConnection connection, IReadOnlyList<string> roles, IReadOnlyList<string> retentionLogins, CancellationToken cancellationToken)
    {
        List<(string Role, Func<NpgsqlTransaction, Task<bool>> Grant)> grants = [.. roles.Select(role => (role, (Func<NpgsqlTransaction, Task<bool>>)(transaction => GrantRoleAsync(connection, transaction, role, cancellationToken))))];
        if (retentionLogins.Count > 0)
        {
            grants.Add((RetentionGrant.Role, transaction => GrantRetentionRoleAsync(connection, transaction, cancellationToken)));
            grants.AddRange(retentionLogins.Select(login => (login, (Func<NpgsqlTransaction, Task<bool>>)(transaction => GrantRetentionLoginAsync(connection, transaction, login, cancellationToken)))));
        }

        string current = grants[0].Role;
        try
        {
            NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                foreach ((string role, Func<NpgsqlTransaction, Task<bool>> grant) in grants)
                {
                    current = role;
                    if (!await grant(transaction).ConfigureAwait(false))
                    {
                        return Failure(Codes.MigrateGrantFailed, "role", role);
                    }
                }

                current = grants[0].Role;
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (DbException)
        {
            return Failure(Codes.MigrateGrantFailed, "role", current);
        }

        return null;
    }

    private static async Task<bool> GrantRoleAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string role, CancellationToken cancellationToken)
    {
        NpgsqlCommand name = new("SELECT set_config('approvals.grant_role', @role, true)", connection, transaction);
        await using (name.ConfigureAwait(false))
        {
            name.Parameters.AddWithValue("role", role);
            await name.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        NpgsqlCommand grant = new(Grant, connection, transaction);
        await using (grant.ConfigureAwait(false))
        {
            await grant.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        NpgsqlCommand holds = new(Holds, connection, transaction);
        await using (holds.ConfigureAwait(false))
        {
            holds.Parameters.AddWithValue("role", role);
            return await holds.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is true;
        }
    }

    private static async Task<bool> GrantRetentionRoleAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        NpgsqlCommand grant = new(RetentionGrantSql, connection, transaction);
        await using (grant.ConfigureAwait(false))
        {
            await grant.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        NpgsqlCommand holds = new(RetentionHolds, connection, transaction);
        await using (holds.ConfigureAwait(false))
        {
            holds.Parameters.AddWithValue("tables", RetentionGrant.Tables);
            holds.Parameters.AddWithValue("columns", RetentionGrant.Columns);
            return await holds.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is true;
        }
    }

    private static async Task<bool> GrantRetentionLoginAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string login, CancellationToken cancellationToken)
    {
        NpgsqlCommand name = new("SELECT set_config('approvals.grant_role', @role, true)", connection, transaction);
        await using (name.ConfigureAwait(false))
        {
            name.Parameters.AddWithValue("role", login);
            await name.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        NpgsqlCommand grant = new(GrantRetention, connection, transaction);
        await using (grant.ConfigureAwait(false))
        {
            await grant.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        NpgsqlCommand holds = new(RetentionGranted, connection, transaction);
        await using (holds.ConfigureAwait(false))
        {
            holds.Parameters.AddWithValue("login", login);
            return await holds.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is true;
        }
    }

    private static MigrationFailure Failure(string code, string? detail = null, string? value = null)
    {
        Dictionary<string, string> details = new(StringComparer.Ordinal);
        if (detail is not null && value is not null)
        {
            details[detail] = value;
        }

        return new MigrationFailure(code, details);
    }
}