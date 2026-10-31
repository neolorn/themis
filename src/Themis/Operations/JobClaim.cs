using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using NpgsqlTypes;

using Themis.Core;

namespace Themis.Operations;

/// <summary>
/// A run's claim on its job's row of the job table (<c>04</c> APR-OPS-004): taken by a
/// conditional update where the row is free or its claim has expired and, unless forced, its
/// last start is at least the job's interval, less one second, before now; renewed after each
/// item by an update conditional on the claim still being this run's; released when the run
/// ends, with the instant of completion where it completed. Every statement takes the instant
/// from the library's clock (<c>04</c> APR-OPS-003).
/// </summary>
internal sealed class JobClaim
{
    private readonly ApprovalsDatabase database;

    private readonly ApprovalsOptions options;

    private readonly TimeProvider clock;

    private readonly string name;

    private readonly string holder;

    private JobClaim(ApprovalsDatabase database, ApprovalsOptions options, TimeProvider clock, string name, string holder)
    {
        this.database = database;
        this.options = options;
        this.clock = clock;
        this.name = name;
        this.holder = holder;
    }

    /// <summary>Takes <paramref name="job"/>'s claim, whatever its interval where <paramref name="force"/> is set, or answers none where another run holds it or its interval has not passed.</summary>
    internal static async Task<JobClaim?> TakeAsync(ApprovalsDatabase database, ApprovalsOptions options, TimeProvider clock, ApprovalJob job, bool force, CancellationToken cancellationToken)
    {
        string name = Spelling<ApprovalJob>.Write(job);
        string holder = Guid.CreateVersion7().ToString("N");
        DateTime now = clock.GetUtcNow().UtcDateTime;
        int taken = await ExecuteAsync(
            database,
            options,
            """
            UPDATE approvals.jobs
            SET holder = @holder, claim_expires_at = @expires, last_started_at = @now, last_renewed_at = @now
            WHERE name = @name AND (holder IS NULL OR claim_expires_at <= @now)
                AND (@force OR last_started_at IS NULL OR last_started_at <= @started)
            """,
            () =>
            [
                new NpgsqlParameter("holder", holder),
                Instant("expires", now + options.Jobs.ClaimTimeout),
                Instant("now", now),
                new NpgsqlParameter("name", name),
                new NpgsqlParameter("force", NpgsqlDbType.Boolean) { Value = force },
                Instant("started", now - (options.Jobs.Interval - TimeSpan.FromSeconds(1))),
            ],
            cancellationToken).ConfigureAwait(false);
        return taken == 1 ? new JobClaim(database, options, clock, name, holder) : null;
    }

    /// <summary>Renews the claim; false where it is no longer this run's, and the run stops before its next item.</summary>
    internal async Task<bool> RenewAsync(CancellationToken cancellationToken)
    {
        DateTime now = clock.GetUtcNow().UtcDateTime;
        int renewed = await ExecuteAsync(
            database,
            options,
            "UPDATE approvals.jobs SET claim_expires_at = @expires, last_renewed_at = @now WHERE name = @name AND holder = @holder",
            () => [Instant("expires", now + options.Jobs.ClaimTimeout), Instant("now", now), new NpgsqlParameter("name", name), new NpgsqlParameter("holder", holder)],
            cancellationToken).ConfigureAwait(false);
        return renewed == 1;
    }

    /// <summary>Releases the claim, recording now as the job's last completion where the run <paramref name="completed"/>.</summary>
    internal Task ReleaseAsync(bool completed, CancellationToken cancellationToken)
    {
        DateTime now = clock.GetUtcNow().UtcDateTime;
        return ExecuteAsync(
            database,
            options,
            """
            UPDATE approvals.jobs
            SET holder = NULL, claim_expires_at = NULL, last_completed_at = CASE WHEN @completed THEN @now ELSE last_completed_at END
            WHERE name = @name AND holder = @holder
            """,
            () => [new NpgsqlParameter("completed", NpgsqlDbType.Boolean) { Value = completed }, Instant("now", now), new NpgsqlParameter("name", name), new NpgsqlParameter("holder", holder)],
            cancellationToken);
    }

    private static NpgsqlParameter Instant(string name, DateTime value)
    {
        return new NpgsqlParameter(name, NpgsqlDbType.TimestampTz) { Value = value };
    }

    /// <summary>Runs one statement in a transaction of the library's own, its parameters made afresh for each run of it.</summary>
    private static Task<int> ExecuteAsync(ApprovalsDatabase database, ApprovalsOptions options, string sql, Func<NpgsqlParameter[]> parameters, CancellationToken cancellationToken)
    {
        return LibraryTransactions.RunAsync(database, options, (context, cancel) => context.Database.ExecuteSqlRawAsync(sql, parameters(), cancel), cancellationToken);
    }
}