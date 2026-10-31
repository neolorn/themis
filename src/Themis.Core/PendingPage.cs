using System.Collections.Generic;

namespace Themis.Core;

/// <summary>A page of the requests a person may decide now (<c>01</c> APR-REQ-013).</summary>
public sealed record PendingPage
{
    /// <summary>The page's items, oldest request first.</summary>
    public IReadOnlyList<PendingItem> Items { get; init; } = [];

    /// <summary>The next page's cursor, or none on the last page.</summary>
    public string? Next { get; init; }
}