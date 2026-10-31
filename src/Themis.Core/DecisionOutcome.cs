using System.Text.Json.Serialization;

namespace Themis.Core;

/// <summary>A decision's outcome (<c>01</c> APR-REQ-004; <c>06</c> section 2); each member is written as its spelling.</summary>
public enum DecisionOutcome
{
    /// <summary>The person approves.</summary>
    [JsonStringEnumMemberName("approve")]
    Approve,

    /// <summary>The person rejects, which rejects the request (<c>01</c> APR-REQ-005).</summary>
    [JsonStringEnumMemberName("reject")]
    Reject,
}