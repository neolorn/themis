using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Themis.Core;

namespace Themis.Requests;

/// <summary>
/// Thrown inside an operation's transaction to refuse it with <see cref="Failure"/>, so the
/// transaction, or the savepoint on a host's, is rolled back and nothing of it is written;
/// the operation answers the failure (<c>05</c> APR-ENG-003). It never leaves the library.
/// </summary>
[SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification = "An internal signal that carries its failure, made by its factories alone and caught inside the library.")]
[SuppressMessage("Design", "CA1064:Exceptions should be public", Justification = "It never leaves the library: each operation catches it and answers its failure.")]
internal sealed class Refusal(Failure failure) : Exception(failure.Code)
{
    internal Failure Failure { get; } = failure;

    /// <summary>A refusal with <paramref name="code"/> and no details.</summary>
    internal static Refusal Of(string code)
    {
        return new Refusal(new Failure { Code = code });
    }

    /// <summary>A refusal with <paramref name="code"/> and the one detail <paramref name="name"/>, <paramref name="value"/>.</summary>
    internal static Refusal Of(string code, string name, string value)
    {
        Dictionary<string, string> details = new(StringComparer.Ordinal) { [name] = value };
        return new Refusal(new Failure { Code = code, Details = details.ToFrozenDictionary(StringComparer.Ordinal) });
    }
}