using System.Collections.Generic;

namespace Themis.Repository.Tests;

/// <summary>
/// A type a built assembly makes public, as its metadata declares it. The name is written as
/// the public API files write it, a nested type after a dot and a generic type with its arity;
/// the interfaces are those the type itself declares it implements.
/// </summary>
internal sealed record PublicType(string Name, PublicTypeKind Kind, bool IsSealed, bool IsAbstract, IReadOnlyList<PublicMethod> Methods, IReadOnlyList<string> Interfaces);