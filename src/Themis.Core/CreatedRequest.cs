namespace Themis.Core;

/// <summary>The request a create recorded (<c>01</c> APR-REQ-001).</summary>
public sealed record CreatedRequest
{
    /// <summary>The request's identifier.</summary>
    public required RequestId Request { get; init; }

    /// <summary>Its status: pending, or approved where the type approved it at once.</summary>
    public required RequestStatus Status { get; init; }
}