using System;
using System.Text;
using System.Threading.Tasks;

using Themis.Core;

using Xunit;

namespace Themis.Testing.Tests;

/// <summary>The recording observer and the fixed secrets a host's tests use (<c>03</c> APR-LIB-007).</summary>
[Trait("Category", "unit")]
public sealed class RecordingObserverTests
{
    [Fact]
    public async Task ObserveAsync_Events_AreReceivedInTheOrderTakenAndOneItFailsOnIsNot()
    {
        RecordingObserver observer = new();
        ApprovalEvent first = Event(1);
        ApprovalEvent second = Event(2);
        observer.FailWith(approvalEvent => approvalEvent.Sequence == 2);

        await observer.ObserveAsync(first, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await observer.ObserveAsync(second, TestContext.Current.CancellationToken));
        observer.FailWith(_ => false);
        await observer.ObserveAsync(second, TestContext.Current.CancellationToken);

        Assert.Equal([first, second], observer.Received);
    }

    [Fact]
    public async Task FixedSecretSource_Secret_IsAnsweredOnceSetAndNotOnceRemoved()
    {
        FixedSecretSource secrets = new();
        SecretVersions connection = new() { Versions = [new SecretVersion { Version = 1, Value = Encoding.UTF8.GetBytes("Host=db") }] };

        SecretVersions? before = await secrets.ReadAsync("connection", TestContext.Current.CancellationToken);
        secrets.SetSecret("connection", connection);
        SecretVersions? set = await secrets.ReadAsync("connection", TestContext.Current.CancellationToken);
        secrets.RemoveSecret("connection");

        Assert.Null(before);
        Assert.Same(connection, set);
        Assert.Null(await secrets.ReadAsync("connection", TestContext.Current.CancellationToken));
    }

    private static ApprovalEvent Event(long sequence)
    {
        return new ApprovalEvent { Event = new ApprovalEventId(Guid.CreateVersion7()), Kind = EventKind.Requested, At = DateTimeOffset.UnixEpoch, Sequence = sequence };
    }
}