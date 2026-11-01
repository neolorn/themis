using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Themis.Core;

namespace Themis.Operations;

/// <summary>
/// The hosted service that runs the jobs on their timer (<c>04</c> APR-OPS-004), every
/// <c>Themis:Jobs:Interval</c> from the start, where <c>Themis:Jobs:Enabled</c> and
/// <c>Themis:Jobs:Hosted</c> are on and no <see cref="HostedJobsOff"/> is registered. Each run
/// takes its job's claim as the interval allows, and a run that fails is logged and runs again
/// at its next time.
/// </summary>
internal sealed partial class HostedJob(JobRunner runner, ApprovalsReady ready, IOptions<ApprovalsOptions> options, IServiceProvider services, TimeProvider clock, ILogger<HostedJob> logger) : BackgroundService
{
    private static readonly ApprovalJob[] Jobs = [ApprovalJob.Deadlines, ApprovalJob.Observer, ApprovalJob.Watch, ApprovalJob.Retention];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!ready.IsReady || services.GetService<HostedJobsOff>() is not null)
        {
            return;
        }

        JobsOptions jobs = options.Value.Jobs;
        if (!jobs.Enabled || !jobs.Hosted)
        {
            return;
        }

        using PeriodicTimer timer = new(jobs.Interval, clock);
        do
        {
            foreach (ApprovalJob job in Jobs)
            {
                await RunAsync(job, stoppingToken).ConfigureAwait(false);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A job that fails is logged and runs again at its next time (04 APR-OPS-004).")]
    private async Task RunAsync(ApprovalJob job, CancellationToken stoppingToken)
    {
        try
        {
            await runner.RunOnceAsync(job, force: false, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception failure)
        {
            string name = Spelling<ApprovalJob>.Write(job);
            string thrown = failure.GetType().Name;
            Failed(name, thrown);
        }
    }

    [LoggerMessage(EventId = 1, EventName = "JobFailed", Level = LogLevel.Error, Message = "Job {Job} failed ({Thrown}); it runs again at its next time.")]
    private partial void Failed(string job, string thrown);
}