using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Themis.Operations;

/// <summary>
/// The health check <c>themis</c> (<c>04</c> APR-OPS-005; <c>06</c> section 7), registered
/// where the jobs run. It turns unhealthy while the oldest undelivered event is older than
/// <c>Themis:Observer:LagThreshold</c> or a job is stalled; the outbox and the jobs it judges
/// come with plan phase 3, and until then neither can hold.
/// </summary>
internal sealed class ApprovalsHealthCheck : IHealthCheck
{
    internal const string Name = "themis";

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(HealthCheckResult.Healthy());
    }
}