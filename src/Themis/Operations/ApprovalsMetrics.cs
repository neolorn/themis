using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;

namespace Themis.Operations;

/// <summary>
/// The meter <c>Themis</c> and its counters (<c>04</c> APR-OPS-005; <c>06</c> section 7), made
/// by the host's <see cref="IMeterFactory"/> where it registers one. The gauge
/// <c>approvals.events.pending</c> answers the number of events not yet taken as the last
/// <c>watch</c> run counted them, and nothing before the first.
/// </summary>
internal sealed class ApprovalsMetrics : IDisposable
{
    internal const string MeterName = "Themis";

    private readonly Meter meter;

    private readonly bool owned;

    private long pending = -1;

    public ApprovalsMetrics(IMeterFactory? factory)
    {
        owned = factory is null;
        meter = factory?.Create(MeterName) ?? new Meter(MeterName);
        RequestsCreated = meter.CreateCounter<long>("approvals.requests.created", "{request}", "Each request created");
        RequestsClosed = meter.CreateCounter<long>("approvals.requests.closed", "{request}", "Each request reaching a final status");
        Decisions = meter.CreateCounter<long>("approvals.decisions", "{decision}", "Each decision recorded");
        EventsDelivered = meter.CreateCounter<long>("approvals.events.delivered", "{event}", "Each event the observer took");
        RetentionDeleted = meter.CreateCounter<long>("approvals.retention.deleted", "{record}", "Each request or delegation the retention job deleted");
        Erasures = meter.CreateCounter<long>("approvals.erasures", "{erasure}", "Each erasure that changed a row");
        meter.CreateObservableGauge("approvals.events.pending", Observe, "{event}", "The events not yet taken");
    }

    /// <summary>Tagged <c>type</c>.</summary>
    internal Counter<long> RequestsCreated { get; }

    /// <summary>Tagged <c>type</c> and <c>status</c>.</summary>
    internal Counter<long> RequestsClosed { get; }

    /// <summary>Tagged <c>type</c> and <c>outcome</c>.</summary>
    internal Counter<long> Decisions { get; }

    /// <summary>Tagged <c>kind</c>.</summary>
    internal Counter<long> EventsDelivered { get; }

    /// <summary>Tagged <c>kind</c>, <c>request</c> or <c>delegation</c>.</summary>
    internal Counter<long> RetentionDeleted { get; }

    /// <summary>Untagged.</summary>
    internal Counter<long> Erasures { get; }

    /// <summary>Sets the number of events not yet taken, as a <c>watch</c> run counted them.</summary>
    internal void Pending(long count)
    {
        Interlocked.Exchange(ref pending, count);
    }

    public void Dispose()
    {
        // A meter the host's factory made is the factory's to dispose.
        if (owned)
        {
            meter.Dispose();
        }
    }

    private IEnumerable<Measurement<long>> Observe()
    {
        long count = Interlocked.Read(ref pending);
        return count < 0 ? [] : [new Measurement<long>(count)];
    }
}