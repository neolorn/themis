using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace Themis.Operations;

/// <summary>
/// The retention grant (<c>01</c> APR-REQ-015; <c>04</c> APR-OPS-002): the role
/// <c>approvals_retention</c> that <c>themis migrate</c> creates, holding <c>DELETE</c> on the
/// request and delegation tables. The retention job and an erasure run only where the session's
/// role holds that privilege on each of them, however it came to hold it.
/// </summary>
internal static class RetentionGrant
{
    internal const string Role = "approvals_retention";

    /// <summary>The request and delegation tables, the ones the role may delete from.</summary>
    internal static readonly string[] Tables = ["requests", "steps", "assignments", "decisions", "events", "delegations"];

    /// <summary>The person and reason columns and the snapshot an erasure rewrites, as <c>table.column</c>, on which the role holds <c>UPDATE</c>.</summary>
    internal static readonly string[] Columns =
    [
        "requests.requester", "requests.subject", "requests.closed_by", "requests.closed_for", "requests.reason", "requests.snapshot",
        "assignments.person", "assignments.via",
        "decisions.decider", "decisions.acting_for", "decisions.reason",
        "delegations.from_person", "delegations.to_person", "delegations.created_by", "delegations.ended_by",
        "events.actor", "events.acting_for", "events.notify",
    ];

    /// <summary>Whether the session of <paramref name="context"/> holds <c>DELETE</c> on each of <see cref="Tables"/>.</summary>
    internal static async Task<bool> HeldAsync(ApprovalsContext context, CancellationToken cancellationToken)
    {
        bool[] held = await context.Database
            .SqlQueryRaw<bool>("SELECT bool_and(has_table_privilege(format('approvals.%I', name), 'DELETE')) AS \"Value\" FROM unnest(@tables) AS name", new NpgsqlParameter("tables", Tables))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return held.SingleOrDefault();
    }

    /// <summary>Whether the session of <paramref name="context"/> holds, beside what <see cref="HeldAsync"/> asks, <c>UPDATE</c> on each of <see cref="Columns"/>.</summary>
    internal static async Task<bool> HeldForErasureAsync(ApprovalsContext context, CancellationToken cancellationToken)
    {
        bool[] held = await context.Database
            .SqlQueryRaw<bool>(
                "SELECT bool_and(has_column_privilege(format('approvals.%I', split_part(name, '.', 1)), split_part(name, '.', 2), 'UPDATE')) AS \"Value\" FROM unnest(@columns) AS name",
                new NpgsqlParameter("columns", Columns))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return held.SingleOrDefault() && await HeldAsync(context, cancellationToken).ConfigureAwait(false);
    }
}