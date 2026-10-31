using System;
using System.IO;

namespace Themis.Repository.Tests;

/// <summary>
/// A new, empty folder under the repository's <c>tmp/</c>, removed when the test ends.
/// </summary>
/// <remarks>
/// Under the repository's root, a project made here takes the repository's own
/// <c>Directory.Build.props</c>, <c>Directory.Packages.props</c> and <c>nuget.config</c>.
/// </remarks>
internal sealed class ScratchFolder : IDisposable
{
    internal ScratchFolder()
    {
        Path = RepositoryRoot.Combine("tmp", "tests", Guid.CreateVersion7().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    internal string Path { get; }

    internal string Write(string relativePath, string content)
    {
        string file = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
        return file;
    }

    public void Dispose()
    {
        if (!Directory.Exists(Path))
        {
            return;
        }

        // Git writes its objects read-only, which Windows will not delete.
        foreach (string file in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(Path, recursive: true);
    }
}