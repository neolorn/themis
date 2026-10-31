using System.Text.Json.Serialization;

namespace Themis.Core;

/// <summary>What a host may do with a protected field of a target (<c>01</c> APR-REQ-011; <c>06</c> section 2); each member is written as its spelling.</summary>
public enum FieldProtection
{
    /// <summary>No open request covers the field.</summary>
    [JsonStringEnumMemberName("free")]
    Free,

    /// <summary>A pending request covers the field; the host refuses a change.</summary>
    [JsonStringEnumMemberName("locked")]
    Locked,

    /// <summary>An approved request covers the field; a change voids it.</summary>
    [JsonStringEnumMemberName("approved")]
    Approved,
}