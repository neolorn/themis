using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Themis.Core;
using Themis.Requests;

namespace Themis.Operations;

/// <summary>
/// Runs the library's jobs (<c>04</c> APR-OPS-004), on demand through
/// <see cref="IApprovalJobs"/> and on the hosted timer, each run under its job's claim. The
/// <c>deadlines</c> job runs the action of each step whose deadline has passed; the
/// <c>observer</c> job delivers the events, completing at once where
/// <c>Themis:Observer:Enabled</c> is off; the <c>watch</c> job judges the alert conditions.
/// The seams are resolved when a run needs them, so a host without one is stopped by the
/// start's seam check (<c>03</c> APR-LIB-005).
/// </summary>
internal sealed partial class JobRunner(
    ApprovalsReady ready,
    ApprovalsDatabase database,
    IOptions<ApprovalsOptions> options,
    IServiceProvider services,
    ApprovalsMetrics metrics,
    TimeProvider clock,
    ILogger<JobRunner> logger) : IApprovalJobs
{
    public ValueTask<int> RunAsync(ApprovalJob job, CancellationToken cancellationToken)
    {
        if (job is not (ApprovalJob.Deadlines or ApprovalJob.Observer or ApprovalJob.Watch))
        {
            throw new ArgumentOutOfRangeException(nameof(job), job, "The job is no member of ApprovalJob.");
        }

        ready.ThrowIfNotReady();
        if (!options.Value.Jobs.Enabled)
        {
            throw new InvalidOperationException("The jobs do not run in this application: Themis:Jobs:Enabled is off.");
        }

        return RunOnceAsync(job, force: true, cancellationToken);
    }

    /// <summary>Runs one run of <paramref name="job"/> where its claim can be taken, whatever its interval where <paramref name="force"/> is set, and answers the number of items it handled.</summary>
    internal async ValueTask<int> RunOnceAsync(ApprovalJob job, bool force, CancellationToken cancellationToken)
    {
        ApprovalsOptions settings = options.Value;
        JobClaim? claim = await JobClaim.TakeAsync(database, settings, clock, job, force, cancellationToken).ConfigureAwait(false);
        if (claim is null)
        {
            return 0;
        }

        int handled;
        bool completed = false;
        try
        {
            handled = job switch
            {
                ApprovalJob.Deadlines => await new DeadlineRun(database, settings, services.GetRequiredService<IApproverDirectory>(), metrics, clock).RunAsync(claim, cancellationToken).ConfigureAwait(false),
                ApprovalJob.Observer when settings.Observer.Enabled => await new ObserverDelivery(database, settings, services.GetRequiredService<IApprovalObserver>(), metrics, clock, logger).RunAsync(claim, cancellationToken).ConfigureAwait(false),
                ApprovalJob.Watch => await new AlertWatch(database, settings, services.GetRequiredService<IAlertSink>(), metrics, clock, logger).RunAsync(claim, cancellationToken).ConfigureAwait(false),
                _ => 0,
            };
            completed = true;
        }
        finally
        {
            await claim.ReleaseAsync(completed, CancellationToken.None).ConfigureAwait(false);
        }

        string name = Spelling<ApprovalJob>.Write(job);
        Ran(name, handled);
        return handled;
    }

    [LoggerMessage(EventId = 1, EventName = "JobRan", Level = LogLevel.Debug, Message = "Job {Job} ran, {Handled} items handled.")]
    private partial void Ran(string job, int handled);
}