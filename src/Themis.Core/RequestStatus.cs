using System.Text.Json.Serialization;

namespace Themis.Core;

/// <summary>A request's status (<c>06</c> section 3): open while <see cref="Pending"/> or <see cref="Approved"/>, closed otherwise; each member is written as its spelling.</summary>
public enum RequestStatus
{
    /// <summary>A step is current; decisions are taken.</summary>
    [JsonStringEnumMemberName("pending")]
    Pending,

    /// <summary>Every step that held is complete, or the type approved it at once; the host has not yet acted.</summary>
    [JsonStringEnumMemberName("approved")]
    Approved,

    /// <summary>A person rejected it, or a deadline did.</summary>
    [JsonStringEnumMemberName("rejected")]
    Rejected,

    /// <summary>Someone cancelled it, a deadline did, or no one could be assigned.</summary>
    [JsonStringEnumMemberName("cancelled")]
    Cancelled,

    /// <summary>A protected field changed after it was approved and before it was completed.</summary>
    [JsonStringEnumMemberName("void")]
    Void,

    /// <summary>The host acted on the approval.</summary>
    [JsonStringEnumMemberName("completed")]
    Completed,
}