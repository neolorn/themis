using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace Themis.Repository.Tests;

/// <summary>
/// The reflection <c>05</c> APR-ENG-004 rules out, read from what each method body of a built
/// assembly uses: a lookup or call of members by name through <c>System.Reflection</c>,
/// <c>Activator</c>, <c>Type.GetType</c> or <c>Delegate.CreateDelegate</c>, and
/// <c>System.Reflection.Metadata</c>, which only the tests read. Generated code, which the
/// criterion allows, is left out.
/// </summary>
internal static class ReflectionRules
{
    private static readonly FrozenSet<string> LookupTypes =
        FrozenSet.Create(StringComparer.Ordinal, "System.Type", "System.Reflection.TypeInfo", "System.Reflection.IReflect", "System.Reflection.Assembly", "System.Reflection.Module");

    private static readonly FrozenSet<string> Lookups = FrozenSet.Create(
        StringComparer.Ordinal,
        "GetMethod", "GetMethods", "GetProperty", "GetProperties", "GetField", "GetFields", "GetMember", "GetMembers",
        "GetEvent", "GetEvents", "GetConstructor", "GetConstructors", "GetNestedType", "GetNestedTypes", "GetInterface",
        "GetDefaultMembers", "FindMembers", "InvokeMember", "GetDeclaredMethod", "GetDeclaredMethods", "GetDeclaredProperty",
        "GetDeclaredField", "GetDeclaredEvent", "GetDeclaredNestedType", "GetType", "CreateInstance");

    private static readonly FrozenSet<string> WholeTypes =
        FrozenSet.Create(StringComparer.Ordinal, "System.Activator", "System.Reflection.RuntimeReflectionExtensions", "System.Reflection.TypeExtensions");

    private static readonly FrozenSet<(string Type, string Member)> Members = new[] { ("System.Delegate", "CreateDelegate") }.ToFrozenSet();

    internal static IEnumerable<string> Violations(string assembly, IEnumerable<MemberUse> uses, IEnumerable<string> assemblies)
    {
        foreach (MemberUse use in uses.Where(use => !use.Generated).Distinct())
        {
            bool lookup = LookupTypes.Contains(use.Type) && Lookups.Contains(use.Member);
            if (lookup || WholeTypes.Contains(use.Type) || Members.Contains((use.Type, use.Member)))
            {
                yield return $"{assembly} looks members up by name: {use.Type}.{use.Member}, in {use.Method}.";
            }
        }

        if (assemblies.Contains("System.Reflection.Metadata", StringComparer.Ordinal))
        {
            yield return $"{assembly} references System.Reflection.Metadata, which only the tests read.";
        }
    }
}