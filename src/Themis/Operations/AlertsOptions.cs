using System;
using System.ComponentModel.DataAnnotations;

namespace Themis.Operations;

/// <summary>The keys <c>Themis:Alerts:...</c> (<c>06</c> section 4).</summary>
internal sealed class AlertsOptions
{
    [Range(typeof(TimeSpan), "00:01:00", "1.00:00:00")]
    public TimeSpan Repeat { get; set; } = TimeSpan.FromHours(1);
}