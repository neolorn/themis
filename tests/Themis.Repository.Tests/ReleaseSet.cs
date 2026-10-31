using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Themis.Repository.Tests;

/// <summary>
/// What the release gate compares beside the public surface (<c>03</c> APR-LIB-006): the codes,
/// the configuration keys, the spellings of the enumerations, statuses and event kinds, and the
/// metric names and kinds, as <c>build/gates/release-baseline.json</c> holds them.
/// </summary>
internal sealed record ReleaseSet(
    IReadOnlyCollection<string> Codes,
    IReadOnlyCollection<string> ConfigurationKeys,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Spellings,
    IReadOnlyDictionary<string, string> Metrics)
{
    /// <summary>The set before the first final release, which holds nothing.</summary>
    internal static ReleaseSet Empty { get; } = new([], [], new Dictionary<string, IReadOnlyDictionary<string, string>>(), new Dictionary<string, string>());

    /// <summary>
    /// The set the built assemblies beside the test run hold, read as the contract tests of
    /// <c>05</c> APR-ENG-007 read it: the constants of the codes, the paths of the options
    /// bound under <c>Themis</c>, the spellings of the public enumerations, and the instruments
    /// the library's meter publishes.
    /// </summary>
    internal static ReleaseSet Read()
    {
        Assembly engine = typeof(ApprovalsServiceCollectionExtensions).Assembly;
        string[] codes = [.. engine.GetType("Themis.Codes", throwOnError: true)!
            .GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!)];
        string[] keys = [.. Paths(engine.GetType("Themis.Operations.ApprovalsOptions", throwOnError: true)!, "Themis")];
        Dictionary<string, IReadOnlyDictionary<string, string>> spellings = [];
        foreach (Type enumeration in typeof(Core.EventKind).Assembly.GetExportedTypes().Where(type => type.IsEnum))
        {
            spellings[enumeration.Name] = enumeration.GetFields(BindingFlags.Static | BindingFlags.Public)
                .ToDictionary(field => field.Name, field => field.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name ?? string.Empty, StringComparer.Ordinal);
        }

        return new ReleaseSet(codes, keys, spellings, Instruments(engine.GetType("Themis.Operations.ApprovalsMetrics", throwOnError: true)!));
    }

    internal static ReleaseSet Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        return new ReleaseSet(
            [.. Member(root, "codes").EnumerateArray().Select(code => code.GetString()!)],
            [.. Member(root, "configurationKeys").EnumerateArray().Select(key => key.GetString()!)],
            Member(root, "spellings").EnumerateObject().ToDictionary(
                enumeration => enumeration.Name,
                enumeration => (IReadOnlyDictionary<string, string>)enumeration.Value.EnumerateObject().ToDictionary(member => member.Name, member => member.Value.GetString()!, StringComparer.Ordinal),
                StringComparer.Ordinal),
            Member(root, "metrics").EnumerateObject().ToDictionary(metric => metric.Name, metric => metric.Value.GetString()!, StringComparer.Ordinal));
    }

    /// <summary>The set as the release commit writes it: each list and object in ordinal order, indented by two spaces, ending in a line feed.</summary>
    internal string Write()
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = true, NewLine = "\n" }))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("codes");
            foreach (string code in Codes.Order(StringComparer.Ordinal))
            {
                writer.WriteStringValue(code);
            }

            writer.WriteEndArray();
            writer.WriteStartArray("configurationKeys");
            foreach (string key in ConfigurationKeys.Order(StringComparer.Ordinal))
            {
                writer.WriteStringValue(key);
            }

            writer.WriteEndArray();
            writer.WriteStartObject("spellings");
            foreach (KeyValuePair<string, IReadOnlyDictionary<string, string>> enumeration in Spellings.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                writer.WriteStartObject(enumeration.Key);
                foreach (KeyValuePair<string, string> member in enumeration.Value.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    writer.WriteString(member.Key, member.Value);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.WriteStartObject("metrics");
            foreach (KeyValuePair<string, string> metric in Metrics.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                writer.WriteString(metric.Key, metric.Value);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    private static JsonElement Member(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out JsonElement member) ? member : throw new InvalidDataException($"The release baseline has no member {name}.");
    }

    /// <summary>The configuration paths of the properties of <paramref name="options"/>, a nested options class standing for a section.</summary>
    private static IEnumerable<string> Paths(Type options, string prefix)
    {
        foreach (PropertyInfo property in options.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            string path = $"{prefix}:{property.Name}";
            if (property.PropertyType.Name.EndsWith("Options", StringComparison.Ordinal) && property.PropertyType.Namespace == options.Namespace)
            {
                foreach (string nested in Paths(property.PropertyType, path))
                {
                    yield return nested;
                }
            }
            else
            {
                yield return path;
            }
        }
    }

    /// <summary>The name and kind of each instrument the library's meter publishes, its kind as <c>06</c> section 7 writes it ("Counter", "Observable gauge").</summary>
    private static Dictionary<string, string> Instruments(Type metrics)
    {
        Dictionary<string, string> instruments = new(StringComparer.Ordinal);
        using RecordingMeterFactory factory = new();
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, _) =>
        {
            if (factory.Created.Contains(instrument.Meter))
            {
                instruments[instrument.Name] = Kind(instrument.GetType());
            }
        };
        listener.Start();
        using IDisposable created = (IDisposable)Activator.CreateInstance(metrics, [factory])!;
        return instruments;
    }

    private static string Kind(Type instrument)
    {
        string name = instrument.Name[..instrument.Name.IndexOf('`', StringComparison.Ordinal)];
        StringBuilder kind = new();
        foreach (char letter in name)
        {
            if (char.IsUpper(letter) && kind.Length > 0)
            {
                kind.Append(' ').Append(char.ToLowerInvariant(letter));
            }
            else
            {
                kind.Append(letter);
            }
        }

        return kind.ToString().Replace("Up down", "Up-down", StringComparison.Ordinal).Replace("up down", "up-down", StringComparison.Ordinal);
    }

    /// <summary>A meter factory that keeps each meter it makes, so the instruments of the library's own meter are told from any other's.</summary>
    private sealed class RecordingMeterFactory : IMeterFactory
    {
        internal List<Meter> Created { get; } = [];

        public Meter Create(MeterOptions options)
        {
            Meter meter = new(options);
            Created.Add(meter);
            return meter;
        }

        public void Dispose()
        {
            foreach (Meter meter in Created)
            {
                meter.Dispose();
            }
        }
    }
}