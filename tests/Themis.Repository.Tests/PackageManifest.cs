using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace Themis.Repository.Tests;

/// <summary>
/// What a packed package's <c>.nuspec</c> says of it: the names of its metadata elements, its
/// identifier, version, authors, description and repository address, and its dependencies.
/// </summary>
internal sealed record PackageManifest(
    string Id,
    string Version,
    IReadOnlyList<string> Elements,
    string? Authors,
    string? Description,
    string? RepositoryUrl,
    IReadOnlyList<PackageDependency> Dependencies)
{
    /// <summary>Reads the manifest of the package at <paramref name="path"/>.</summary>
    internal static PackageManifest Read(string path)
    {
        using ZipArchive archive = ZipFile.OpenRead(path);
        ZipArchiveEntry entry = archive.Entries.Single(entry => !entry.FullName.Contains('/', StringComparison.Ordinal) && entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        using Stream stream = entry.Open();
        return Parse(XDocument.Load(stream));
    }

    internal static PackageManifest Parse(XDocument document)
    {
        XElement metadata = document.Root!.Elements().Single(element => element.Name.LocalName is "metadata");
        string? Text(string name) => metadata.Elements().FirstOrDefault(element => string.Equals(element.Name.LocalName, name, StringComparison.Ordinal))?.Value;

        IReadOnlyList<PackageDependency> dependencies = [.. metadata.Descendants()
            .Where(element => element.Name.LocalName is "dependency")
            .Select(element => new PackageDependency(element.Attribute("id")?.Value ?? string.Empty, element.Attribute("version")?.Value ?? string.Empty))];

        return new PackageManifest(
            Text("id") ?? string.Empty,
            Text("version") ?? string.Empty,
            [.. metadata.Elements().Select(element => element.Name.LocalName)],
            Text("authors"),
            Text("description"),
            metadata.Elements().FirstOrDefault(element => element.Name.LocalName is "repository")?.Attribute("url")?.Value,
            dependencies);
    }
}