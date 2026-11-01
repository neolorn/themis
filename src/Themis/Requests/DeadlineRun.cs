using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Themis.Core;
using Themis.Operations;

namespace Themis.Requests;

/// <summary>
/// The <c>deadlines</c> job's run (<c>01</c> APR-REQ-009; <c>04</c> APR-OPS-004): the current
/// steps whose deadline has passed and whose action has not run, oldest due first, in batches of
/// <c>Themis:Jobs:BatchSize</c>, each in a transaction of its own under its request's lock, taken
/// <c>FOR UPDATE SKIP LOCKED</c> so a request a host's transaction holds is left for a later run.
/// A step whose directory call fails is left for the next run; the others go on. Before the actions,
/// it sends the reminders due in the same way, one for each step whose reminder has fallen due,
/// while its deadline is ahead (<c>01</c> APR-REQ-009, T-025).
/// </summary>
internal sealed class DeadlineRun(ApprovalsDatabase database, ApprovalsOptions options, IApproverDirectory directory, ApprovalsMetrics metrics, TimeProvider clock)
{
    /// <summary>Sends the reminders due, then runs the actions due, renewing <paramref name="claim"/> after each step taken, and answers the number of reminders sent and actions run.</summary>
    internal async Task<int> RunAsync(JobClaim claim, CancellationToken cancellationToken)
    {
        (int handled, bool held) = await RemindAsync(claim, cancellationToken).ConfigureAwait(false);
        if (!held)
        {
            return handled;
        }

        List<Guid> tried = [];
        while (true)
        {
            List<(Guid Request, int Step)> due = await DueAsync(tried, cancellationToken).ConfigureAwait(false);
            if (due.Count == 0)
            {
                return handled;
            }

            foreach ((Guid request, int step) in due)
            {
                tried.Add(request);
                if (await ActAsync(request, step, cancellationToken).ConfigureAwait(false))
                {
                    handled++;
                }

                if (!await claim.RenewAsync(cancellationToken).ConfigureAwait(false))
                {
                    return handled;
                }
            }
        }
    }

    /// <summary>Sends the reminders due, and answers their number and whether the claim is still the run's.</summary>
    private async Task<(int Sent, bool Held)> RemindAsync(JobClaim claim, CancellationToken cancellationToken)
    {
        List<Guid> tried = [];
        int sent = 0;
        while (true)
        {
            List<(Guid Request, int Step)> due = await RemindersDueAsync(tried, cancellationToken).ConfigureAwait(false);
            if (due.Count == 0)
            {
                return (sent, true);
            }

            foreach ((Guid request, int step) in due)
            {
                tried.Add(request);
                RequestMoves? moves = await LibraryTransactions.RunAsync(database, options, (context, cancel) => RemindInAsync(context, request, step, cancel), cancellationToken).ConfigureAwait(false);
                if (moves is not null)
                {
                    moves.Count();
                    sent++;
                }

                if (!await claim.RenewAsync(cancellationToken).ConfigureAwait(false))
                {
                    return (sent, false);
                }
            }
        }
    }

    /// <summary>The next batch of steps whose next reminder has fallen due, earliest first, of requests not yet tried in this run.</summary>
    private async Task<List<(Guid Request, int Step)>> RemindersDueAsync(List<Guid> tried, CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.GetUtcNow();
        ApprovalsContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            var due = await context.Steps.AsNoTracking()
                .Where(step => step.Status == StepStatus.Current && step.DeadlineRanAt == null && step.RemindAt <= now && !tried.Contains(step.Request))
                .OrderBy(step => step.RemindAt)
                .ThenBy(step => step.Request)
                .Take(options.Jobs.BatchSize)
                .Select(step => new { step.Request, step.Number })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            return [.. due.Select(step => (step.Request, step.Number))];
        }
    }

    /// <summary>Sends the reminder due of the request's step <paramref name="number"/>, or passes those left where its deadline has passed; answers the moves where a reminder was sent, and none where the request is locked, has moved on, or nothing was sent.</summary>
    private async Task<RequestMoves?> RemindInAsync(ApprovalsContext context, Guid id, int number, CancellationToken cancellationToken)
    {
        RequestRow? request = await context.Requests
            .FromSqlRaw("SELECT * FROM approvals.requests WHERE id = @id FOR UPDATE SKIP LOCKED", new NpgsqlParameter("id", id))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        RequestMoves moves = new(context, new DirectoryCalls(directory, options.Timeout, clock), metrics, clock.GetUtcNow());
        StepRow? step = request is null ? null : await context.Steps.SingleAsync(row => row.Request == id && row.Number == number, cancellationToken).ConfigureAwait(false);
        if (request is null || step is null || request.Status != RequestStatus.Pending || step.Status != StepStatus.Current || step.DeadlineRanAt is not null || step.RemindAt is null || step.RemindAt > moves.Now)
        {
            return null;
        }

        ApprovalTypeDefinition definition = await ApprovalService.DefinitionAsync(context, request, cancellationToken).ConfigureAwait(false);
        StepDefinition defined = ApprovalService.StepsOf(definition, request)[number - 1];
        bool sent = await moves.RemindAsync(request, definition, defined, step, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return sent ? moves : null;
    }

    /// <summary>The next batch of steps due, oldest due first, of requests not yet tried in this run.</summary>
    private async Task<List<(Guid Request, int Step)>> DueAsync(List<Guid> tried, CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.GetUtcNow();
        ApprovalsContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            var due = await context.Steps.AsNoTracking()
                .Where(step => step.Status == StepStatus.Current && step.DeadlineRanAt == null && step.DueAt <= now && !tried.Contains(step.Request))
                .OrderBy(step => step.DueAt)
                .ThenBy(step => step.Request)
                .Take(options.Jobs.BatchSize)
                .Select(step => new { step.Request, step.Number })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            return [.. due.Select(step => (step.Request, step.Number))];
        }
    }

    /// <summary>Runs the deadline's action of the request's step <paramref name="number"/>, and answers whether it ran: not where the request is locked, has moved on, or a directory call failed.</summary>
    private async Task<bool> ActAsync(Guid id, int number, CancellationToken cancellationToken)
    {
        try
        {
            RequestMoves? moves = await LibraryTransactions.RunAsync(database, options, (context, cancel) => ActInAsync(context, id, number, cancel), cancellationToken).ConfigureAwait(false);
            moves?.Count();
            return moves is not null;
        }
        catch (Refusal)
        {
            return false;
        }
    }

    private async Task<RequestMoves?> ActInAsync(ApprovalsContext context, Guid id, int number, CancellationToken cancellationToken)
    {
        RequestRow? request = await context.Requests
            .FromSqlRaw("SELECT * FROM approvals.requests WHERE id = @id FOR UPDATE SKIP LOCKED", new NpgsqlParameter("id", id))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        RequestMoves moves = new(context, new DirectoryCalls(directory, options.Timeout, clock), metrics, clock.GetUtcNow());
        StepRow? step = request is null ? null : await context.Steps.SingleAsync(row => row.Request == id && row.Number == number, cancellationToken).ConfigureAwait(false);
        if (request is null || step is null || request.Status != RequestStatus.Pending || step.Status != StepStatus.Current || step.DeadlineRanAt is not null || step.DueAt > moves.Now)
        {
            return null;
        }

        ApprovalTypeDefinition definition = await ApprovalService.DefinitionAsync(context, request, cancellationToken).ConfigureAwait(false);
        StepDefinition defined = ApprovalService.StepsOf(definition, request)[number - 1];
        step.DeadlineRanAt = moves.Now;
        step.RemindAt = null;
        switch (defined.Deadline?.Action)
        {
            case DeadlineAction.Reject:
                await moves.CloseAsync(request, definition, RequestStatus.Rejected, EventKind.Rejected, actor: null, actingFor: null, reason: null, ReasonCode.Deadline, cancellationToken).ConfigureAwait(false);
                break;
            case DeadlineAction.Cancel:
                await moves.CloseAsync(request, definition, RequestStatus.Cancelled, EventKind.Cancelled, actor: null, actingFor: null, reason: null, ReasonCode.Deadline, cancellationToken).ConfigureAwait(false);
                break;
            case DeadlineAction.Escalate:
                await moves.EscalateAsync(request, definition, defined, number, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException("A step due holds no deadline action of DeadlineAction.");
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return moves;
    }
}