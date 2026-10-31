using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Themis.Types;

/// <summary>
/// Writes a decimal without trailing zeros or exponent (<c>1000.5</c>, <c>0</c>), so two
/// decimals equal but for their scale have one canonical form (<c>05</c> APR-ENG-006).
/// </summary>
internal sealed class DecimalWithoutTrailingZerosConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.GetDecimal();
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteNumberValue(WithoutTrailingZeros(value));
    }

    /// <summary>
    /// <paramref name="value"/> at the least scale that keeps it; zero, of either sign, as
    /// <c>0</c>. The writer writes a decimal in fixed-point notation, never with an exponent.
    /// </summary>
    internal static decimal WithoutTrailingZeros(decimal value)
    {
        if (value == 0m)
        {
            return 0m;
        }

        while (value.Scale > 0 && decimal.Round(value, value.Scale - 1) == value)
        {
            value = decimal.Round(value, value.Scale - 1);
        }

        return value;
    }
}