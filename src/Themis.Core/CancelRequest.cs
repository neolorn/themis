using System.Globalization;
using System.Text;

namespace Themis.Core;

/// <summary>
/// A cancellation of an open request (<c>01</c> APR-REQ-010). Formatting it writes neither its
/// persons nor its reason.
/// </summary>
public sealed record CancelRequest
{
    /// <summary>The request.</summary>
    public required RequestId Request { get; init; }

    /// <summary>The person the host says cancelled it.</summary>
    public required string Actor { get; init; }

    /// <summary>The person the actor acted for, or none.</summary>
    public string? OnBehalfOf { get; init; }

    /// <summary>The reason, 0 to 2,000 characters, or none.</summary>
    public string? Reason { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Request = {Request}");
        return true;
    }
}