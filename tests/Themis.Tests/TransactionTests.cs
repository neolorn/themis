using System;
using System.Text;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Themis.Operations;

using Xunit;

namespace Themis.Tests;

[Trait("Category", "integration")]
public sealed class TransactionTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string SerializationFailure = "DO $$ BEGIN RAISE EXCEPTION 'serialization failure' USING ERRCODE = '40001'; END $$";

    private const string Deadlock = "DO $$ BEGIN RAISE EXCEPTION 'deadlock detected' USING ERRCODE = '40P01'; END $$";

    [Fact]
    public async Task RunAsync_TransactionAbortedWith40001Each_IsRunAgainAtMostRetriesTimes()
    {
        await using ApprovalsDatabase database = await OpenAsync();
        ApprovalsOptions options = new() { Database = { Retries = 3 } };
        int runs = 0;

        PostgresException failure = await Assert.ThrowsAsync<PostgresException>(() => LibraryTransactions.RunAsync(
            database,
            options,
            async (context, cancellationToken) =>
            {
                runs++;
                return await context.Database.ExecuteSqlRawAsync(SerializationFailure, cancellationToken);
            },
            TestContext.Current.CancellationToken));

        Assert.Equal(PostgresErrorCodes.SerializationFailure, failure.SqlState);
        Assert.Equal(4, runs);
    }

    [Fact]
    public async Task RunAsync_TransactionAbortedTwiceFor40001And40P01_CommitsOnTheThirdRun()
    {
        await using ApprovalsDatabase database = await OpenAsync();
        int runs = 0;

        int result = await LibraryTransactions.RunAsync(
            database,
            new ApprovalsOptions(),
            async (context, cancellationToken) =>
            {
                runs++;
                if (runs < 3)
                {
                    await context.Database.ExecuteSqlRawAsync(runs == 1 ? SerializationFailure : Deadlock, cancellationToken);
                }

                return runs;
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(3, result);
    }

    [Fact]
    public async Task RunAsync_RetriesZero_RunsOnce()
    {
        await using ApprovalsDatabase database = await OpenAsync();
        int runs = 0;

        await Assert.ThrowsAsync<PostgresException>(() => LibraryTransactions.RunAsync(
            database,
            new ApprovalsOptions { Database = { Retries = 0 } },
            async (context, cancellationToken) =>
            {
                runs++;
                return await context.Database.ExecuteSqlRawAsync(SerializationFailure, cancellationToken);
            },
            TestContext.Current.CancellationToken));

        Assert.Equal(1, runs);
    }

    [Fact]
    public async Task RunAsync_ErrorOtherThanADeadlockOrASerializationFailure_IsNotRunAgain()
    {
        await using ApprovalsDatabase database = await OpenAsync();
        int runs = 0;

        PostgresException failure = await Assert.ThrowsAsync<PostgresException>(() => LibraryTransactions.RunAsync(
            database,
            new ApprovalsOptions(),
            async (context, cancellationToken) =>
            {
                runs++;
                return await context.Database.ExecuteSqlRawAsync("SELECT 1 / 0", cancellationToken);
            },
            TestContext.Current.CancellationToken));

        Assert.Equal(PostgresErrorCodes.DivisionByZero, failure.SqlState);
        Assert.Equal(1, runs);
    }

    [Fact]
    public async Task RunAsync_EachTransaction_SetsItsIdleTimeoutToTwiceTheTimeout()
    {
        await using ApprovalsDatabase database = await OpenAsync();

        string timeout = await LibraryTransactions.RunAsync(
            database,
            new ApprovalsOptions { Timeout = TimeSpan.FromSeconds(7) },
            (context, cancellationToken) => context.Database.SqlQueryRaw<string>("SELECT current_setting('idle_in_transaction_session_timeout') AS \"Value\"").SingleAsync(cancellationToken),
            TestContext.Current.CancellationToken);

        Assert.Equal("14s", timeout);
    }

    [Fact]
    public async Task Now_ParameterOfAStatement_IsTheLibrarysClocksInstant()
    {
        await using ApprovalsDatabase database = await OpenAsync();
        DateTimeOffset instant = new(2031, 2, 3, 4, 5, 6, 789, TimeSpan.Zero);

        DateTime read = await LibraryTransactions.RunAsync(
            database,
            new ApprovalsOptions(),
            (context, cancellationToken) => context.Database.SqlQueryRaw<DateTime>("SELECT @now AS \"Value\"", LibraryTransactions.Now(new FixedClock(instant))).SingleAsync(cancellationToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(instant.UtcDateTime, read);
        Assert.Equal(DateTimeKind.Utc, read.Kind);
    }

    private async Task<ApprovalsDatabase> OpenAsync()
    {
        (_, string connection) = await postgres.NewMigratedDatabaseAsync();
        ApprovalsDatabase database = new();
        database.Open(Encoding.UTF8.GetBytes(connection));
        return database;
    }

    private sealed class FixedClock(DateTimeOffset instant) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return instant;
        }
    }
}