using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using Themis.Core;
using Themis.Requests;

namespace Themis.Operations;

/// <summary>
/// The health check <c>themis</c> (<c>04</c> APR-OPS-005; <c>06</c> section 7), registered
/// where the jobs run. It turns unhealthy while a job is stalled, as <c>job-stalled</c> holds,
/// whatever <c>Themis:Observer:Enabled</c> says; and, in an application whose own
/// <c>Themis:Observer:Enabled</c> is on, while the oldest event not yet taken was written longer
/// ago than <c>Themis:Observer:LagThreshold</c>, as <c>observer-failing</c> holds (T-022).
/// Before the start has opened the library's database, nothing is judged.
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

        ApprovalsOptions settings = options.Value;
        DateTimeOffset now = clock.GetUtcNow();
        ApprovalsContext approvals = database.Context();
        await using (approvals.ConfigureAwait(false))
        {
            List<JobRow> jobs = await approvals.Jobs.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
            List<string> stalled = [.. jobs.Where(job => JobStall.Stalled(job, settings, now)).Select(job => Spelling<ApprovalJob>.Write(job.Name)).Order(StringComparer.Ordinal)];
            if (stalled.Count > 0)
            {
                return HealthCheckResult.Unhealthy($"A job is stalled: {string.Join(", ", stalled)}.");
            }

            if (!settings.Observer.Enabled)
            {
                return HealthCheckResult.Healthy();
            }

            (DateTimeOffset? oldest, _) = await AlertWatch.OutboxAsync(approvals, cancellationToken).ConfigureAwait(false);
            return oldest is DateTimeOffset written && now - written > settings.Observer.LagThreshold
                ? HealthCheckResult.Unhealthy("The oldest event not yet taken is older than Themis:Observer:LagThreshold.")
                : HealthCheckResult.Healthy();
        }
    }
}