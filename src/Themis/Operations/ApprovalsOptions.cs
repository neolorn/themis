using System;
using System.ComponentModel.DataAnnotations;

using Microsoft.Extensions.Options;

namespace Themis.Operations;

/// <summary>
/// The configuration section <c>Themis</c> (<c>06</c> section 4), bound by the configuration
/// binder's source generator once the raw values have passed <see cref="ConfigurationRules"/>,
/// and validated by <see cref="ApprovalsOptionsValidator"/> as a second line (<c>04</c>
/// APR-OPS-001). It is read once, at the start, so a value changed while the application runs
/// takes effect at its next start.
/// </summary>
internal sealed class ApprovalsOptions
{
    [Range(typeof(TimeSpan), "00:00:01", "00:01:00")]
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    [ValidateObjectMembers]
    public DatabaseOptions Database { get; set; } = new();

    [ValidateObjectMembers]
    public JobsOptions Jobs { get; set; } = new();

    [ValidateObjectMembers]
    public ObserverOptions Observer { get; set; } = new();

    [ValidateObjectMembers]
    public AlertsOptions Alerts { get; set; } = new();
}