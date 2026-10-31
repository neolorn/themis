using System;

using Themis.Requests;

namespace Themis.Operations;

/// <summary>
/// The judgement of <c>job-stalled</c> (<c>04</c> APR-OPS-004, APR-OPS-006; <c>06</c> section 5):
/// a job some process has started whose last completion and last renewal are both older than
/// <c>Themis:Jobs:StallFactor</c> of <c>Themis:Jobs:Interval</c> plus <c>Themis:Timeout</c>, a
/// completion or a renewal that never happened counting as older. A job no process has started
/// is never stalled.
/// </summary>
internal static class JobStall
{
    internal const string Condition = "job-stalled";

    /// <summary>Whether <paramref name="job"/> is stalled at <paramref name="now"/>.</summary>
    internal static bool Stalled(JobRow job, ApprovalsOptions options, DateTimeOffset now)
    {
        DateTimeOffset before = now - ((options.Jobs.Interval * options.Jobs.StallFactor) + options.Timeout);
        return job.LastStartedAt is not null
            && (job.LastCompletedAt is not DateTimeOffset completed || completed < before)
            && (job.LastRenewedAt is not DateTimeOffset renewed || renewed < before);
    }
}