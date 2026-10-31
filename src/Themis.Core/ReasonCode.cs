using System.Text.Json.Serialization;

namespace Themis.Core;

/// <summary>Why the library itself closed a request (<c>06</c> section 2); each member is written as its spelling.</summary>
public enum ReasonCode
{
    /// <summary>No one could be assigned (<c>01</c> APR-REQ-007).</summary>
    [JsonStringEnumMemberName("noapprover")]
    NoApprover,

    /// <summary>A deadline's action (<c>01</c> APR-REQ-009).</summary>
    [JsonStringEnumMemberName("deadline")]
    Deadline,

    /// <summary>A protected field changed (<c>01</c> APR-REQ-011).</summary>
    [JsonStringEnumMemberName("fieldchanged")]
    FieldChanged,
}