using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Themis.Operations;

/// <summary>
/// The keys of <c>06</c> section 4, in that section's order, each read from its raw text as
/// the configuration binder reads it, in the invariant culture, and the rule that bounds one
/// key by another (<c>04</c> APR-OPS-001).
/// </summary>
internal static class ConfigurationRules
{
    /// <summary>The configuration section the library binds.</summary>
    internal const string Section = "Themis";

    internal static IReadOnlyList<ConfigurationKey> Keys { get; } = MakeKeys();

    /// <summary>
    /// Answers the first key in <see cref="Keys"/>' order outside its own rule, given each key's
    /// raw value, then the first whose rule over another does not hold; none where every key is
    /// within its rule.
    /// </summary>
    internal static ConfigurationKey? Invalid(Func<string, string?> raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        Dictionary<string, object> values = new(StringComparer.Ordinal);
        foreach (ConfigurationKey key in Keys)
        {
            string? text = raw(key.Path);
            if (text is null && key.Default is null)
            {
                continue;
            }

            object? value = text is null ? key.Default : key.Read(text);
            if (value is null || !key.Within(value))
            {
                return key;
            }

            values[key.Path] = value;
        }

        // Jobs:ClaimTimeout is above three times Timeout, so a claim outlives an item's seam calls.
        bool claimHolds = (TimeSpan)values["Jobs:ClaimTimeout"] > 3 * (TimeSpan)values["Timeout"];
        return claimHolds ? null : Keys.Single(key => key.Path == "Jobs:ClaimTimeout");
    }

    private static ConfigurationKey[] MakeKeys()
    {
        ApprovalsOptions defaults = new();
        return
        [
            Duration("Timeout", TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1), defaults.Timeout),
            Whole("Database:Retries", 0, 10, defaults.Database.Retries),
            Flag("Jobs:Enabled", defaults.Jobs.Enabled),
            Flag("Jobs:Hosted", defaults.Jobs.Hosted),
            Duration("Jobs:Interval", TimeSpan.FromSeconds(5), TimeSpan.FromHours(1), defaults.Jobs.Interval),
            Duration("Jobs:ClaimTimeout", TimeSpan.FromMinutes(1), TimeSpan.FromHours(1), defaults.Jobs.ClaimTimeout),
            Whole("Jobs:BatchSize", 1, 1_000, defaults.Jobs.BatchSize),
            Whole("Jobs:StallFactor", 2, 100, defaults.Jobs.StallFactor),
            Flag("Observer:Enabled", defaults.Observer.Enabled),
            Duration("Observer:LagThreshold", TimeSpan.FromMinutes(1), TimeSpan.FromDays(1), defaults.Observer.LagThreshold),
            Duration("Alerts:Repeat", TimeSpan.FromMinutes(1), TimeSpan.FromDays(1), defaults.Alerts.Repeat),
            Days("Retention:ClosedRequests", 30, 10_950),
        ];
    }

    private static ConfigurationKey Whole(string path, int least, int most, int value)
    {
        return new(
            path,
            text => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int read) ? read : null,
            read => (int)read >= least && (int)read <= most,
            value);
    }

    private static ConfigurationKey Flag(string path, bool value)
    {
        return new(path, text => bool.TryParse(text, out bool read) ? read : null, _ => true, value);
    }

    private static ConfigurationKey Duration(string path, TimeSpan least, TimeSpan most, TimeSpan value)
    {
        return new(
            path,
            text => TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out TimeSpan read) ? read : null,
            read => (TimeSpan)read >= least && (TimeSpan)read <= most,
            value);
    }

    /// <summary>An optional duration in whole days, from <paramref name="least"/> to <paramref name="most"/> days, unset where absent.</summary>
    private static ConfigurationKey Days(string path, int least, int most)
    {
        return new(
            path,
            text => TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out TimeSpan read) ? read : null,
            read => (TimeSpan)read is TimeSpan days && days.Ticks % TimeSpan.TicksPerDay == 0 && days >= TimeSpan.FromDays(least) && days <= TimeSpan.FromDays(most),
            Default: null);
    }
}