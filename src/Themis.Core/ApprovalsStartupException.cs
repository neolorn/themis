using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace Themis.Core;

/// <summary>
/// The exception in which a "(startup)" code of the library's reference is thrown while the
/// host starts: a configuration value, a seam, a secret or the schema outside its rule
/// stops the start before any hosted service starts or a request is served (<c>03</c> APR-LIB-003).
/// </summary>
public sealed class ApprovalsStartupException : Exception
{
    /// <summary>Makes the exception for <paramref name="code"/> with its <paramref name="details"/>.</summary>
    /// <param name="code">The startup code, such as <c>approvals.startup.configurationinvalid</c>.</param>
    /// <param name="details">The code's details, such as <c>key</c>; never a secret, a person or a value.</param>
    /// <exception cref="ArgumentException"><paramref name="code"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> or <paramref name="details"/> is null.</exception>
    public ApprovalsStartupException(string code, IReadOnlyDictionary<string, string> details)
        : base(Describe(code, details))
    {
        Code = code;
        Details = details.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>The startup code.</summary>
    public string Code { get; }

    /// <summary>The code's details, by name.</summary>
    public IReadOnlyDictionary<string, string> Details { get; }

    private static string Describe(string code, IReadOnlyDictionary<string, string> details)
    {
        ArgumentException.ThrowIfNullOrEmpty(code);
        ArgumentNullException.ThrowIfNull(details);
        string named = string.Join(", ", details.OrderBy(detail => detail.Key, StringComparer.Ordinal).Select(detail => $"{detail.Key} {detail.Value}"));
        return details.Count == 0 ? $"The host did not start: {code}." : $"The host did not start: {code} ({named}).";
    }
}