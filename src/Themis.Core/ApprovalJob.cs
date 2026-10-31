using System.Text.Json.Serialization;

namespace Themis.Core;

/// <summary>The library's background jobs (<c>04</c> APR-OPS-004; <c>06</c> section 2); each member is written as its spelling.</summary>
public enum ApprovalJob
{
    /// <summary>Runs the action of each step whose deadline has passed.</summary>
    [JsonStringEnumMemberName("deadlines")]
    Deadlines,

    /// <summary>Delivers the events not yet taken, in order for each request.</summary>
    [JsonStringEnumMemberName("observer")]
    Observer,

    /// <summary>Judges the alert conditions.</summary>
    [JsonStringEnumMemberName("watch")]
    Watch,
}