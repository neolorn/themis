using System;
using System.ComponentModel.DataAnnotations;

namespace Themis.Operations;

/// <summary>The keys <c>Themis:Retention:...</c> (<c>06</c> section 4; T-025).</summary>
internal sealed class RetentionOptions
{
    /// <summary>How long a closed request is kept after it closed, in whole days; unset keeps every request.</summary>
    [Range(typeof(TimeSpan), "30.00:00:00", "10950.00:00:00")]
    public TimeSpan? ClosedRequests { get; set; }
}