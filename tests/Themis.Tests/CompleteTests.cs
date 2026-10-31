using System.Threading.Tasks;

using Npgsql;

using Themis.Core;

using Xunit;

using static Themis.Tests.Requests;

namespace Themis.Tests;

/// <summary>Completing approved requests against a real database in a container, each test on a database of its own (<c>01</c> APR-REQ-010, APR-REQ-012; <c>03</c> APR-LIB-008).</summary>
[Trait("Category", "integration")]
public sealed class CompleteTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly ApprovalTypeDefinition OneStep = Type(Step("review", ApprovalMode.AnyOne, Person(A)));

    [Fact]
    public async Task APR_REQ_012_AC1_CompletingAPendingRequestIsRefusedAndCompletingAnApprovedOneEndsItCompleted()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(OneStep);
        RequestId request = await host.CreatedAsync(Request());

        Result pending = await host.CompleteAsync(request, Requester);
        await host.DecideAsync(request, A);
        Result completed = await host.CompleteAsync(request, Requester);
        RequestRecord record = await host.ReadAsync(request);

        Assert.Equal("approvals.request.notapproved", pending.Failure?.Code);
        Assert.True(completed.IsSuccess);
        Assert.Equal((RequestStatus.Completed, Requester), (record.Status, record.ClosedBy));
        Assert.NotNull(record.ClosedAt);
        RecordedEvent last = (await host.EventsAsync(request))[^1];
        Assert.Equal((EventKind.Completed, Requester, (int?)null), (last.Kind, last.Actor, last.Step));
        Assert.Equal([Requester, Watcher], last.Notify);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task Complete_ClosedOrUnknownRequest_IsRefusedAndChangesNothing()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(OneStep);
        RequestId request = await host.CreatedAsync(Request());
        await host.DecideAsync(request, A);
        await host.CompleteAsync(request, Requester);
        string before = await host.CountsAsync();

        Result again = await host.CompleteAsync(request, Requester);
        Result unknown = await host.CompleteAsync(new RequestId(System.Guid.CreateVersion7()), Requester);

        Assert.Equal(("approvals.request.closed", "approvals.request.unknown"), (again.Failure?.Code, unknown.Failure?.Code));
        Assert.Equal(before, await host.CountsAsync());
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_010_AC2_ApprovedRequestNotYetCompletedCanBeCancelledAndACompletedOneCannot()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(OneStep);
        RequestId approved = await host.CreatedAsync(Request());
        await host.DecideAsync(approved, A);
        Result cancelled = await host.CancelAsync(approved, Requester);
        RequestId completed = await host.CreatedAsync(Request());
        await host.DecideAsync(completed, A);
        await host.CompleteAsync(completed, Requester);

        Result refused = await host.CancelAsync(completed, Requester);

        Assert.True(cancelled.IsSuccess);
        Assert.Equal("approvals.request.closed", refused.Failure?.Code);
        Assert.Equal((RequestStatus.Cancelled, RequestStatus.Completed), ((await host.ReadAsync(approved)).Status, (await host.ReadAsync(completed)).Status));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task Complete_OnAHostTransaction_LeavesTheRequestApprovedWhenTheHostRollsBackAndCompletesItWhenItCommits()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(OneStep);
        RequestId request = await host.CreatedAsync(Request());
        await host.DecideAsync(request, A);
        await using NpgsqlConnection database = await host.OpenHostAsync();

        await CompleteOnHostAsync(host, database, request, commit: false);
        RequestStatus rolledBack = (await host.ReadAsync(request)).Status;
        await CompleteOnHostAsync(host, database, request, commit: true);

        Assert.Equal((RequestStatus.Approved, RequestStatus.Completed), (rolledBack, (await host.ReadAsync(request)).Status));
        Assert.Equal(EventKind.Completed, (await host.EventsAsync(request))[^1].Kind);
        host.AssertNoLogHoldsAValueUsed();
    }

    private static async Task CompleteOnHostAsync(RequestHost host, NpgsqlConnection database, RequestId request, bool commit)
    {
        await using NpgsqlTransaction transaction = await database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        Result completed = await host.CompleteAsync(request, Requester, transaction);
        Assert.True(completed.IsSuccess);
        if (commit)
        {
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }
        else
        {
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }
    }
}