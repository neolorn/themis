using System;
using System.IO;

namespace Themis.Repository.Tests;

/// <summary>
/// The repository's root folder, the nearest folder above the test run that holds the solution.
/// </summary>
internal static class RepositoryRoot
{
    internal static string Path { get; } = Find();

    internal static string Combine(params string[] parts)
    {
        return System.IO.Path.Combine([Path, .. parts]);
    }

    private static string Find()
    {
        DirectoryInfo? folder = new(AppContext.BaseDirectory);
        while (folder is not null)
        {
            if (File.Exists(System.IO.Path.Combine(folder.FullName, "Themis.sln")))
            {
                return folder.FullName;
            }

            folder = folder.Parent;
        }

        throw new InvalidOperationException("No folder above the test run holds Themis.sln.");
    }
}