using System.Text.Json.Serialization;

namespace Themis.Core;

/// <summary>How a condition compares a field with a value (<c>02</c> APR-TYPE-005; <c>06</c> section 2); each member is written as its spelling.</summary>
public enum CompareOperator
{
    /// <summary>The field equals the value.</summary>
    [JsonStringEnumMemberName("equal")]
    Equal,

    /// <summary>The field does not equal the value.</summary>
    [JsonStringEnumMemberName("notequal")]
    NotEqual,

    /// <summary>The field is greater than the value; a number field alone.</summary>
    [JsonStringEnumMemberName("greater")]
    Greater,

    /// <summary>The field is greater than or equal to the value; a number field alone.</summary>
    [JsonStringEnumMemberName("greaterorequal")]
    GreaterOrEqual,

    /// <summary>The field is less than the value; a number field alone.</summary>
    [JsonStringEnumMemberName("less")]
    Less,

    /// <summary>The field is less than or equal to the value; a number field alone.</summary>
    [JsonStringEnumMemberName("lessorequal")]
    LessOrEqual,
}