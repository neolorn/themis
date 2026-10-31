using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Themis.Core;

/// <summary>
/// What happened to a request or a delegation, told to the observer in order for each request
/// (<c>01</c> APR-REQ-014). It prints neither its target nor any person.
/// </summary>
public sealed record ApprovalEvent
{
    /// <summary>The event's identifier.</summary>
    public required ApprovalEventId Event { get; init; }

    /// <summary>What the event tells.</summary>
    public required EventKind Kind { get; init; }

    /// <summary>When the event was written.</summary>
    public required DateTimeOffset At { get; init; }

    /// <summary>The event's number, from 1 within its request; 1 for a delegation's.</summary>
    public required long Sequence { get; init; }

    /// <summary>The request, or none for a delegation's event.</summary>
    public RequestId? Request { get; init; }

    /// <summary>The request's approval type, or none.</summary>
    public string? Type { get; init; }

    /// <summary>The approval type's version the request runs under, or none.</summary>
    public int? Version { get; init; }

    /// <summary>The request's target, or none.</summary>
    public Target? Target { get; init; }

    /// <summary>The step's number, or none.</summary>
    public int? Step { get; init; }

    /// <summary>The person who acted, or none.</summary>
    public string? Actor { get; init; }

    /// <summary>The person the actor acted for, or none.</summary>
    public string? ActingFor { get; init; }

    /// <summary>Why the library itself closed the request, or none.</summary>
    public ReasonCode? ReasonCode { get; init; }

    /// <summary>The delegation, or none for a request's event.</summary>
    public DelegationId? Delegation { get; init; }

    /// <summary>The persons the event names to be told.</summary>
    public IReadOnlyList<string> Notify { get; init; } = [];

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Event = {Event}, Kind = {Kind}, At = {At:O}, Sequence = {Sequence}, Request = {Request}, Type = {Type}, Version = {Version}, Step = {Step}, ReasonCode = {ReasonCode}, Delegation = {Delegation}");
        return true;
    }
}