using System.Text.Json.Serialization;

namespace Themis.Core;

/// <summary>Why a person is assigned to a step (<c>01</c> APR-REQ-003, APR-REQ-007, APR-REQ-009; <c>06</c> section 2); each member is written as its spelling.</summary>
public enum AssignmentOrigin
{
    /// <summary>One of the step's approvers named them.</summary>
    [JsonStringEnumMemberName("approver")]
    Approver,

    /// <summary>The step's or the type's fallback, where the rules left no one.</summary>
    [JsonStringEnumMemberName("fallback")]
    Fallback,

    /// <summary>A deadline's escalation added them.</summary>
    [JsonStringEnumMemberName("escalation")]
    Escalation,
}