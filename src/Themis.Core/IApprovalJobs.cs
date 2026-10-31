using System;
using System.Threading;
using System.Threading.Tasks;

namespace Themis.Core;

/// <summary>Runs one run of a background job on demand (<c>04</c> APR-OPS-004).</summary>
public interface IApprovalJobs
{
    /// <summary>Runs one run of <paramref name="job"/>, taking its claim whatever its interval.</summary>
    /// <param name="job">The job.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The number of items the run handled; 0 where another process holds the job's claim.</returns>
    /// <exception cref="InvalidOperationException">The startup checks have not run, or <c>Themis:Jobs:Enabled</c> is off.</exception>
    ValueTask<int> RunAsync(ApprovalJob job, CancellationToken cancellationToken);
}