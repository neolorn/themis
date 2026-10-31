using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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
/// The <c>watch</c> job's run (<c>04</c> APR-OPS-006; <c>06</c> section 5): it judges
/// <c>observer-failing</c>, each type's <c>request-unassignable</c> and each job's
/// <c>job-stalled</c>, raising a condition through the alert sink when it starts to hold, again
/// each time <c>Themis:Alerts:Repeat</c> has passed since it was last raised while it holds,
/// and clearing it once when it stops, each call bounded by <c>Themis:Timeout</c> and its state
/// kept in the alert table. A call that fails leaves the state as it was, for the next run.
/// </summary>
internal sealed partial class AlertWatch(ApprovalsDatabase database, ApprovalsOptions options, IAlertSink sink, ApprovalsMetrics metrics, TimeProvider clock, ILogger logger)
{
    internal const string ObserverFailing = "observer-failing";

    internal const string RequestUnassignable = "request-unassignable";

    /// <summary>Judges the conditions, renewing <paramref name="claim"/> after each, and answers the number raised or cleared.</summary>
    internal async Task<int> RunAsync(JobClaim claim, CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.GetUtcNow();
        List<AlertState> states;
        (DateTimeOffset? Oldest, long Pending) outbox;
        List<JobRow> jobs;
        ApprovalsContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            states = await context.Alerts.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
            outbox = await OutboxAsync(context, cancellationToken).ConfigureAwait(false);
            jobs = await context.Jobs.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        metrics.Pending(outbox.Pending);
        int handled = 0;
        bool failing = options.Observer.Enabled && outbox.Oldest is DateTimeOffset oldest && now - oldest > options.Observer.LagThreshold;
        Dictionary<string, string> lag = outbox.Oldest is DateTimeOffset written ? new() { ["oldest"] = written.ToString("O", CultureInfo.InvariantCulture) } : [];
        handled += await JudgeAsync(ObserverFailing, string.Empty, states.SingleOrDefault(state => state.Condition == ObserverFailing), failing, lag, now, cancellationToken).ConfigureAwait(false);
        foreach (AlertState state in states.Where(state => state.Condition == RequestUnassignable).OrderBy(state => state.Scope, StringComparer.Ordinal))
        {
            if (!await claim.RenewAsync(cancellationToken).ConfigureAwait(false))
            {
                return handled;
            }

            bool holds = state.LastOccurredAt is DateTimeOffset occurred && now - occurred < options.Alerts.Repeat;
            Dictionary<string, string> details = new()
            {
                ["request"] = state.Request?.ToString("D") ?? string.Empty,
                ["step"] = state.Step?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            };
            handled += await JudgeAsync(RequestUnassignable, state.Scope, state, holds, details, now, cancellationToken).ConfigureAwait(false);
        }

        foreach (JobRow job in jobs.OrderBy(job => Spelling<ApprovalJob>.Write(job.Name), StringComparer.Ordinal))
        {
            if (!await claim.RenewAsync(cancellationToken).ConfigureAwait(false))
            {
                return handled;
            }

            Dictionary<string, string> last = job.LastCompletedAt is DateTimeOffset completed ? new() { ["lastCompleted"] = completed.ToString("O", CultureInfo.InvariantCulture) } : [];
            string scope = Spelling<ApprovalJob>.Write(job.Name);
            handled += await JudgeAsync(JobStall.Condition, scope, states.SingleOrDefault(state => state.Condition == JobStall.Condition && state.Scope == scope), JobStall.Stalled(job, options, now), last, now, cancellationToken).ConfigureAwait(false);
        }

        return handled;
    }

    /// <summary>The instant the oldest event not yet taken was written, or none, and the number not yet taken.</summary>
    internal static async Task<(DateTimeOffset? Oldest, long Pending)> OutboxAsync(ApprovalsContext context, CancellationToken cancellationToken)
    {
        var outbox = await context.Events
            .Where(row => row.TakenAt == null)
            .GroupBy(_ => 1)
            .Select(group => new { Oldest = group.Min(row => (DateTimeOffset?)row.At), Pending = group.LongCount() })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return (outbox?.Oldest, outbox?.Pending ?? 0);
    }

    private async Task<int> JudgeAsync(string condition, string scope, AlertState? state, bool holds, Dictionary<string, string> details, DateTimeOffset now, CancellationToken cancellationToken)
    {
        bool holding = state?.Holding ?? false;
        string? named = scope.Length == 0 ? null : scope;
        if (holds && (!holding || state?.LastRaisedAt is not DateTimeOffset raised || now - raised >= options.Alerts.Repeat))
        {
            if (!await CallAsync(condition, () => sink.RaiseAsync(condition, named, details, cancellationToken).AsTask(), cancellationToken).ConfigureAwait(false))
            {
                return 0;
            }

            await WriteAsync(
                """
                INSERT INTO approvals.alerts (condition, scope, holding, last_raised_at) VALUES (@condition, @scope, true, @now)
                ON CONFLICT (condition, scope) DO UPDATE SET holding = true, last_raised_at = @now
                """,
                condition,
                scope,
                now,
                cancellationToken).ConfigureAwait(false);
            return 1;
        }

        if (!holds && holding)
        {
            if (!await CallAsync(condition, () => sink.ClearAsync(condition, named, cancellationToken).AsTask(), cancellationToken).ConfigureAwait(false))
            {
                return 0;
            }

            await WriteAsync("UPDATE approvals.alerts SET holding = false WHERE condition = @condition AND scope = @scope", condition, scope, now, cancellationToken).ConfigureAwait(false);
            return 1;
        }

        return 0;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "The alert sink is the host's code: whatever it throws leaves the condition's state as it was, for the next run (04 APR-OPS-006).")]
    private async Task<bool> CallAsync(string condition, Func<Task> call, CancellationToken cancellationToken)
    {
        try
        {
            await call().WaitAsync(options.Timeout, clock, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception failure)
        {
            string thrown = failure.GetType().Name;
            SinkFailed(condition, thrown);
            return false;
        }
    }

    private Task<int> WriteAsync(string sql, string condition, string scope, DateTimeOffset now, CancellationToken cancellationToken)
    {
        return LibraryTransactions.RunAsync(
            database,
            options,
            (context, cancel) => context.Database.ExecuteSqlRawAsync(sql, [new NpgsqlParameter("condition", condition), new NpgsqlParameter("scope", scope), new NpgsqlParameter("now", now)], cancel),
            cancellationToken);
    }

    [LoggerMessage(EventId = 3, EventName = "AlertSinkFailed", Level = LogLevel.Warning, Message = "The alert sink did not take a call for {Condition} ({Thrown}); the condition is judged again at the next run.")]
    private partial void SinkFailed(string condition, string thrown);
}