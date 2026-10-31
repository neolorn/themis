using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Themis.Core;

/// <summary>Is told when an alert condition of <c>06</c> section 5 is raised and when it clears (<c>04</c> APR-OPS-006).</summary>
public interface IAlertSink
{
    /// <summary>Raises a condition.</summary>
    /// <param name="condition">The condition's name.</param>
    /// <param name="scope">The condition's scope, or none.</param>
    /// <param name="details">The condition's details.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the sink has taken the call.</returns>
    ValueTask RaiseAsync(string condition, string? scope, IReadOnlyDictionary<string, string> details, CancellationToken cancellationToken);

    /// <summary>Clears a condition.</summary>
    /// <param name="condition">The condition's name.</param>
    /// <param name="scope">The condition's scope, or none.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the sink has taken the call.</returns>
    ValueTask ClearAsync(string condition, string? scope, CancellationToken cancellationToken);
}