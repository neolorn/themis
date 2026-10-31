using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Themis.Core;

namespace Themis.Testing;

/// <summary>An <see cref="IApprovalObserver"/> that records each event it takes, in the order taken (<c>03</c> APR-LIB-007).</summary>
public sealed class RecordingObserver : IApprovalObserver
{
    private readonly Lock gate = new();

    private readonly List<ApprovalEvent> received = [];

    private Func<ApprovalEvent, bool>? fails;

    /// <summary>The events taken, in the order taken.</summary>
    public IReadOnlyList<ApprovalEvent> Received
    {
        get
        {
            lock (gate)
            {
                return [.. received];
            }
        }
    }

    /// <summary>Makes the observer throw on each event <paramref name="fails"/> matches, which is then not taken.</summary>
    /// <param name="fails">Whether the observer fails on an event.</param>
    /// <exception cref="ArgumentNullException"><paramref name="fails"/> is null.</exception>
    public void FailWith(Func<ApprovalEvent, bool> fails)
    {
        ArgumentNullException.ThrowIfNull(fails);
        lock (gate)
        {
            this.fails = fails;
        }
    }

    /// <summary>Takes <paramref name="approvalEvent"/>, or throws where <see cref="FailWith"/> matches it.</summary>
    /// <param name="approvalEvent">The event.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes once the event is taken.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="approvalEvent"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The event is one the observer is set to fail on.</exception>
    public ValueTask ObserveAsync(ApprovalEvent approvalEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(approvalEvent);
        lock (gate)
        {
            if (fails is not null && fails(approvalEvent))
            {
                throw new InvalidOperationException("The recording observer is set to fail on this event.");
            }

            received.Add(approvalEvent);
        }

        return ValueTask.CompletedTask;
    }
}