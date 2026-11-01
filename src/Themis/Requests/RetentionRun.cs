using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using NpgsqlTypes;

using Themis.Operations;

namespace Themis.Requests;

/// <summary>
/// The <c>retention</c> job's run (<c>01</c> APR-REQ-015; <c>04</c> APR-OPS-004): where
/// <c>Themis:Retention:ClosedRequests</c> is set, it deletes each closed request whose closing
/// is older than the period, with its steps, assignments, decisions and events, and each ended
/// delegation whose end is older than the period, with its event. A request goes with every
/// request linked to it through <c>previous</c>, in one transaction, once each of them is closed
/// past the period and every event of each has been taken; a delegation goes once its event has
/// been taken. At most 1,000 requests go in one run, a chain that does not fit what is left
/// waiting for the next unless it alone holds more, when it goes alone in a run, and at most
/// 1,000 delegations; a row a host's transaction holds is left for a later run. Where the
/// period is unset, nothing is read or deleted; where the session lacks the retention grant, the run fails naming
/// <c>approvals.retention.notgranted</c>.
/// </summary>
internal sealed class RetentionRun(ApprovalsDatabase database, ApprovalsOptions options, ApprovalsMetrics metrics, TimeProvider clock)
{
    /// <summary>The most requests, and the most delegations, one run deletes.</summary>
    internal const int MostPerRun = 1_000;

    /// <summary>A request closed past the period, <c>@cutoff</c>, none of whose events waits for the observer.</summary>
    private const string Due = """
        r.status IN ('completed', 'rejected', 'cancelled', 'void') AND r.closed_at < @cutoff
            AND NOT EXISTS (SELECT 1 FROM approvals.events e WHERE e.request = r.id AND e.taken_at IS NULL)
        """;

    /// <summary>The requests linked to <c>@id</c> through <c>previous</c>, either way, with it.</summary>
    private const string Chain = """
        WITH RECURSIVE chain(id) AS (
            SELECT @id::uuid
            UNION
            SELECT r.id
            FROM chain c
            JOIN approvals.requests p ON p.id = c.id
            JOIN approvals.requests r ON r.previous = c.id OR r.id = p.previous)
        SELECT id AS "Value" FROM chain
        """;

    /// <summary>Runs the deletions due, renewing <paramref name="claim"/> after each transaction, and answers the number of requests and delegations deleted.</summary>
    internal async Task<int> RunAsync(JobClaim claim, CancellationToken cancellationToken)
    {
        if (options.Retention.ClosedRequests is not TimeSpan period)
        {
            return 0;
        }

        DateTimeOffset cutoff = clock.GetUtcNow() - period;
        await CheckGrantAsync(cancellationToken).ConfigureAwait(false);
        (int requests, bool held) = await RequestsAsync(claim, cutoff, cancellationToken).ConfigureAwait(false);
        return held ? requests + await DelegationsAsync(claim, cutoff, cancellationToken).ConfigureAwait(false) : requests;
    }

    private static NpgsqlParameter Instant(string name, DateTimeOffset value)
    {
        return new NpgsqlParameter(name, NpgsqlDbType.TimestampTz) { Value = value.UtcDateTime };
    }

    private async Task CheckGrantAsync(CancellationToken cancellationToken)
    {
        ApprovalsContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            if (!await RetentionGrant.HeldAsync(context, cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException($"{Codes.RetentionNotGranted}: the application's role holds no DELETE on the request and delegation tables; grant it the role {RetentionGrant.Role}.");
            }
        }
    }

    /// <summary>Deletes the requests due, and answers their number and whether the claim is still the run's.</summary>
    private async Task<(int Deleted, bool Held)> RequestsAsync(JobClaim claim, DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        HashSet<Guid> tried = [];
        int deleted = 0;
        while (deleted < MostPerRun)
        {
            List<Guid> due = await DueRequestsAsync(cutoff, tried, MostPerRun - deleted, cancellationToken).ConfigureAwait(false);
            if (due.Count == 0)
            {
                return (deleted, true);
            }

            foreach (Guid request in due.Where(request => !tried.Contains(request)))
            {
                (IReadOnlyList<Guid> chain, bool gone) = await LibraryTransactions.RunAsync(database, options, (context, cancel) => DeleteChainAsync(context, request, cutoff, deleted, cancel), cancellationToken).ConfigureAwait(false);
                tried.UnionWith(chain);
                tried.Add(request);
                if (gone)
                {
                    deleted += chain.Count;
                    metrics.RetentionDeleted.Add(chain.Count, new KeyValuePair<string, object?>("kind", "request"));
                }

                if (!await claim.RenewAsync(cancellationToken).ConfigureAwait(false))
                {
                    return (deleted, false);
                }

                if (deleted >= MostPerRun)
                {
                    return (deleted, true);
                }
            }
        }

        return (deleted, true);
    }

    /// <summary>The next requests due by themselves, oldest closing first, not yet tried in this run.</summary>
    private async Task<List<Guid>> DueRequestsAsync(DateTimeOffset cutoff, HashSet<Guid> tried, int most, CancellationToken cancellationToken)
    {
        ApprovalsContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            return await context.Database
                .SqlQueryRaw<Guid>(
                    $"SELECT r.id AS \"Value\" FROM approvals.requests r WHERE {Due} AND NOT (r.id = ANY(@tried)) ORDER BY r.closed_at, r.id LIMIT @most",
                    Instant("cutoff", cutoff),
                    new NpgsqlParameter("tried", tried.ToArray()),
                    new NpgsqlParameter("most", most))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Deletes the chain of <paramref name="request"/> where each of its requests is due and none
    /// is held by another transaction, and the run has room for it; answers the chain as read and
    /// whether it was deleted, so the run does not try its requests again either way.
    /// </summary>
    private static async Task<(IReadOnlyList<Guid> Chain, bool Deleted)> DeleteChainAsync(ApprovalsContext context, Guid request, DateTimeOffset cutoff, int deleted, CancellationToken cancellationToken)
    {
        List<Guid> chain = await ChainAsync(context, request, cancellationToken).ConfigureAwait(false);
        Guid[] ids = [.. chain];
        List<Guid> locked = await context.Database
            .SqlQueryRaw<Guid>("SELECT id AS \"Value\" FROM approvals.requests WHERE id = ANY(@ids) FOR UPDATE SKIP LOCKED", new NpgsqlParameter("ids", ids))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        List<Guid> again = await ChainAsync(context, request, cancellationToken).ConfigureAwait(false);
        bool room = deleted + ids.Length <= MostPerRun || deleted == 0;
        if (!room || locked.Count != ids.Length || !again.ToHashSet().SetEquals(ids) || await DueCountAsync(context, ids, cutoff, cancellationToken).ConfigureAwait(false) != ids.Length)
        {
            return (ids, false);
        }

        NpgsqlParameter[] Ids() => [new NpgsqlParameter("ids", ids)];
        await context.Database.ExecuteSqlRawAsync("DELETE FROM approvals.assignments WHERE request = ANY(@ids)", Ids(), cancellationToken).ConfigureAwait(false);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM approvals.decisions WHERE request = ANY(@ids)", Ids(), cancellationToken).ConfigureAwait(false);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM approvals.events WHERE request = ANY(@ids)", Ids(), cancellationToken).ConfigureAwait(false);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM approvals.steps WHERE request = ANY(@ids)", Ids(), cancellationToken).ConfigureAwait(false);

        // Each statement deletes the requests no remaining request names as its previous, the chain's later ones first.
        int left = ids.Length;
        while (left > 0)
        {
            int gone = await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM approvals.requests r WHERE r.id = ANY(@ids) AND NOT EXISTS (SELECT 1 FROM approvals.requests l WHERE l.previous = r.id)",
                Ids(),
                cancellationToken).ConfigureAwait(false);
            if (gone == 0)
            {
                throw new InvalidOperationException("A chain of requests through previous holds a cycle.");
            }

            left -= gone;
        }

        return (ids, true);
    }

    private static Task<List<Guid>> ChainAsync(ApprovalsContext context, Guid request, CancellationToken cancellationToken)
    {
        return context.Database.SqlQueryRaw<Guid>(Chain, new NpgsqlParameter("id", request)).ToListAsync(cancellationToken);
    }

    private static async Task<int> DueCountAsync(ApprovalsContext context, Guid[] ids, DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        List<int> count = await context.Database
            .SqlQueryRaw<int>($"SELECT count(*)::int AS \"Value\" FROM approvals.requests r WHERE r.id = ANY(@ids) AND {Due}", new NpgsqlParameter("ids", ids), Instant("cutoff", cutoff))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return count.Single();
    }

    /// <summary>Deletes the delegations due, and answers their number; it stops where the claim is no longer the run's.</summary>
    private async Task<int> DelegationsAsync(JobClaim claim, DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        List<Guid> due;
        ApprovalsContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            due = await context.Database
                .SqlQueryRaw<Guid>(
                    "SELECT d.id AS \"Value\" FROM approvals.delegations d WHERE LEAST(COALESCE(d.ended_at, d.ends), d.ends) < @cutoff ORDER BY LEAST(COALESCE(d.ended_at, d.ends), d.ends), d.id LIMIT @most",
                    Instant("cutoff", cutoff),
                    new NpgsqlParameter("most", MostPerRun))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        int deleted = 0;
        foreach (Guid delegation in due)
        {
            bool gone = await LibraryTransactions.RunAsync(database, options, (context, cancel) => DeleteDelegationAsync(context, delegation, cutoff, cancel), cancellationToken).ConfigureAwait(false);
            if (gone)
            {
                deleted++;
                metrics.RetentionDeleted.Add(1, new KeyValuePair<string, object?>("kind", "delegation"));
            }

            if (!await claim.RenewAsync(cancellationToken).ConfigureAwait(false))
            {
                return deleted;
            }
        }

        return deleted;
    }

    /// <summary>Deletes the delegation and its event where it ended past the period, its event has been taken, no request's event names it, and no other transaction holds it.</summary>
    private static async Task<bool> DeleteDelegationAsync(ApprovalsContext context, Guid delegation, DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        List<Guid> locked = await context.Database
            .SqlQueryRaw<Guid>(
                """
                SELECT d.id AS "Value" FROM approvals.delegations d
                WHERE d.id = @id AND LEAST(COALESCE(d.ended_at, d.ends), d.ends) < @cutoff
                    AND NOT EXISTS (SELECT 1 FROM approvals.events e WHERE e.delegation = d.id AND (e.taken_at IS NULL OR e.request IS NOT NULL))
                FOR UPDATE SKIP LOCKED
                """,
                new NpgsqlParameter("id", delegation),
                Instant("cutoff", cutoff))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (locked.Count == 0)
        {
            return false;
        }

        await context.Database.ExecuteSqlRawAsync("DELETE FROM approvals.events WHERE delegation = @id AND request IS NULL", [new NpgsqlParameter("id", delegation)], cancellationToken).ConfigureAwait(false);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM approvals.delegations WHERE id = @id", [new NpgsqlParameter("id", delegation)], cancellationToken).ConfigureAwait(false);
        return true;
    }
}