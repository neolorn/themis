using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using Themis.Core;

namespace Themis.Tests;

/// <summary>An observer that keeps every event it is told, in order.</summary>
internal sealed class ApprovalObserverFake : IApprovalObserver
{
    private readonly ConcurrentQueue<ApprovalEvent> received = new();

    internal ApprovalEvent[] Received => [.. received];

    public ValueTask ObserveAsync(ApprovalEvent approvalEvent, CancellationToken cancellationToken)
    {
        received.Enqueue(approvalEvent);
        return ValueTask.CompletedTask;
    }
}