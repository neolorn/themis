using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Themis.Types;

/// <summary>
/// Writes a duration, a deadline's <c>After</c>, as its whole number of seconds (<c>05</c>
/// APR-ENG-006); a definition within its rule holds whole seconds alone (<c>02</c> APR-TYPE-004).
/// </summary>
internal sealed class WholeSecondsConverter : JsonConverter<TimeSpan>
{
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return TimeSpan.FromSeconds(reader.GetInt64());
    }

    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteNumberValue(value.Ticks / TimeSpan.TicksPerSecond);
    }
}