using System;
using System.ComponentModel.DataAnnotations;

namespace Themis.Operations;

/// <summary>The keys <c>Themis:Jobs:...</c> (<c>06</c> section 4).</summary>
internal sealed class JobsOptions
{
    public bool Enabled { get; set; } = true;

    public bool Hosted { get; set; } = true;

    [Range(typeof(TimeSpan), "00:00:05", "01:00:00")]
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(30);

    [Range(typeof(TimeSpan), "00:01:00", "01:00:00")]
    public TimeSpan ClaimTimeout { get; set; } = TimeSpan.FromMinutes(5);

    [Range(1, 1_000)]
    public int BatchSize { get; set; } = 100;

    [Range(2, 100)]
    public int StallFactor { get; set; } = 3;
}