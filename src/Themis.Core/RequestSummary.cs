using System;

namespace Themis.Core;

/// <summary>A request on a target, as <see cref="IApprovals.ByTargetAsync"/> answers it (<c>01</c> APR-REQ-013). It names no person.</summary>
public sealed record RequestSummary
{
    /// <summary>The request's identifier.</summary>
    public required RequestId Request { get; init; }

    /// <summary>The type's key.</summary>
    public required string Type { get; init; }

    /// <summary>The version it was created under.</summary>
    public required int Version { get; init; }

    /// <summary>Its status.</summary>
    public required RequestStatus Status { get; init; }

    /// <summary>When it was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>When it closed, or none while open.</summary>
    public DateTimeOffset? ClosedAt { get; init; }
}