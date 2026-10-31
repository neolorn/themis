using System;
using System.Threading;
using System.Threading.Tasks;

using Themis.Core;

namespace Themis.Testing;

/// <summary>Runs the library's jobs on demand in a test (<c>03</c> APR-LIB-007).</summary>
public sealed class TestJobs
{
    private readonly IApprovalJobs jobs;

    /// <summary>Runs the jobs through <paramref name="jobs"/>.</summary>
    /// <param name="jobs">The library's jobs, as the test's host resolves them.</param>
    /// <exception cref="ArgumentNullException"><paramref name="jobs"/> is null.</exception>
    public TestJobs(IApprovalJobs jobs)
    {
        ArgumentNullException.ThrowIfNull(jobs);
        this.jobs = jobs;
    }

    /// <summary>
    /// Runs the <c>deadlines</c> job, then the <c>observer</c> job, until a round of both handles
    /// nothing. A test moves the clock, the <see cref="TimeProvider"/> the library is registered
    /// with, to pass a deadline.
    /// </summary>
    /// <param name="cancellationToken">Cancels the runs.</param>
    /// <returns>The number of items handled.</returns>
    /// <exception cref="InvalidOperationException">The host has not started, or <c>Themis:Jobs:Enabled</c> is off.</exception>
    public async ValueTask<int> RunUntilIdleAsync(CancellationToken cancellationToken)
    {
        int handled = 0;
        while (true)
        {
            int round = await jobs.RunAsync(ApprovalJob.Deadlines, cancellationToken).ConfigureAwait(false);
            round += await jobs.RunAsync(ApprovalJob.Observer, cancellationToken).ConfigureAwait(false);
            if (round == 0)
            {
                return handled;
            }

            handled += round;
        }
    }
}