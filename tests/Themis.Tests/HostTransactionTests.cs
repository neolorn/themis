using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;

using Npgsql;

using Themis.Core;

using Xunit;

using static Themis.Tests.Requests;

namespace Themis.Tests;

/// <summary>Operations on a host's transaction against a real database in a container, each test on a database of its own (<c>03</c> APR-LIB-008; <c>01</c> APR-REQ-001, APR-REQ-002; <c>04</c> APR-OPS-003).</summary>
[Trait("Category", "integration")]
public sealed class HostTransactionTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly ApprovalTypeDefinition OneStep = Type(Step("review", ApprovalMode.AnyOne, Person(A)));

    [Fact]
    public async Task APR_REQ_001_AC1_RequestCreatedInAHostTransactionThatRollsBackLeavesNoRequestAssignmentOrEvent()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(OneStep);
        await using NpgsqlConnection database = await host.OpenHostAsync();

        await using (NpgsqlTransaction transaction = await database.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            Result<CreatedRequest> created = await host.CreateAsync(Request(), transaction);
            Assert.Equal(RequestStatus.Pending, created.Value?.Status);
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal("0,0,0,0,0", await host.CountsAsync());
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_LIB_008_AC1_CreateOnAHostTransactionLeavesNothingWhenTheHostRollsBackAndBothWhenItCommits()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(OneStep);
        await using NpgsqlConnection database = await host.OpenHostAsync();

        await HostWriteAndCreateAsync(host, database, "order-rolled", commit: false);
        Assert.Equal(("0,0,0,0,0", 0L), (await host.CountsAsync(), await host.HostRowsAsync()));
        await HostWriteAndCreateAsync(host, database, "order-committed", commit: true);

        Assert.Equal(("1,1,1,0,1", 1L), (await host.CountsAsync(), await host.HostRowsAsync()));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_LIB_008_AC2_RefusedCreateOnAHostTransactionLeavesTheHostsEarlierWritesAndTheHostsCommitSucceeds()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(OneStep);
        await host.CreatedAsync(Request());
        await using NpgsqlConnection database = await host.OpenHostAsync();

        await using (NpgsqlTransaction transaction = await database.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            await InsertHostRowAsync(database, transaction, "order-host");
            Result<CreatedRequest> unknown = await host.CreateAsync(Request() with { Type = "purchase.other" }, transaction);
            Result<CreatedRequest> open = await host.CreateAsync(Request(), transaction);
            Assert.Equal(("approvals.type.unknown", "approvals.request.open"), (unknown.Failure?.Code, open.Failure?.Code));
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(("1,1,1,0,1", 1L), (await host.CountsAsync(), await host.HostRowsAsync()));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_LIB_008_AC3_TransactionOfAnotherProviderOrACommittedOneMakesTheCallThrowArgumentException()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(OneStep);
        await using NpgsqlConnection database = await host.OpenHostAsync();
        NpgsqlTransaction committed = await database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await committed.CommitAsync(TestContext.Current.CancellationToken);
        CancelRequest cancel = new() { Request = new RequestId(Guid.CreateVersion7()), Actor = Requester };

        await Assert.ThrowsAsync<ArgumentException>(() => host.CreateAsync(Request(), new ForeignTransaction()));
        await Assert.ThrowsAsync<ArgumentException>(() => host.CreateAsync(Request(), committed));
        await Assert.ThrowsAsync<ArgumentException>(() => host.CancelAsync(cancel, new ForeignTransaction()));
        await Assert.ThrowsAsync<ArgumentException>(() => host.CancelAsync(cancel, committed));

        await committed.DisposeAsync();
        Assert.Equal("0,0,0,0,0", await host.CountsAsync());
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_002_AC1_TwoCreatesOfOneTypeForOneTargetAtOnceRecordOneRequestAndTheOtherIsRefusedOpen()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(OneStep);
        await using NpgsqlConnection database = await host.OpenHostAsync();

        Task<Result<CreatedRequest>> second;
        await using (NpgsqlTransaction transaction = await database.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            Result<CreatedRequest> first = await host.CreateAsync(Request(), transaction);
            Assert.Equal(RequestStatus.Pending, first.Value?.Status);
            second = Task.Run(() => host.CreateAsync(Request()), TestContext.Current.CancellationToken);
            await host.WaitForLockWaitersAsync(1);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal("approvals.request.open", (await second).Failure?.Code);
        Assert.Equal("1,1,1,0,1", await host.CountsAsync());
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_OPS_003_AC1_RequestCreatedInAHostTransactionOnADataSourceTheHostBuiltCommitsWithTheHostsWrite()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(OneStep);
        (await host.OpenHostAsync()).Dispose();
        await using NpgsqlDataSource source = NpgsqlDataSource.Create(host.Connection);
        await using NpgsqlConnection database = await source.OpenConnectionAsync(TestContext.Current.CancellationToken);

        await HostWriteAndCreateAsync(host, database, "order-source", commit: true);

        Assert.Equal(("1,1,1,0,1", 1L), (await host.CountsAsync(), await host.HostRowsAsync()));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task Cancel_OnAHostTransactionThatRollsBack_LeavesTheRequestPending()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(OneStep);
        RequestId request = await host.CreatedAsync(Request());
        await using NpgsqlConnection database = await host.OpenHostAsync();

        await using (NpgsqlTransaction transaction = await database.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            Result cancelled = await host.CancelAsync(new CancelRequest { Request = request, Actor = Requester }, transaction);
            Assert.True(cancelled.IsSuccess);
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(RequestStatus.Pending, (await host.ReadAsync(request)).Status);
        host.AssertNoLogHoldsAValueUsed();
    }

    private static async Task HostWriteAndCreateAsync(RequestHost host, NpgsqlConnection database, string order, bool commit)
    {
        await using NpgsqlTransaction transaction = await database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await InsertHostRowAsync(database, transaction, order);
        Result<CreatedRequest> created = await host.CreateAsync(Request(), transaction);
        Assert.Equal(RequestStatus.Pending, created.Value?.Status);
        if (commit)
        {
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }
        else
        {
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }
    }

    private static async Task InsertHostRowAsync(NpgsqlConnection database, NpgsqlTransaction transaction, string order)
    {
        await using NpgsqlCommand insert = new("INSERT INTO public.host_orders (id) VALUES (@id)", database, transaction);
        insert.Parameters.AddWithValue("id", order);
        await insert.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A transaction of a provider other than Npgsql.</summary>
    private sealed class ForeignTransaction : DbTransaction
    {
        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

        protected override DbConnection? DbConnection => null;

        public override void Commit()
        {
        }

        public override void Rollback()
        {
        }
    }
}