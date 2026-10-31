using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Themis.Repository.Tests;

/// <summary>
/// Reads the public types of a built assembly from its metadata, without loading it.
/// </summary>
internal static class AssemblySurface
{
    /// <summary>The built assemblies of the projects <c>src/*/*.csproj</c>, copied beside the test run by its project references.</summary>
    internal static IReadOnlyList<string> ShippedAssemblies()
    {
        return [.. ProjectFile.SourceProjects(RepositoryRoot.Path)
            .Select(project => Path.Combine(AppContext.BaseDirectory, Path.GetFileNameWithoutExtension(project) + ".dll"))];
    }

    internal static IReadOnlyList<PublicType> PublicTypes(string assembly)
    {
        using FileStream stream = File.OpenRead(assembly);
        using PEReader reader = new(stream);
        MetadataReader metadata = reader.GetMetadataReader();
        SignatureNames names = new(metadata);
        List<PublicType> types = [];

        foreach (TypeDefinitionHandle handle in metadata.TypeDefinitions)
        {
            TypeDefinition type = metadata.GetTypeDefinition(handle);
            if (!IsVisible(metadata, type))
            {
                continue;
            }

            List<PublicMethod> methods = [];
            foreach (MethodDefinitionHandle methodHandle in type.GetMethods())
            {
                MethodDefinition method = metadata.GetMethodDefinition(methodHandle);
                MethodAttributes access = method.Attributes & MethodAttributes.MemberAccessMask;
                if (access is not (MethodAttributes.Public or MethodAttributes.Family or MethodAttributes.FamORAssem))
                {
                    continue;
                }

                MethodSignature<string> signature = method.DecodeSignature(names, genericContext: null);
                methods.Add(new PublicMethod(metadata.GetString(method.Name), signature.ReturnType, signature.ParameterTypes));
            }

            TypeAttributes attributes = type.Attributes;
            types.Add(new PublicType(
                SignatureNames.Name(metadata, handle),
                Kind(metadata, type),
                (attributes & TypeAttributes.Sealed) != 0,
                (attributes & TypeAttributes.Abstract) != 0,
                methods,
                [.. type.GetInterfaceImplementations().Select(implementation => names.Interface(metadata.GetInterfaceImplementation(implementation).Interface))]));
        }

        return types;
    }

    /// <summary>
    /// The members each method body of the assembly calls, reads, writes or takes a token of,
    /// read from its IL, each with the method that uses it and whether that method's type is
    /// generated code: a type marked <c>GeneratedCodeAttribute</c>, or nested in one.
    /// </summary>
    internal static IReadOnlyList<MemberUse> MemberUses(string assembly)
    {
        using FileStream stream = File.OpenRead(assembly);
        using PEReader reader = new(stream);
        MetadataReader metadata = reader.GetMetadataReader();
        SignatureNames names = new(metadata);
        List<MemberUse> uses = [];

        foreach (TypeDefinitionHandle typeHandle in metadata.TypeDefinitions)
        {
            TypeDefinition type = metadata.GetTypeDefinition(typeHandle);
            string typeName = SignatureNames.Name(metadata, typeHandle);
            bool generated = IsGenerated(metadata, typeHandle);
            foreach (MethodDefinitionHandle methodHandle in type.GetMethods())
            {
                MethodDefinition method = metadata.GetMethodDefinition(methodHandle);
                if (method.RelativeVirtualAddress == 0)
                {
                    continue;
                }

                string methodName = typeName + "." + metadata.GetString(method.Name);
                BlobReader il = reader.GetMethodBody(method.RelativeVirtualAddress).GetILReader();
                foreach (EntityHandle token in IntermediateLanguage.MemberTokens(il))
                {
                    if (Member(metadata, names, token) is (string memberType, string member))
                    {
                        uses.Add(new MemberUse(memberType, member, methodName, generated));
                    }
                }
            }
        }

        return uses;
    }

    /// <summary>The assemblies the built assembly grants its internals to, as its <c>InternalsVisibleToAttribute</c> values name them.</summary>
    internal static IReadOnlyList<string> Grants(string assembly)
    {
        using FileStream stream = File.OpenRead(assembly);
        using PEReader reader = new(stream);
        MetadataReader metadata = reader.GetMetadataReader();
        List<string> grants = [];

        foreach (CustomAttributeHandle handle in metadata.GetAssemblyDefinition().GetCustomAttributes())
        {
            CustomAttribute attribute = metadata.GetCustomAttribute(handle);
            string? type = attribute.Constructor.Kind switch
            {
                HandleKind.MemberReference => SignatureNames.Name(metadata, metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent),
                HandleKind.MethodDefinition => SignatureNames.Name(metadata, metadata.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType()),
                _ => null,
            };
            if (!string.Equals(type, "System.Runtime.CompilerServices.InternalsVisibleToAttribute", StringComparison.Ordinal))
            {
                continue;
            }

            // The value blob: the prolog 0x0001, then the one string argument (ECMA-335 II.23.3).
            BlobReader value = metadata.GetBlobReader(attribute.Value);
            if (value.ReadUInt16() == 1 && value.ReadSerializedString() is string grantee)
            {
                grants.Add(grantee.Split(',')[0].Trim());
            }
        }

        return grants;
    }

    /// <summary>The names of the assemblies the assembly references.</summary>
    internal static IReadOnlyList<string> AssemblyReferences(string assembly)
    {
        using FileStream stream = File.OpenRead(assembly);
        using PEReader reader = new(stream);
        MetadataReader metadata = reader.GetMetadataReader();
        return [.. metadata.AssemblyReferences.Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name))];
    }

    /// <summary>The type and name of the member a token of the IL names, or none when it names no member.</summary>
    private static (string Type, string Member)? Member(MetadataReader metadata, SignatureNames names, EntityHandle token)
    {
        switch (token.Kind)
        {
            case HandleKind.MemberReference:
                MemberReference reference = metadata.GetMemberReference((MemberReferenceHandle)token);
                string? parent = reference.Parent.Kind switch
                {
                    HandleKind.TypeReference or HandleKind.TypeDefinition => SignatureNames.Name(metadata, (EntityHandle)reference.Parent),
                    HandleKind.TypeSpecification => metadata.GetTypeSpecification((TypeSpecificationHandle)reference.Parent).DecodeSignature(names, genericContext: null),
                    _ => null,
                };
                return parent is null ? null : (parent, metadata.GetString(reference.Name));
            case HandleKind.MethodDefinition:
                MethodDefinition method = metadata.GetMethodDefinition((MethodDefinitionHandle)token);
                return (SignatureNames.Name(metadata, method.GetDeclaringType()), metadata.GetString(method.Name));
            case HandleKind.FieldDefinition:
                FieldDefinition field = metadata.GetFieldDefinition((FieldDefinitionHandle)token);
                return (SignatureNames.Name(metadata, field.GetDeclaringType()), metadata.GetString(field.Name));
            case HandleKind.MethodSpecification:
                return Member(metadata, names, metadata.GetMethodSpecification((MethodSpecificationHandle)token).Method);
            default:
                return null;
        }
    }

    private static bool IsGenerated(MetadataReader metadata, TypeDefinitionHandle handle)
    {
        for (TypeDefinitionHandle current = handle; !current.IsNil; current = metadata.GetTypeDefinition(current).GetDeclaringType())
        {
            foreach (CustomAttributeHandle attributeHandle in metadata.GetTypeDefinition(current).GetCustomAttributes())
            {
                EntityHandle constructor = metadata.GetCustomAttribute(attributeHandle).Constructor;
                string? attribute = constructor.Kind switch
                {
                    HandleKind.MemberReference => SignatureNames.Name(metadata, metadata.GetMemberReference((MemberReferenceHandle)constructor).Parent),
                    HandleKind.MethodDefinition => SignatureNames.Name(metadata, metadata.GetMethodDefinition((MethodDefinitionHandle)constructor).GetDeclaringType()),
                    _ => null,
                };
                if (string.Equals(attribute, "System.CodeDom.Compiler.GeneratedCodeAttribute", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsVisible(MetadataReader metadata, TypeDefinition type)
    {
        TypeAttributes visibility = type.Attributes & TypeAttributes.VisibilityMask;
        return visibility switch
        {
            TypeAttributes.Public => true,
            TypeAttributes.NestedPublic or TypeAttributes.NestedFamily or TypeAttributes.NestedFamORAssem =>
                IsVisible(metadata, metadata.GetTypeDefinition(type.GetDeclaringType())),
            _ => false,
        };
    }

    private static PublicTypeKind Kind(MetadataReader metadata, TypeDefinition type)
    {
        if ((type.Attributes & TypeAttributes.Interface) != 0)
        {
            return PublicTypeKind.Interface;
        }

        string? baseType = type.BaseType.IsNil ? null : SignatureNames.Name(metadata, type.BaseType);
        return baseType switch
        {
            "System.Enum" => PublicTypeKind.Enumeration,
            "System.ValueType" => PublicTypeKind.Struct,
            "System.MulticastDelegate" => PublicTypeKind.Delegate,
            _ => PublicTypeKind.Class,
        };
    }

    /// <summary>Decodes signatures into full type names.</summary>
    private sealed class SignatureNames(MetadataReader metadata) : ISignatureTypeProvider<string, object?>
    {
        /// <summary>The full name of an interface a type implements, a generic one with its arguments.</summary>
        internal string Interface(EntityHandle handle)
        {
            return handle.Kind == HandleKind.TypeSpecification
                ? metadata.GetTypeSpecification((TypeSpecificationHandle)handle).DecodeSignature(this, genericContext: null)
                : Name(metadata, handle);
        }

        internal static string Name(MetadataReader metadata, EntityHandle handle)
        {
            return handle.Kind switch
            {
                HandleKind.TypeDefinition => Name(metadata, (TypeDefinitionHandle)handle),
                HandleKind.TypeReference => Name(metadata, (TypeReferenceHandle)handle),
                _ => handle.Kind.ToString(),
            };
        }

        internal static string Name(MetadataReader metadata, TypeDefinitionHandle handle)
        {
            TypeDefinition type = metadata.GetTypeDefinition(handle);
            string name = metadata.GetString(type.Name);
            TypeDefinitionHandle declaring = type.GetDeclaringType();
            if (!declaring.IsNil)
            {
                return Name(metadata, declaring) + "." + name;
            }

            string space = metadata.GetString(type.Namespace);
            return space.Length == 0 ? name : space + "." + name;
        }

        private static string Name(MetadataReader metadata, TypeReferenceHandle handle)
        {
            TypeReference type = metadata.GetTypeReference(handle);
            string name = metadata.GetString(type.Name);
            if (type.ResolutionScope.Kind == HandleKind.TypeReference)
            {
                return Name(metadata, (TypeReferenceHandle)type.ResolutionScope) + "." + name;
            }

            string space = metadata.GetString(type.Namespace);
            return space.Length == 0 ? name : space + "." + name;
        }

        public string GetArrayType(string elementType, ArrayShape shape)
        {
            return elementType + "[" + new string(',', shape.Rank - 1) + "]";
        }

        public string GetByReferenceType(string elementType)
        {
            return elementType + "&";
        }

        public string GetFunctionPointerType(MethodSignature<string> signature)
        {
            return "delegate*";
        }

        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
        {
            return genericType + "<" + string.Join(",", typeArguments) + ">";
        }

        public string GetGenericMethodParameter(object? genericContext, int index)
        {
            return "!!" + index;
        }

        public string GetGenericTypeParameter(object? genericContext, int index)
        {
            return "!" + index;
        }

        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired)
        {
            return unmodifiedType;
        }

        public string GetPinnedType(string elementType)
        {
            return elementType;
        }

        public string GetPointerType(string elementType)
        {
            return elementType + "*";
        }

        public string GetPrimitiveType(PrimitiveTypeCode typeCode)
        {
            return "System." + typeCode;
        }

        public string GetSZArrayType(string elementType)
        {
            return elementType + "[]";
        }

        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            return Name(metadata, handle);
        }

        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            return Name(metadata, handle);
        }

        public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
        {
            return reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
        }
    }
}