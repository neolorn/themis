using System.Text.Json.Serialization;

namespace Themis.Core;

/// <summary>How many of a step's assignments must approve (<c>02</c> APR-TYPE-004; <c>06</c> section 2); each member is written as its spelling.</summary>
public enum ApprovalMode
{
    /// <summary>One approval completes the step.</summary>
    [JsonStringEnumMemberName("anyone")]
    AnyOne,

    /// <summary>Every assignment must be approved.</summary>
    [JsonStringEnumMemberName("all")]
    All,

    /// <summary><c>Required</c> approvals, counted per distinct person, complete the step.</summary>
    [JsonStringEnumMemberName("atleast")]
    AtLeast,
}