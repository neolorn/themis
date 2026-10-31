using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Themis.Core;

namespace Themis;

/// <summary>
/// The spelling of an enumeration's member, as its <see cref="JsonStringEnumMemberNameAttribute"/>
/// gives it (<c>03</c> APR-LIB-002), which the library writes wherever it writes the member as
/// text: a column, a log entry, a metric's tag. It is read through a source-generated context.
/// </summary>
internal static class Spelling<TEnum>
    where TEnum : struct, Enum
{
    private static readonly JsonTypeInfo<TEnum> Info = (JsonTypeInfo<TEnum>)(SpellingContext.Default.GetTypeInfo(typeof(TEnum)) ?? throw new InvalidOperationException($"{typeof(TEnum).Name} has no spelling."));

    private static readonly FrozenDictionary<TEnum, string> Spellings = Enum.GetValues<TEnum>().ToFrozenDictionary(member => member, Of);

    private static readonly FrozenDictionary<string, TEnum> Members = Spellings.ToFrozenDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);

    /// <summary>Every member's spelling, in the members' order.</summary>
    internal static IReadOnlyList<string> All { get; } = [.. Enum.GetValues<TEnum>().Select(member => Spellings[member])];

    /// <summary>The spelling of <paramref name="member"/>.</summary>
    internal static string Write(TEnum member)
    {
        return Spellings.TryGetValue(member, out string? spelling) ? spelling : throw new ArgumentOutOfRangeException(nameof(member), member, "The value is no member of its enumeration.");
    }

    /// <summary>The member spelt <paramref name="spelling"/>.</summary>
    internal static TEnum Read(string spelling)
    {
        return Members.TryGetValue(spelling, out TEnum member) ? member : throw new FormatException($"'{spelling}' spells no member of {typeof(TEnum).Name}.");
    }

    private static string Of(TEnum member)
    {
        return JsonSerializer.Deserialize(JsonSerializer.SerializeToUtf8Bytes(member, Info), SpellingContext.Default.String) ?? throw new InvalidOperationException($"{typeof(TEnum).Name}.{member} carries no spelling.");
    }
}

/// <summary>The enumerations the library writes as text, each member as its spelling.</summary>
[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(ApprovalMode))]
[JsonSerializable(typeof(AssignmentOrigin))]
[JsonSerializable(typeof(ApprovalJob))]
[JsonSerializable(typeof(DecisionOutcome))]
[JsonSerializable(typeof(EventKind))]
[JsonSerializable(typeof(ReasonCode))]
[JsonSerializable(typeof(RequestStatus))]
[JsonSerializable(typeof(StepStatus))]
internal sealed partial class SpellingContext : JsonSerializerContext;