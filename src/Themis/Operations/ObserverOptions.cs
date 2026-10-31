using System;
using System.ComponentModel.DataAnnotations;

namespace Themis.Operations;

/// <summary>The keys <c>Themis:Observer:...</c> (<c>06</c> section 4).</summary>
internal sealed class ObserverOptions
{
    public bool Enabled { get; set; } = true;

    [Range(typeof(TimeSpan), "00:01:00", "1.00:00:00")]
    public TimeSpan LagThreshold { get; set; } = TimeSpan.FromMinutes(5);
}