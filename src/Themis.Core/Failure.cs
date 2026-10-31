using System.Collections.Frozen;
using System.Collections.Generic;

namespace Themis.Core;

/// <summary>
/// A refusal an operation answers: a code of the library's reference and its details, such as
/// <c>member</c> for <c>approvals.type.invalid</c> (<c>05</c> APR-ENG-003). Its details name
/// no person, value or secret.
/// </summary>
public sealed record Failure
{
    /// <summary>The code, such as <c>approvals.request.closed</c>.</summary>
    public required string Code { get; init; }

    /// <summary>The code's details, by name; none where the code has none.</summary>
    public IReadOnlyDictionary<string, string> Details { get; init; } = FrozenDictionary<string, string>.Empty;
}