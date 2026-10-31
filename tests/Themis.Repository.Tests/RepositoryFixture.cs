using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Themis.Repository.Tests;

/// <summary>
/// A repository tree under <c>tmp/</c> that keeps every rule: copies of the repository's
/// project files and lockfiles, its solution, its shared settings, package sources and tool
/// manifest, without any source file. A test breaks one rule in it and checks the rule finds
/// that.
/// </summary>
internal sealed class RepositoryFixture : IDisposable
{
    private static readonly IReadOnlyList<string> SharedFiles =
    [
        "Themis.sln", "Directory.Build.props", "Directory.Packages.props", "global.json", "nuget.config", ".gitignore",
        ".config/dotnet-tools.json", "build/gates/gitleaks.toml",
    ];

    private readonly ScratchFolder _folder = new();

    internal RepositoryFixture()
    {
        foreach (string file in SharedFiles)
        {
            Write(file, File.ReadAllText(RepositoryRoot.Combine(file)));
        }

        foreach (ProjectFile project in ProjectFile.ReadAll(RepositoryRoot.Path))
        {
            Write(project.RelativePath, File.ReadAllText(project.Path));
            string lockFile = Path.Combine(project.Folder, "packages.lock.json");
            if (File.Exists(lockFile))
            {
                Write(Path.Combine(Path.GetDirectoryName(project.RelativePath)!, "packages.lock.json"), File.ReadAllText(lockFile));
            }
        }
    }

    internal string Root => _folder.Path;

    internal string Write(string relativePath, string content)
    {
        return _folder.Write(relativePath, content);
    }

    /// <summary>Writes a project file of the given body, with an empty lockfile beside it.</summary>
    internal void WriteProject(string relativePath, string body)
    {
        Write(relativePath, $"""
            <Project Sdk="Microsoft.NET.Sdk">
            {body}
            </Project>
            """);
        Write(Path.Combine(Path.GetDirectoryName(relativePath)!, "packages.lock.json"), """{ "version": 2, "dependencies": { "net10.0": { } } }""");
    }

    internal void WriteSource(string name, IReadOnlyList<string> projects)
    {
        WriteProject($"src/{name}/{name}.csproj", $"""
              <ItemGroup>
                <PackageReference Include="Microsoft.CodeAnalysis.PublicApiAnalyzers" PrivateAssets="all" />
                <PackageReference Include="MinVer" PrivateAssets="all" />
              </ItemGroup>
            {References(projects)}
            """);
    }

    internal void WriteTest(string name, IReadOnlyList<string> projects)
    {
        WriteProject($"tests/{name}/{name}.csproj", $"""
              <ItemGroup>
                <PackageReference Include="xunit.v3" />
              </ItemGroup>
            {References(projects)}
            """);
    }

    internal void WriteSolution(IReadOnlyList<string> projects)
    {
        IEnumerable<string> lines = projects.Select(project =>
            $"Project(\"{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}\") = \"{Path.GetFileNameWithoutExtension(project)}\", \"{project.Replace('/', '\\')}\", \"{{{Guid.CreateVersion7().ToString().ToUpperInvariant()}}}\"\nEndProject");
        Write("Themis.sln", string.Join('\n', lines) + "\n");
    }

    public void Dispose()
    {
        _folder.Dispose();
    }

    private static string References(IReadOnlyList<string> projects)
    {
        if (projects.Count == 0)
        {
            return string.Empty;
        }

        string items = string.Join('\n', projects.Select(project => $"    <ProjectReference Include=\"..\\..\\src\\{project}\\{project}.csproj\" />"));
        return $"  <ItemGroup>\n{items}\n  </ItemGroup>";
    }
}