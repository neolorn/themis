using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Themis.Repository.Tests;

/// <summary>
/// A project a test writes under <c>tmp/</c> and builds or restores. Under the repository's
/// root, it takes the repository's own shared settings, package versions and sources.
/// </summary>
internal sealed class ProbeProject(ScratchFolder folder, string name)
{
    internal string Folder => Path.Combine(folder.Path, name);

    internal string File => Path.Combine(Folder, name + ".csproj");

    internal string Assembly => Path.Combine(Folder, "bin", "Release", "net10.0", name + ".dll");

    internal void WriteProject(string body)
    {
        folder.Write(Path.Combine(name, name + ".csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
            {body}
            </Project>
            """);
    }

    internal void WriteSource(string fileName, string content)
    {
        folder.Write(Path.Combine(name, fileName), content);
    }

    internal Task<ProcessResult> BuildAsync(CancellationToken cancellationToken, params string[] properties)
    {
        return Processes.DotnetAsync(Folder, cancellationToken, ["build", File, "-c", "Release", "-nodeReuse:false", .. properties]);
    }

    internal Task<ProcessResult> RestoreAsync(CancellationToken cancellationToken, params string[] options)
    {
        return Processes.DotnetAsync(Folder, cancellationToken, ["restore", File, .. options]);
    }
}