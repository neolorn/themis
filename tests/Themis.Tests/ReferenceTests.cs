using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Themis.Core;
using Themis.Operations;

using Xunit;

namespace Themis.Tests;

/// <summary>The names the library uses checked against <c>docs/spec/06-reference.md</c> (<c>05</c> APR-ENG-007).</summary>
[Trait("Category", "unit")]
public sealed partial class ReferenceTests
{
    [Fact]
    public void APR_ENG_007_AC1_EveryCodeTheLibraryAnswersIsInSection1AndSpeltAsTheRuleGives()
    {
        string[] codes = [.. typeof(Codes).GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!)];
        IReadOnlyList<string> listed = Reference.LeadingNames(1);

        Assert.NotEmpty(codes);
        Assert.All(codes, code => Assert.Contains(code, listed));
        Assert.All(codes, code => Assert.Matches(CodeForm(), code));
    }

    [Fact]
    public void APR_ENG_007_AC1_EveryConfigurationKeyTheLibraryBindsIsInSection4()
    {
        IEnumerable<string> bound = Paths(typeof(ApprovalsOptions), "Themis");

        Assert.Equal(Reference.FirstColumn(4).Order(StringComparer.Ordinal), bound.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void APR_ENG_007_AC1_EveryEnumerationIsSpeltAsSection2SpellsIt()
    {
        Type[] enumerations = [.. typeof(EventKind).Assembly.GetExportedTypes().Where(type => type.IsEnum)];

        Assert.NotEmpty(enumerations);
        Assert.All(enumerations, enumeration =>
        {
            IReadOnlyList<(string Member, string Spelling)> spelt = [.. enumeration
                .GetFields(BindingFlags.Static | BindingFlags.Public)
                .OrderBy(field => Convert.ToInt64(field.GetRawConstantValue(), System.Globalization.CultureInfo.InvariantCulture))
                .Select(field => (field.Name, field.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name ?? string.Empty))];
            Assert.Equal(Reference.Spellings(enumeration.Name), spelt);
        });
    }

    [Fact]
    public void APR_ENG_007_AC1_EveryAlertConditionIsSpeltAsSection5SpellsIt()
    {
        Assert.Equal(Reference.FirstColumn(5), AlertState.Conditions);
    }

    [Fact]
    public async Task APR_ENG_007_AC1_EveryInstrumentATestHostsMeterPublishesIsInSection7()
    {
        List<Instrument> published = [];
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, _) =>
        {
            lock (published)
            {
                published.Add(instrument);
            }
        };
        listener.Start();
        await using TestHost host = TestHost.Build();

        Meter meter = host.Services.GetRequiredService<ApprovalsMetrics>().RequestsCreated.Meter;

        string[] own;
        lock (published)
        {
            own = [.. published.Where(instrument => ReferenceEquals(instrument.Meter, meter)).Select(instrument => instrument.Name)];
        }

        Assert.NotEmpty(own);
        Assert.All(own, name => Assert.Contains(name, Reference.FirstColumn(7)));
    }

    /// <summary>The configuration paths of the properties of <paramref name="options"/>, a nested options class standing for a section.</summary>
    private static IEnumerable<string> Paths(Type options, string prefix)
    {
        foreach (PropertyInfo property in options.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            string path = $"{prefix}:{property.Name}";
            if (property.PropertyType.Name.EndsWith("Options", StringComparison.Ordinal) && property.PropertyType.Namespace == typeof(ApprovalsOptions).Namespace)
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

    [GeneratedRegex(@"^approvals(\.[a-z]+)+$", RegexOptions.CultureInvariant)]
    private static partial Regex CodeForm();
}