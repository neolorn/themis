using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace Themis.Repository.Tests;

/// <summary>
/// The packages of the first release (<c>00</c> section 2.2, <c>03</c> APR-LIB-001), each a
/// project under <c>src/</c>.
/// </summary>
internal static class Packages
{
    internal const string Core = "Themis.Core";

    internal const string Engine = "Themis";

    internal const string Testing = "Themis.Testing";

    internal const string Cli = "Themis.Cli";

    /// <summary>The test project over the whole repository, the one with no source project.</summary>
    internal const string RepositoryTests = "Themis.Repository.Tests";

    internal static IReadOnlyList<string> All { get; } = [Core, Engine, Testing, Cli];

    /// <summary>The packages whose projects the repository holds from phase 0; <c>Themis.Cli</c>'s comes with phase 1 (plan).</summary>
    internal static IReadOnlyList<string> Present { get; } = [Core, Engine, Testing];

    /// <summary>The folders a project may hold, by project (<c>05</c> APR-ENG-001); every other project is flat.</summary>
    internal static FrozenDictionary<string, IReadOnlyList<string>> Folders { get; } = new Dictionary<string, IReadOnlyList<string>>
    {
        [Engine] = ["Types", "Requests", "Operations", "Migrations"],
    }.ToFrozenDictionary(StringComparer.Ordinal);

    internal static string TestProject(string package)
    {
        return package + ".Tests";
    }
}