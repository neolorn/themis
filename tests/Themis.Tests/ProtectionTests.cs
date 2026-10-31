using System.Collections.Generic;
using System.Threading.Tasks;

using Npgsql;

using Themis.Core;

using Xunit;

using static Themis.Tests.Requests;

namespace Themis.Tests;

/// <summary>The protection of a request's fields and the changes recorded to them, against a real database in a container, each test on a database of its own (<c>01</c> APR-REQ-011; <c>03</c> APR-LIB-008).</summary>
[Trait("Category", "integration")]
public sealed class ProtectionTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Amount = "amount";

    private const string Unprotected = "notes";

    private static readonly ApprovalTypeDefinition OneStep = Type(Step("review", ApprovalMode.AnyOne, Person(A)));

    [Fact]
    public async Task APR_REQ_011_AC1_FieldOfAPendingRequestReadsLockedAndRecordingAChangeToItIsRefused()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(OneStep);
        RequestId request = await host.CreatedAsync(Request());
        string before = await host.CountsAsync();

        FieldProtection protection = await host.ProtectionAsync(Requests.Target, Amount);
        Result<IReadOnlyList<RequestId>> refused = await host.RecordChangeAsync(Requests.Target, Amount, C);

        Assert.Equal(FieldProtection.Locked, protection);
        Assert.Equal("approvals.field.locked", refused.Failure?.Code);
        Assert.Equal((RequestStatus.Pending, before), ((await host.ReadAsync(request)).Status, await host.CountsAsync()));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_011_AC2_ChangeRecordedToAnApprovedRequestsProtectedFieldVoidsItAndAChangeToAFieldItDoesNotProtectLeavesItApproved()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(OneStep);
        RequestId request = await host.CreatedAsync(Request());
        await host.DecideAsync(request, A);

        (FieldProtection Protected, FieldProtection Other) before = (await host.ProtectionAsync(Requests.Target, Amount), await host.ProtectionAsync(Requests.Target, Unprotected));
        Result<IReadOnlyList<RequestId>> other = await host.RecordChangeAsync(Requests.Target, Unprotected, C);
        RequestStatus afterOther = (await host.ReadAsync(request)).Status;
        Result<IReadOnlyList<RequestId>> voided = await host.RecordChangeAsync(Requests.Target, Amount, C);
        RequestRecord record = await host.ReadAsync(request);

        Assert.Equal((FieldProtection.Approved, FieldProtection.Free), before);
        Assert.Empty(other.Value!);
        Assert.Equal(RequestStatus.Approved, afterOther);
        Assert.Equal([request], voided.Value!);
        Assert.Equal((RequestStatus.Void, (string?)Amount, (ReasonCode?)ReasonCode.FieldChanged, (string?)C), (record.Status, record.ChangedField, record.ReasonCode, record.ClosedBy));
        Assert.NotNull(record.ClosedAt);
        RecordedEvent last = (await host.EventsAsync(request))[^1];
        Assert.Equal((EventKind.Voided, (string?)C, (ReasonCode?)ReasonCode.FieldChanged), (last.Kind, last.Actor, last.ReasonCode));
        Assert.Equal([Requester, Watcher], last.Notify);
        Assert.Equal(FieldProtection.Free, await host.ProtectionAsync(Requests.Target, Amount));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_REQ_011_AC3_ChangeRecordedAfterTheRequestIsCompletedVoidsNothing()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(OneStep);
        RequestId request = await host.CreatedAsync(Request());
        await host.DecideAsync(request, A);
        await host.CompleteAsync(request, Requester);

        FieldProtection protection = await host.ProtectionAsync(Requests.Target, Amount);
        Result<IReadOnlyList<RequestId>> changed = await host.RecordChangeAsync(Requests.Target, Amount, C);

        Assert.Equal(FieldProtection.Free, protection);
        Assert.Empty(changed.Value!);
        Assert.Equal(RequestStatus.Completed, (await host.ReadAsync(request)).Status);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task Protection_FieldOnAnotherTarget_IsFree()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(OneStep);
        await host.CreatedAsync(Request());

        FieldProtection protection = await host.ProtectionAsync(new Target { Kind = "order", Id = "order-other" }, Amount);

        Assert.Equal(FieldProtection.Free, protection);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task RecordChange_OnAHostTransaction_VoidsNothingWhenTheHostRollsBackAndVoidsWhenItCommits()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(OneStep);
        RequestId request = await host.CreatedAsync(Request());
        await host.DecideAsync(request, A);
        await using NpgsqlConnection database = await host.OpenHostAsync();

        await ChangeOnHostAsync(host, database, request, commit: false);
        RequestStatus rolledBack = (await host.ReadAsync(request)).Status;
        await ChangeOnHostAsync(host, database, request, commit: true);

        Assert.Equal((RequestStatus.Approved, RequestStatus.Void), (rolledBack, (await host.ReadAsync(request)).Status));
        host.AssertNoLogHoldsAValueUsed();
    }

    private static async Task ChangeOnHostAsync(RequestHost host, NpgsqlConnection database, RequestId request, bool commit)
    {
        await using NpgsqlTransaction transaction = await database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        Result<IReadOnlyList<RequestId>> changed = await host.RecordChangeAsync(Requests.Target, Amount, C, transaction);
        Assert.Equal([request], changed.Value!);
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