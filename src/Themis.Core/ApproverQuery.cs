using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace Themis.Core;

/// <summary>
/// The request a directory resolves approvers for (<c>01</c> APR-REQ-003). It prints neither
/// its target, nor any person, nor the snapshot's values.
/// </summary>
public sealed record ApproverQuery
{
    /// <summary>The request.</summary>
    public required RequestId Request { get; init; }

    /// <summary>The request's approval type.</summary>
    public required string Type { get; init; }

    /// <summary>The approval type's version the request runs under.</summary>
    public required int Version { get; init; }

    /// <summary>The request's target.</summary>
    public required Target Target { get; init; }

    /// <summary>The person who made the request.</summary>
    public required string Requester { get; init; }

    /// <summary>The person the request is about, or none.</summary>
    public string? Subject { get; init; }

    /// <summary>The protected fields' values the request holds, by field.</summary>
    public IReadOnlyDictionary<string, FieldValue> Snapshot { get; init; } = ReadOnlyDictionary<string, FieldValue>.Empty;

    /// <summary>The step's number, or none when the type's watchers are resolved.</summary>
    public int? Step { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Request = {Request}, Type = {Type}, Version = {Version}, Step = {Step}");
        return true;
    }
}