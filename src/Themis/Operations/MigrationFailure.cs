using System.Collections.Generic;

namespace Themis.Operations;

/// <summary>Why <c>themis migrate</c> failed: a "(migrate)" code of <c>06</c> section 1 and its details.</summary>
/// <param name="Code">The code.</param>
/// <param name="Details">The details.</param>
internal sealed record MigrationFailure(string Code, IReadOnlyDictionary<string, string> Details);