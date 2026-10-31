using System.Text.Json.Serialization;

namespace Themis.Core;

/// <summary>Whether a person who approved one step stands for a later step they are assigned to (<c>01</c> APR-REQ-006; <c>06</c> section 2); each member is written as its spelling.</summary>
public enum DuplicateRule
{
    /// <summary>The earlier approval satisfies the later assignment.</summary>
    [JsonStringEnumMemberName("skip")]
    Skip,

    /// <summary>The person is not assigned to the later step, and someone else decides it.</summary>
    [JsonStringEnumMemberName("requireother")]
    RequireOther,
}