using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Xunit;

namespace Themis.Repository.Tests;

[Trait("Category", "contract")]
public sealed class AssemblyContractTests
{
    public static TheoryData<string> Assemblies => [.. AssemblySurface.ShippedAssemblies().Select(path => Path.GetFileName(path))];

    [Fact]
    public void AssemblySurface_ShippedAssemblies_AreEachPackagesBuiltAssembly()
    {
        IReadOnlyList<string> assemblies = AssemblySurface.ShippedAssemblies();

        Assert.NotEmpty(assemblies);
        Assert.All(assemblies, assembly => Assert.True(File.Exists(assembly), $"{assembly} was not built beside the test run."));
    }

    [Theory]
    [MemberData(nameof(Assemblies))]
    public void APR_ENG_004_AC1_EveryPublicClassIsSealedOrAbstract(string assembly)
    {
        Assert.Empty(SurfaceRules.SealingViolations(AssemblySurface.PublicTypes(Path.Combine(AppContext.BaseDirectory, assembly))));
    }

    [Theory]
    [MemberData(nameof(Assemblies))]
    public void APR_ENG_004_AC1_EveryPublicAsynchronousMethodTakesACancellationToken(string assembly)
    {
        Assert.Empty(SurfaceRules.TokenViolations(AssemblySurface.PublicTypes(Path.Combine(AppContext.BaseDirectory, assembly))));
    }

    [Theory]
    [MemberData(nameof(Assemblies))]
    public void APR_ENG_004_AC2_NoMemberIsLookedUpByName(string assembly)
    {
        string path = Path.Combine(AppContext.BaseDirectory, assembly);

        Assert.Empty(ReflectionRules.Violations(assembly, AssemblySurface.MemberUses(path), AssemblySurface.AssemblyReferences(path)));
    }

    [Theory]
    [MemberData(nameof(Assemblies))]
    public void APR_ENG_007_AC1_NoPublicTypeNameHoldsTheLibrarysName(string assembly)
    {
        Assert.Empty(SurfaceRules.NameViolations(AssemblySurface.PublicTypes(Path.Combine(AppContext.BaseDirectory, assembly))));
    }

    [Theory]
    [MemberData(nameof(Assemblies))]
    public void APR_LIB_002_AC2_EveryPublicTypeIsOneTheTablesGive(string assembly)
    {
        Assert.Empty(PublicTypeTable.Violations(Path.GetFileNameWithoutExtension(assembly), AssemblySurface.PublicTypes(Path.Combine(AppContext.BaseDirectory, assembly))));
    }

    [Theory]
    [MemberData(nameof(Assemblies))]
    public void APR_LIB_002_AC2_PublicApiFilesListExactlyThePublicTypes(string assembly)
    {
        string project = RepositoryRoot.Combine("src", Path.GetFileNameWithoutExtension(assembly));

        Assert.True(File.Exists(Path.Combine(project, "PublicAPI.Shipped.txt")), $"{project} has no PublicAPI.Shipped.txt.");
        Assert.True(File.Exists(Path.Combine(project, "PublicAPI.Unshipped.txt")), $"{project} has no PublicAPI.Unshipped.txt.");
        Assert.Empty(SurfaceRules.ListingViolations(project, AssemblySurface.PublicTypes(Path.Combine(AppContext.BaseDirectory, assembly))));
    }
}