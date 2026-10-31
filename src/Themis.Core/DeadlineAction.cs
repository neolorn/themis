using System.Text.Json.Serialization;

namespace Themis.Core;

/// <summary>What a step's deadline does once it passes undecided; none approves (<c>02</c> APR-TYPE-004; <c>06</c> section 2). Each member is written as its spelling.</summary>
public enum DeadlineAction
{
    /// <summary>Adds the escalation target's persons to the step.</summary>
    [JsonStringEnumMemberName("escalate")]
    Escalate,

    /// <summary>Rejects the request.</summary>
    [JsonStringEnumMemberName("reject")]
    Reject,

    /// <summary>Cancels the request.</summary>
    [JsonStringEnumMemberName("cancel")]
    Cancel,
}