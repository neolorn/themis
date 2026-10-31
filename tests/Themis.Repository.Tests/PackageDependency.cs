namespace Themis.Repository.Tests;

/// <summary>
/// A dependency a packed package's manifest names: the package's identifier and the version
/// range, as NuGet writes it (<c>[1.2.3]</c> exactly, <c>1.2.3</c> that version or later).
/// </summary>
internal sealed record PackageDependency(string Id, string Version);