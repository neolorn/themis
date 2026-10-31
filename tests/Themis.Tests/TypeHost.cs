using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using NpgsqlTypes;

using Themis.Core;

using Xunit;

namespace Themis.Tests;

/// <summary>
/// A started host on a migrated database of its own, whose <see cref="IApprovalTypes"/> a test
/// calls, capturing every log line so the test can assert that none holds a value it used.
/// </summary>
internal sealed class TypeHost : IAsyncDisposable
{
    private readonly TestHost host;

    private TypeHost(TestHost host, string database, string connection, LogCapture logs)
    {
        this.host = host;
        Database = database;
        Connection = connection;
        Logs = logs;
    }

    /// <summary>The database's name.</summary>
    internal string Database { get; }

    /// <summary>The superuser's connection string to the database.</summary>
    internal string Connection { get; }

    internal LogCapture Logs { get; }

    internal IServiceProvider Services => host.Services;

    /// <summary>Starts a host on a new migrated database of <paramref name="postgres"/>, with <paramref name="configuration"/>.</summary>
    internal static async Task<TypeHost> StartAsync(PostgresFixture postgres, IReadOnlyDictionary<string, string?>? configuration = null)
    {
        (string name, string connection) = await postgres.NewMigratedDatabaseAsync();
        LogCapture logs = new(everyCategory: true);
        TestHost host = TestHost.Build(configuration, before: TestHost.Seams(SecretSourceFake.WithConnection(connection)), logs: logs);
        await host.StartAsync();
        return new TypeHost(host, name, connection, logs);
    }

    /// <summary>Runs <paramref name="call"/> on the <see cref="IApprovalTypes"/> of a scope of its own, as a host's request would.</summary>
    internal async Task<T> CallAsync<T>(Func<IApprovalTypes, ValueTask<T>> call)
    {
        AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using (scope)
        {
            return await call(scope.ServiceProvider.GetRequiredService<IApprovalTypes>());
        }
    }

    /// <summary>Publishes <paramref name="definition"/>.</summary>
    internal Task<Result<PublishedVersion>> PublishAsync(ApprovalTypeDefinition definition)
    {
        return CallAsync(types => types.PublishAsync(definition, TestContext.Current.CancellationToken));
    }

    /// <summary>Reads version <paramref name="version"/> of <paramref name="key"/>, or its latest.</summary>
    internal Task<Result<ApprovalTypeVersion>> ReadAsync(string key, int? version = null)
    {
        return CallAsync(types => types.ReadAsync(key, version, TestContext.Current.CancellationToken));
    }

    /// <summary>Retires <paramref name="key"/>.</summary>
    internal Task<Result> RetireAsync(string key)
    {
        return CallAsync(types => types.RetireAsync(key, TestContext.Current.CancellationToken));
    }

    /// <summary>The versions the database holds, each its key, number, definition and hash, in the order of their keys and numbers.</summary>
    internal async Task<IReadOnlyList<(string Key, int Version, string Definition, byte[] Hash)>> VersionsAsync()
    {
        await using NpgsqlConnection database = new(Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand select = new("SELECT key, version, definition, hash FROM approvals.versions ORDER BY key, version", database);
        await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        List<(string, int, string, byte[])> versions = [];
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            versions.Add((reader.GetString(0), reader.GetInt32(1), reader.GetString(2), (byte[])reader.GetValue(3)));
        }

        return versions;
    }

    /// <summary>The number of types the database holds.</summary>
    internal async Task<long> TypeCountAsync()
    {
        await using NpgsqlConnection database = new(Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand count = new("SELECT count(*) FROM approvals.types", database);
        return (long)(await count.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    /// <summary>
    /// The type's lock as <c>04</c> APR-OPS-003 gives it, worked out here on its own: the first
    /// 8 bytes, read as a signed big-endian integer, of the SHA-256 of the text
    /// <c>approvals-type</c>, a zero byte and the key.
    /// </summary>
    internal static long TypeLockKey(string key)
    {
        byte[] text = [.. "approvals-type"u8, 0, .. Encoding.UTF8.GetBytes(key)];
        return BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(text));
    }

    /// <summary>Takes the type's lock of <paramref name="key"/> in a transaction of the test's own, held until it is released.</summary>
    internal async Task<HeldLock> HoldTypeLockAsync(string key)
    {
        NpgsqlConnection? holder = new(Connection);
        try
        {
            await holder.OpenAsync(TestContext.Current.CancellationToken);
            NpgsqlTransaction transaction = await holder.BeginTransactionAsync(TestContext.Current.CancellationToken);
            await using (NpgsqlCommand take = new("SELECT pg_advisory_xact_lock(@key)", holder, transaction))
            {
                take.Parameters.AddWithValue("key", TypeLockKey(key));
                await take.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }

            HeldLock held = new(holder, transaction, Connection, TypeLockKey(key));
            holder = null;
            return held;
        }
        finally
        {
            holder?.Dispose();
        }
    }

    /// <summary>Asserts that no log line holds a person reference, role or value of <see cref="Definitions"/> (<c>04</c> APR-OPS-005).</summary>
    internal void AssertNoLogHoldsAValueUsed()
    {
        string used = string.Join('|', Definitions.Unloggable.Select(Regex.Escape));
        Assert.All(Logs.Entries, entry => Assert.DoesNotMatch(used, entry.Message));
    }

    public async ValueTask DisposeAsync()
    {
        await host.DisposeAsync();
        Logs.Dispose();
    }

    /// <summary>A type's lock a test holds in a transaction of its own, so a library's transaction that takes it waits.</summary>
    internal sealed class HeldLock(NpgsqlConnection holder, NpgsqlTransaction transaction, string connection, long key) : IAsyncDisposable
    {
        /// <summary>Waits until <paramref name="count"/> sessions wait for the lock.</summary>
        internal async Task WaitForWaitersAsync(int count)
        {
            await using NpgsqlConnection database = new(connection);
            await database.OpenAsync(TestContext.Current.CancellationToken);
            for (int attempt = 0; attempt < 200; attempt++)
            {
                await using NpgsqlCommand waiting = new("SELECT count(*) FROM pg_locks WHERE locktype = 'advisory' AND NOT granted AND classid = @high AND objid = @low AND objsubid = 1", database);
                waiting.Parameters.AddWithValue("high", NpgsqlDbType.Oid, (uint)(key >> 32));
                waiting.Parameters.AddWithValue("low", NpgsqlDbType.Oid, (uint)key);
                if ((long)(await waiting.ExecuteScalarAsync(TestContext.Current.CancellationToken))! == count)
                {
                    return;
                }

                await Task.Delay(50, TestContext.Current.CancellationToken);
            }

            Assert.Fail($"{count} session(s) never waited for the type's lock.");
        }

        /// <summary>Releases the lock by committing the test's transaction.</summary>
        internal async Task ReleaseAsync()
        {
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await holder.DisposeAsync();
        }
    }
}