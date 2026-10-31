using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace Themis.Repository.Tests;

/// <remarks>
/// The rules of the contract tests, run over an assembly built to break them, so a public
/// type that breaks one in a package is found.
/// </remarks>
[Trait("Category", "integration")]
public sealed class AssemblyContractProbeTests
{
    [Fact]
    public async Task APR_ENG_004_AC1_PublicClassNeitherSealedNorAbstractIsFound()
    {
        IReadOnlyList<PublicType> types = await BuildProbeAsync(TestContext.Current.CancellationToken);

        string[] violations = [.. SurfaceRules.SealingViolations(types)];

        Assert.Equal(["Probe.Open is a public class neither sealed nor abstract."], violations);
    }

    [Fact]
    public async Task APR_ENG_004_AC1_PublicAsynchronousMethodWithoutATokenIsFound()
    {
        IReadOnlyList<PublicType> types = await BuildProbeAsync(TestContext.Current.CancellationToken);

        string[] violations = [.. SurfaceRules.TokenViolations(types)];

        Assert.Equal(
            [
                "Probe.Runner.RunAsync is a public asynchronous method without a CancellationToken.",
                "Probe.Closer.DisposeAsync is a public asynchronous method without a CancellationToken.",
            ],
            violations);
    }

    [Fact]
    public async Task APR_ENG_007_AC1_PublicTypeNamedAfterTheLibraryIsFound()
    {
        IReadOnlyList<PublicType> types = await BuildProbeAsync(TestContext.Current.CancellationToken);

        string[] violations = [.. SurfaceRules.NameViolations(types)];

        Assert.Equal(["Probe.ThemisClock is a public type whose name holds the library's name."], violations);
    }

    [Fact]
    public async Task APR_ENG_004_AC2_LookupOfMembersByNameIsFound()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        ProbeProject probe = await WriteAndBuildAsync(folder, cancellationToken);

        string[] violations = [.. ReflectionRules.Violations("Probe.dll", AssemblySurface.MemberUses(probe.Assembly), AssemblySurface.AssemblyReferences(probe.Assembly))];

        Assert.Equal(
            [
                "Probe.dll looks members up by name: System.Type.GetMethod, in Probe.Lookups.Find.",
                "Probe.dll looks members up by name: System.Activator.CreateInstance, in Probe.Lookups.Find.",
                "Probe.dll looks members up by name: System.Type.GetType, in Probe.Lookups.Find.",
                "Probe.dll looks members up by name: System.Delegate.CreateDelegate, in Probe.Lookups.Find.",
            ],
            violations);
    }

    [Fact]
    public async Task APR_ENG_004_AC2_LookupsOfGeneratedCodeAreLeftOut()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        ProbeProject probe = await WriteAndBuildAsync(folder, cancellationToken);

        MemberUse[] generated = [.. AssemblySurface.MemberUses(probe.Assembly).Where(use => use.Generated)];
        string[] violations = [.. ReflectionRules.Violations("Probe.dll", generated, [])];

        Assert.Contains(generated, use => string.Equals(use.Type, "System.Type", StringComparison.Ordinal) && string.Equals(use.Member, "GetProperty", StringComparison.Ordinal));
        Assert.All(generated, use => Assert.StartsWith("Probe.ShapeContext.", use.Method, StringComparison.Ordinal));
        Assert.Empty(violations);
    }

    [Fact]
    public async Task APR_LIB_002_AC2_PublicTypeTheFilesDoNotListIsFound()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        ProbeProject probe = await WriteAndBuildAsync(folder, cancellationToken);
        probe.WriteSource("PublicAPI.Shipped.txt", "#nullable enable\nProbe.Open\nProbe.Open.Open() -> void\nProbe.Listed\n");
        probe.WriteSource("PublicAPI.Unshipped.txt", "#nullable enable\nProbe.Runner\n*REMOVED*Probe.Listed\nProbe.ThemisClock\nProbe.Disposer\nProbe.Lookups\nProbe.Closer\nProbe.Sources\n");

        string[] violations = [.. SurfaceRules.ListingViolations(probe.Folder, AssemblySurface.PublicTypes(probe.Assembly))];

        Assert.Equal(["Probe declares the public type Probe.Result`1, which its public API files do not list."], violations);
    }

    [Fact]
    public async Task APR_ENG_001_AC1_GrantOfEveryFormIsReadFromTheBuiltAssembly()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        ProbeProject probe = new(folder, Packages.Core);
        probe.WriteProject("""
              <ItemGroup>
                <InternalsVisibleTo Include="Themis.Core.Tests" />
                <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleToAttribute">
                  <_Parameter1>Themis.Cli</_Parameter1>
                </AssemblyAttribute>
              </ItemGroup>
            """);
        probe.WriteSource("Grants.cs", """
            [assembly: System.Runtime.CompilerServices.InternalsVisibleToAttribute("Themis.Testing")]
            """);
        ProcessResult build = await probe.BuildAsync(cancellationToken, "-p:TreatWarningsAsErrors=false", "-p:MSBuildTreatWarningsAsErrors=false");
        Assert.True(build.ExitCode == 0, build.Output);

        string[] violations = [.. ReferenceRules.GrantViolations("src/Themis.Core/Themis.Core.csproj", Packages.Core, AssemblySurface.Grants(probe.Assembly))];

        Assert.Equal(
            [
                "src/Themis.Core/Themis.Core.csproj grants its internals to Themis.Cli, which APR-ENG-001 does not allow.",
                "src/Themis.Core/Themis.Core.csproj grants its internals to Themis.Testing, which APR-ENG-001 does not allow.",
            ],
            violations.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task APR_LIB_002_AC2_PublicTypeNoTableGivesIsFound()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        ProbeProject probe = new(folder, Packages.Core);
        probe.WriteProject(string.Empty);
        probe.WriteSource("Types.cs", """
            namespace Themis.Core;

            /// <summary>A type the tables give.</summary>
            public sealed record Target;

            /// <summary>A type with a case the tables give and one they do not.</summary>
            public abstract record ApproverReference
            {
                /// <summary>A case the tables give.</summary>
                public sealed record Permission : ApproverReference;

                /// <summary>A case the tables do not give.</summary>
                public sealed record Group : ApproverReference;
            }

            /// <summary>A type the tables do not give.</summary>
            public sealed class Probe;
            """);
        ProcessResult build = await probe.BuildAsync(cancellationToken, "-p:TreatWarningsAsErrors=false", "-p:MSBuildTreatWarningsAsErrors=false");
        Assert.True(build.ExitCode == 0, build.Output);

        string[] violations = [.. PublicTypeTable.Violations(Packages.Core, AssemblySurface.PublicTypes(probe.Assembly))];

        Assert.Equal(
            [
                "Themis.Core declares the public type Themis.Core.ApproverReference.Group, which no table of 03 APR-LIB-002 or APR-LIB-007 gives.",
                "Themis.Core declares the public type Themis.Core.Probe, which no table of 03 APR-LIB-002 or APR-LIB-007 gives.",
            ],
            violations);
    }

    private static async Task<IReadOnlyList<PublicType>> BuildProbeAsync(CancellationToken cancellationToken)
    {
        using ScratchFolder folder = new();
        ProbeProject probe = await WriteAndBuildAsync(folder, cancellationToken);
        return AssemblySurface.PublicTypes(probe.Assembly);
    }

    private static async Task<ProbeProject> WriteAndBuildAsync(ScratchFolder folder, CancellationToken cancellationToken)
    {
        ProbeProject probe = new(folder, "Probe");
        probe.WriteProject(string.Empty);
        probe.WriteSource("Probe.cs", """
            using System;
            using System.Text.Json.Serialization;
            using System.Threading;
            using System.Threading.Tasks;

            namespace Probe;

            /// <summary>A class neither sealed nor abstract.</summary>
            public class Open
            {
            }

            /// <summary>Holds asynchronous methods.</summary>
            public static class Runner
            {
                /// <summary>Takes no token.</summary>
                /// <returns>A completed task.</returns>
                public static Task RunAsync()
                {
                    return Task.CompletedTask;
                }

                /// <summary>Takes a token.</summary>
                /// <param name="cancellationToken">Cancels it.</param>
                /// <returns>A completed task.</returns>
                public static ValueTask StopAsync(CancellationToken cancellationToken)
                {
                    return ValueTask.CompletedTask;
                }
            }

            /// <summary>A type named after the library.</summary>
            public sealed class ThemisClock
            {
            }

            /// <summary>Disposes without a token, as IAsyncDisposable has it.</summary>
            public sealed class Disposer : IAsyncDisposable
            {
                /// <inheritdoc/>
                public ValueTask DisposeAsync()
                {
                    return ValueTask.CompletedTask;
                }
            }

            /// <summary>A generic type.</summary>
            /// <typeparam name="T">Anything.</typeparam>
            public sealed class Result<T>
            {
            }

            /// <summary>Looks members up by name.</summary>
            public static class Lookups
            {
                /// <summary>Finds a method and a type by name and makes an instance.</summary>
                /// <param name="type">The type to look into.</param>
                /// <returns>What it found.</returns>
                public static object?[] Find(Type type)
                {
                    return [type.GetMethod("Trim"), Activator.CreateInstance(type), Type.GetType("System.String"), Delegate.CreateDelegate(typeof(Func<string>), type, "ToString")];
                }
            }

            /// <summary>Disposes asynchronously, though it is no IAsyncDisposable.</summary>
            public sealed class Closer
            {
                /// <summary>Takes no token.</summary>
                /// <returns>A completed task.</returns>
                public ValueTask DisposeAsync()
                {
                    return ValueTask.CompletedTask;
                }
            }

            /// <summary>Answers types of the tasks namespace that are not asynchronous.</summary>
            public static class Sources
            {
                /// <summary>Makes a source.</summary>
                /// <returns>The source.</returns>
                public static TaskCompletionSource Source()
                {
                    return new TaskCompletionSource();
                }

                /// <summary>Makes a factory.</summary>
                /// <returns>The factory.</returns>
                public static TaskFactory Factory()
                {
                    return new TaskFactory();
                }
            }

            /// <summary>A shape the serializer's source generator writes a context for.</summary>
            /// <param name="Width">The width.</param>
            internal sealed record Shape(int Width);

            /// <summary>The generated context, whose code looks members up by name.</summary>
            [JsonSerializable(typeof(Shape))]
            internal sealed partial class ShapeContext : JsonSerializerContext;
            """);

        // A probe built to break rules is built with its warnings left as warnings.
        ProcessResult build = await probe.BuildAsync(cancellationToken, "-p:TreatWarningsAsErrors=false", "-p:MSBuildTreatWarningsAsErrors=false");
        Assert.True(build.ExitCode == 0, build.Output);
        return probe;
    }
}