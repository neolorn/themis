using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace Themis.Core;

/// <summary>
/// A request's whole record (<c>01</c> APR-REQ-013). Formatting it writes neither its target,
/// its snapshot, its persons, their roles nor its reason.
/// </summary>
public sealed record RequestRecord
{
    /// <summary>The request's identifier.</summary>
    public required RequestId Request { get; init; }

    /// <summary>The type's key.</summary>
    public required string Type { get; init; }

    /// <summary>The version it was created under.</summary>
    public required int Version { get; init; }

    /// <summary>The policy it took, or none for the type's fallback step or an approval at once.</summary>
    public string? Policy { get; init; }

    /// <summary>What it is about.</summary>
    public required Target Target { get; init; }

    /// <summary>The values of the version's protected fields, by field name.</summary>
    public IReadOnlyDictionary<string, FieldValue> Snapshot { get; init; } = ReadOnlyDictionary<string, FieldValue>.Empty;

    /// <summary>The requester.</summary>
    public required string Requester { get; init; }

    /// <summary>The subject, or none.</summary>
    public string? Subject { get; init; }

    /// <summary>The requester's roles as the host gave them.</summary>
    public IReadOnlyList<string> RequesterRoles { get; init; } = [];

    /// <summary>The subject's roles as the host gave them.</summary>
    public IReadOnlyList<string> SubjectRoles { get; init; } = [];

    /// <summary>The request it resubmits, or none.</summary>
    public RequestId? Previous { get; init; }

    /// <summary>Its status.</summary>
    public required RequestStatus Status { get; init; }

    /// <summary>When it was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>When it was approved, or none.</summary>
    public DateTimeOffset? ApprovedAt { get; init; }

    /// <summary>When it closed, or none while it is open.</summary>
    public DateTimeOffset? ClosedAt { get; init; }

    /// <summary>The person who closed it, or none where no person did.</summary>
    public string? ClosedBy { get; init; }

    /// <summary>The person the closer acted for, or none.</summary>
    public string? ClosedFor { get; init; }

    /// <summary>The reason a person gave for closing it, or none.</summary>
    public string? Reason { get; init; }

    /// <summary>The reason code where the library itself closed it, or none.</summary>
    public ReasonCode? ReasonCode { get; init; }

    /// <summary>The field whose change voided it, or none.</summary>
    public string? ChangedField { get; init; }

    /// <summary>Its steps, in the policy's order.</summary>
    public IReadOnlyList<StepRecord> Steps { get; init; } = [];

    /// <summary>Every decision on it, in the order made.</summary>
    public IReadOnlyList<DecisionRecord> Decisions { get; init; } = [];

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Request = {Request}, Type = {Type}, Version = {Version}, Policy = {Policy}, Previous = {Previous}, Status = {Status}, CreatedAt = {CreatedAt:O}, ApprovedAt = {ApprovedAt:O}, ClosedAt = {ClosedAt:O}, ReasonCode = {ReasonCode}, ChangedField = {ChangedField}");
        return true;
    }
}