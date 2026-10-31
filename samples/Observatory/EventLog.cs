using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Themis.Core;

namespace Observatory;

/// <summary>
/// The observatory's observer: it logs each event's kind, request and step, and nothing that
/// names a person, since a host's notifications would read the persons to notify from the
/// event's <see cref="ApprovalEvent.Notify"/> and send them elsewhere.
/// </summary>
internal sealed partial class EventLog(ILogger<EventLog> logger) : IApprovalObserver
{
    public ValueTask ObserveAsync(ApprovalEvent approvalEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(approvalEvent);
        Observed(logger, approvalEvent.Kind, approvalEvent.Request?.Value, approvalEvent.Step);
        return ValueTask.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "The event {Kind} of the request {Request} at step {Step}.")]
    private static partial void Observed(ILogger logger, EventKind kind, Guid? request, int? step);
}