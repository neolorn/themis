using System.Text.Json.Serialization;

namespace Themis.Core;

/// <summary>A step's status (<c>06</c> section 3); each member is written as its spelling.</summary>
public enum StepStatus
{
    /// <summary>Not yet reached, or never reached because the request closed or approved before it.</summary>
    [JsonStringEnumMemberName("waiting")]
    Waiting,

    /// <summary>Started; its assignments are being decided.</summary>
    [JsonStringEnumMemberName("current")]
    Current,

    /// <summary>Completed by its approvals.</summary>
    [JsonStringEnumMemberName("done")]
    Done,

    /// <summary>Its condition was false when the request reached it.</summary>
    [JsonStringEnumMemberName("skipped")]
    Skipped,

    /// <summary>It was current when the request was rejected or cancelled.</summary>
    [JsonStringEnumMemberName("closed")]
    Closed,
}