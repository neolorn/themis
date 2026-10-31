using System.Collections.Generic;

namespace Themis.Repository.Tests;

/// <summary>
/// A method a public type makes visible outside its assembly, with the full names of its
/// return type and its parameters' types.
/// </summary>
internal sealed record PublicMethod(string Name, string ReturnType, IReadOnlyList<string> ParameterTypes);