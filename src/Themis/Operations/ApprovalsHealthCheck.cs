using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Themis.Operations;

/// <summary>
/// The health check <c>themis</c> (<c>04</c> APR-OPS-005; <c>06</c> section 7), registered
/// where the jobs run. It turns unhealthy while the oldest event not yet taken was written
/// longer ago than <c>Themis:Observer:LagThreshold</c>; a job judged stalled comes with the
/// condition <c>job-stalled</c>, and until then none is. Before the start has opened the
/// library's database, nothing is judged.
/// </summary>
internal sealed class ApprovalsHealthCheck(ApprovalsReady ready, ApprovalsDatabase database, IOptions<ApprovalsOptions> options, TimeProvider clock) : IHealthCheck
{
    internal const string Name = "themis";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!ready.IsReady)
        {
            return HealthCheckResult.Healthy();
        }

        ApprovalsContext approvals = database.Context();
        await using (approvals.ConfigureAwait(false))
        {
            (DateTimeOffset? oldest, _) = await AlertWatch.OutboxAsync(approvals, cancellationToken).ConfigureAwait(false);
            return oldest is DateTimeOffset written && clock.GetUtcNow() - written > options.Value.Observer.LagThreshold
                ? HealthCheckResult.Unhealthy("The oldest event not yet taken is older than Themis:Observer:LagThreshold.")
                : HealthCheckResult.Healthy();
        }
    }
}