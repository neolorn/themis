using System;
using System.Globalization;
using System.Text;

namespace Themis.Core;

/// <summary>A request a person may decide now (<c>01</c> APR-REQ-013). Formatting it writes neither its target nor the person acted for.</summary>
public sealed record PendingItem
{
    /// <summary>The request.</summary>
    public required RequestId Request { get; init; }

    /// <summary>The type's key.</summary>
    public required string Type { get; init; }

    /// <summary>What it is about.</summary>
    public required Target Target { get; init; }

    /// <summary>The number of its current step.</summary>
    public required int Step { get; init; }

    /// <summary>The name of its current step.</summary>
    public required string StepName { get; init; }

    /// <summary>When the request was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>The person whose assignment the person decides through a delegation, or none.</summary>
    public string? ActingFor { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Request = {Request}, Type = {Type}, Step = {Step}, StepName = {StepName}, CreatedAt = {CreatedAt:O}");
        return true;
    }
}