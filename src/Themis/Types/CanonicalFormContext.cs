using System.Collections.Generic;
using System.Text.Json.Serialization;

using Themis.Core;

namespace Themis.Types;

/// <summary>
/// The source-generated context that writes and reads a definition's canonical form
/// (<c>05</c> APR-ENG-006): members in camel case, an enumeration member by its spelling, a
/// decimal without trailing zeros and a duration as its whole number of seconds. Each case is
/// listed, so <see cref="CanonicalForm"/> can write it as a case.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    Converters = [typeof(DecimalWithoutTrailingZerosConverter), typeof(WholeSecondsConverter)])]
[JsonSerializable(typeof(ApprovalTypeDefinition))]
[JsonSerializable(typeof(Dictionary<string, FieldValue>))]
[JsonSerializable(typeof(ApproverReference))]
[JsonSerializable(typeof(Condition.AllOf))]
[JsonSerializable(typeof(Condition.AnyOf))]
[JsonSerializable(typeof(Condition.Negate))]
[JsonSerializable(typeof(Condition.Compare))]
[JsonSerializable(typeof(Condition.OneOf))]
[JsonSerializable(typeof(Condition.RequesterHas))]
[JsonSerializable(typeof(Condition.SubjectHas))]
[JsonSerializable(typeof(ApproverReference.Permission))]
[JsonSerializable(typeof(ApproverReference.Person))]
[JsonSerializable(typeof(FieldValue.PlainText))]
[JsonSerializable(typeof(FieldValue.WholeNumber))]
[JsonSerializable(typeof(FieldValue.DecimalNumber))]
[JsonSerializable(typeof(FieldValue.Flag))]
internal sealed partial class CanonicalFormContext : JsonSerializerContext;