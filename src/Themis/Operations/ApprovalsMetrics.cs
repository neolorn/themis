using System;
using System.Diagnostics.Metrics;

namespace Themis.Operations;

/// <summary>
/// The meter <c>Themis</c> and its counters (<c>04</c> APR-OPS-005; <c>06</c> section 7), made
/// by the host's <see cref="IMeterFactory"/> where it registers one. The gauge
/// <c>approvals.events.pending</c> comes with the outbox it observes.
/// </summary>
internal sealed class ApprovalsMetrics : IDisposable
{
    internal const string MeterName = "Themis";

    private readonly Meter meter;

    private readonly bool owned;

    public ApprovalsMetrics(IMeterFactory? factory)
    {
        owned = factory is null;
        meter = factory?.Create(MeterName) ?? new Meter(MeterName);
        RequestsCreated = meter.CreateCounter<long>("approvals.requests.created", "{request}", "Each request created");
        RequestsClosed = meter.CreateCounter<long>("approvals.requests.closed", "{request}", "Each request reaching a final status");
        Decisions = meter.CreateCounter<long>("approvals.decisions", "{decision}", "Each decision recorded");
        EventsDelivered = meter.CreateCounter<long>("approvals.events.delivered", "{event}", "Each event the observer took");
    }

    /// <summary>Tagged <c>type</c>.</summary>
    internal Counter<long> RequestsCreated { get; }

    /// <summary>Tagged <c>type</c> and <c>status</c>.</summary>
    internal Counter<long> RequestsClosed { get; }

    /// <summary>Tagged <c>type</c> and <c>outcome</c>.</summary>
    internal Counter<long> Decisions { get; }

    /// <summary>Tagged <c>kind</c>.</summary>
    internal Counter<long> EventsDelivered { get; }

    public void Dispose()
    {
        // A meter the host's factory made is the factory's to dispose.
        if (owned)
        {
            meter.Dispose();
        }
    }
}