using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Themis.Core;

namespace Themis.Types;

/// <summary>
/// A definition's canonical form, one exact sequence of bytes for each definition, whose
/// SHA-256 is its version's hash (<c>05</c> APR-ENG-006; <c>02</c> APR-TYPE-001): UTF-8 JSON
/// written by <see cref="CanonicalFormContext"/> with the serializer's default encoder and no
/// whitespace, each record's members in the order of <c>03</c> APR-LIB-002 under their names in
/// camel case, a member that is none, an empty list or <c>false</c> left out, and a case written
/// as an object whose first member, <c>case</c>, is the case's name in camel case.
/// </summary>
internal static class CanonicalForm
{
    private static readonly JsonSerializerOptions Options = MakeOptions();

    /// <summary>Writes the canonical form of <paramref name="definition"/>.</summary>
    internal static byte[] Write(ApprovalTypeDefinition definition)
    {
        return JsonSerializer.SerializeToUtf8Bytes(definition, Definition);
    }

    /// <summary>Reads the definition whose canonical form <paramref name="form"/> is.</summary>
    internal static ApprovalTypeDefinition Read(ReadOnlySpan<byte> form)
    {
        ApprovalTypeDefinition read = JsonSerializer.Deserialize(form, Definition) ?? throw new JsonException("A canonical form holds no definition.");

        // An empty list is left out of the form, and the serializer leaves an init-only list it
        // does not read null; only these two lists of a definition within its rule may be empty.
        return read with { ProtectedFields = read.ProtectedFields ?? [], Watchers = read.Watchers ?? [] };
    }

    /// <summary>The SHA-256 of <paramref name="form"/>, the version's hash.</summary>
    internal static byte[] Hash(ReadOnlySpan<byte> form)
    {
        return SHA256.HashData(form);
    }

    private static JsonTypeInfo<ApprovalTypeDefinition> Definition => (JsonTypeInfo<ApprovalTypeDefinition>)Options.GetTypeInfo(typeof(ApprovalTypeDefinition));

    private static JsonSerializerOptions MakeOptions()
    {
        JsonSerializerOptions options = new(CanonicalFormContext.Default.Options)
        {
            TypeInfoResolver = CanonicalFormContext.Default.WithAddedModifier(Shape),
        };
        options.MakeReadOnly();
        return options;
    }

    private static void Shape(JsonTypeInfo info)
    {
        if (info.Type == typeof(Condition))
        {
            info.PolymorphismOptions = Cases(
                (typeof(Condition.AllOf), nameof(Condition.AllOf)),
                (typeof(Condition.AnyOf), nameof(Condition.AnyOf)),
                (typeof(Condition.Negate), nameof(Condition.Negate)),
                (typeof(Condition.Compare), nameof(Condition.Compare)),
                (typeof(Condition.OneOf), nameof(Condition.OneOf)),
                (typeof(Condition.RequesterHas), nameof(Condition.RequesterHas)),
                (typeof(Condition.SubjectHas), nameof(Condition.SubjectHas)));
        }
        else if (info.Type == typeof(ApproverReference))
        {
            info.PolymorphismOptions = Cases(
                (typeof(ApproverReference.Permission), nameof(ApproverReference.Permission)),
                (typeof(ApproverReference.Person), nameof(ApproverReference.Person)));
        }
        else if (info.Type == typeof(FieldValue))
        {
            info.PolymorphismOptions = Cases(
                (typeof(FieldValue.PlainText), nameof(FieldValue.PlainText)),
                (typeof(FieldValue.WholeNumber), nameof(FieldValue.WholeNumber)),
                (typeof(FieldValue.DecimalNumber), nameof(FieldValue.DecimalNumber)),
                (typeof(FieldValue.Flag), nameof(FieldValue.Flag)));
        }

        if (info.Kind == JsonTypeInfoKind.Object)
        {
            foreach (JsonPropertyInfo property in info.Properties)
            {
                property.ShouldSerialize = static (_, value) => value is not (null or false or IReadOnlyCollection<object> { Count: 0 });
            }
        }
    }

    /// <summary>The cases of a type with cases, each written with its name in camel case as its member <c>case</c>.</summary>
    private static JsonPolymorphismOptions Cases(params (Type Type, string Name)[] cases)
    {
        JsonPolymorphismOptions options = new() { TypeDiscriminatorPropertyName = "case" };
        foreach ((Type type, string name) in cases)
        {
            options.DerivedTypes.Add(new JsonDerivedType(type, JsonNamingPolicy.CamelCase.ConvertName(name)));
        }

        return options;
    }
}