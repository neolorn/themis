using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Npgsql;

using Themis.Core;
using Themis.Requests;

namespace Themis.Operations;

/// <summary>
/// The <c>observer</c> job's run (<c>01</c> APR-REQ-014; <c>04</c> APR-OPS-004): for each
/// request in turn, the lowest event not yet taken is told to the host's observer, each call
/// bounded by <c>Themis:Timeout</c>, and marked taken when the call returns without throwing; a
/// request whose call fails waits for the next run while the others go on.
/// </summary>
internal sealed partial class ObserverDelivery(ApprovalsDatabase database, ApprovalsOptions options, IApprovalObserver observer, ApprovalsMetrics metrics, TimeProvider clock, ILogger logger)
{
    /// <summary>Delivers the events not yet taken, renewing <paramref name="claim"/> after each, and answers the number taken.</summary>
    internal async Task<int> RunAsync(JobClaim claim, CancellationToken cancellationToken)
    {
        List<Guid> failed = [];
        int taken = 0;
        while (true)
        {
            List<EventRow> next = await NextAsync(failed, cancellationToken).ConfigureAwait(false);
            if (next.Count == 0)
            {
                return taken;
            }

            foreach (EventRow row in next)
            {
                if (!await DeliverAsync(row, cancellationToken).ConfigureAwait(false))
                {
                    failed.Add(row.Request ?? row.Id);
                    continue;
                }

                await TakeAsync(row.Id, cancellationToken).ConfigureAwait(false);
                taken++;
                string kind = Spelling<EventKind>.Write(row.Kind);
                metrics.EventsDelivered.Add(1, new KeyValuePair<string, object?>("kind", kind));
                if (!await claim.RenewAsync(cancellationToken).ConfigureAwait(false))
                {
                    return taken;
                }
            }
        }
    }

    /// <summary>The event the host's observer is told, from its row.</summary>
    internal static ApprovalEvent Event(EventRow row)
    {
        return new ApprovalEvent
        {
            Event = new ApprovalEventId(row.Id),
            Kind = row.Kind,
            At = row.At,
            Sequence = row.Sequence,
            Request = row.Request is Guid request ? new RequestId(request) : null,
            Type = row.Type,
            Version = row.Version,
            Target = row.TargetKind is not null && row.TargetId is not null ? new Target { Kind = row.TargetKind, Id = row.TargetId } : null,
            Step = row.Step,
            Actor = row.Actor,
            ActingFor = row.ActingFor,
            ReasonCode = row.ReasonCode,
            Delegation = row.Delegation is Guid delegation ? new DelegationId(delegation) : null,
            Notify = row.Notify,
        };
    }

    /// <summary>The lowest event not yet taken of each request but those failed in this run, the oldest first, at most <c>Themis:Jobs:BatchSize</c>.</summary>
    private async Task<List<EventRow>> NextAsync(List<Guid> failed, CancellationToken cancellationToken)
    {
        ApprovalsContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            return await context.Events
                .FromSqlRaw("SELECT DISTINCT ON (coalesce(request, id)) * FROM approvals.events WHERE taken_at IS NULL ORDER BY coalesce(request, id), sequence")
                .Where(row => !failed.Contains(row.Request ?? row.Id))
                .OrderBy(row => row.At)
                .ThenBy(row => row.Id)
                .Take(options.Jobs.BatchSize)
                .AsNoTracking()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "The observer is the host's code: whatever it throws leaves the event to be told again (01 APR-REQ-014).")]
    private async Task<bool> DeliverAsync(EventRow row, CancellationToken cancellationToken)
    {
        try
        {
            await observer.ObserveAsync(Event(row), cancellationToken).AsTask().WaitAsync(options.Timeout, clock, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception failure)
        {
            string thrown = failure.GetType().Name;
            NotTaken(row.Id, row.Request, row.Sequence, thrown);
            return false;
        }
    }

    private async Task TakeAsync(Guid id, CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.GetUtcNow();
        await LibraryTransactions.RunAsync(
            database,
            options,
            (context, cancel) => context.Database.ExecuteSqlRawAsync(
                "UPDATE approvals.events SET taken_at = @now WHERE id = @id AND taken_at IS NULL",
                [new NpgsqlParameter("now", now), new NpgsqlParameter("id", id)],
                cancel),
            cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(EventId = 2, EventName = "EventNotTaken", Level = LogLevel.Warning, Message = "Event {Event} of request {Request}, number {Sequence}, was not taken by the observer ({Thrown}); its request waits for the next run.")]
    private partial void NotTaken(Guid @event, Guid? request, long sequence, string thrown);
}