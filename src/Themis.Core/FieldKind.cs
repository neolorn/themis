using System.Text.Json.Serialization;

namespace Themis.Core;

/// <summary>A protected field's kind (<c>06</c> section 2); each member is written as its spelling.</summary>
public enum FieldKind
{
    /// <summary>Text, at most 1,024 characters in a snapshot.</summary>
    [JsonStringEnumMemberName("text")]
    Text,

    /// <summary>A whole number.</summary>
    [JsonStringEnumMemberName("wholenumber")]
    WholeNumber,

    /// <summary>A decimal number.</summary>
    [JsonStringEnumMemberName("decimalnumber")]
    DecimalNumber,

    /// <summary>A flag, true or false.</summary>
    [JsonStringEnumMemberName("flag")]
    Flag,
}