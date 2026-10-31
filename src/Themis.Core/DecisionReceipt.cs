namespace Themis.Core;

/// <summary>The decision a decide recorded (<c>01</c> APR-REQ-004).</summary>
public sealed record DecisionReceipt
{
    /// <summary>The decision's identifier.</summary>
    public required DecisionId Decision { get; init; }

    /// <summary>The request's status once the decision is recorded.</summary>
    public required RequestStatus Status { get; init; }
}