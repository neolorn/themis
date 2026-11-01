using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Themis.Core;

namespace Themis.Endpoints;

/// <summary>
/// The JSON of the endpoints (<c>03</c> APR-LIB-004): the library's types as they are, in camel
/// case, identifiers as strings, enumerations by their spellings of <c>06</c> section 2, and a
/// case written as an object whose member <c>case</c> is the case's name in camel case. It is
/// the endpoints' own, so the host's JSON settings neither change it nor are changed by it.
/// </summary>
internal static class ApprovalsJson
{
    internal static JsonSerializerOptions Options { get; } = MakeOptions();

    private static JsonSerializerOptions MakeOptions()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver().WithAddedModifier(Cases),
        };
        options.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
        options.Converters.Add(new IdConverter<RequestId>(value => new RequestId(value), id => id.Value));
        options.Converters.Add(new IdConverter<DecisionId>(value => new DecisionId(value), id => id.Value));
        options.Converters.Add(new IdConverter<DelegationId>(value => new DelegationId(value), id => id.Value));
        options.Converters.Add(new IdConverter<ApprovalEventId>(value => new ApprovalEventId(value), id => id.Value));
        options.MakeReadOnly();
        return options;
    }

    private static void Cases(JsonTypeInfo info)
    {
        if (info.Type == typeof(Condition))
        {
            info.PolymorphismOptions = Polymorphism(
                typeof(Condition.AllOf),
                typeof(Condition.AnyOf),
                typeof(Condition.Negate),
                typeof(Condition.Compare),
                typeof(Condition.OneOf),
                typeof(Condition.RequesterHas),
                typeof(Condition.SubjectHas));
        }
        else if (info.Type == typeof(ApproverReference))
        {
            info.PolymorphismOptions = Polymorphism(typeof(ApproverReference.Permission), typeof(ApproverReference.Person));
        }
        else if (info.Type == typeof(FieldValue))
        {
            info.PolymorphismOptions = Polymorphism(
                typeof(FieldValue.PlainText),
                typeof(FieldValue.WholeNumber),
                typeof(FieldValue.DecimalNumber),
                typeof(FieldValue.Flag));
        }
    }

    /// <summary>The cases of a type with cases, each written with its name in camel case as its member <c>case</c>.</summary>
    private static JsonPolymorphismOptions Polymorphism(params Type[] cases)
    {
        JsonPolymorphismOptions options = new() { TypeDiscriminatorPropertyName = "case" };
        foreach (Type type in cases)
        {
            options.DerivedTypes.Add(new JsonDerivedType(type, JsonNamingPolicy.CamelCase.ConvertName(type.Name)));
        }

        return options;
    }

    /// <summary>An identifier written as its UUID's string.</summary>
    private sealed class IdConverter<T>(Func<Guid, T> make, Func<T, Guid> value) : JsonConverter<T>
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType == JsonTokenType.String && Guid.TryParse(reader.GetString(), out Guid id)
                ? make(id)
                : throw new JsonException("An identifier is not a UUID's string.");
        }

        public override void Write(Utf8JsonWriter writer, T id, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);
            writer.WriteStringValue(value(id));
        }
    }
}