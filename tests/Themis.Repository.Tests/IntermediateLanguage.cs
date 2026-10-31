using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Themis.Repository.Tests;

/// <summary>
/// Reads a method body's IL instruction by instruction, by the operand each opcode takes
/// (ECMA-335 partition III), the opcodes taken from <see cref="OpCodes"/>.
/// </summary>
internal static class IntermediateLanguage
{
    private static readonly FrozenDictionary<int, OperandType> Operands = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToFrozenDictionary(code => (int)(ushort)code.Value, code => code.OperandType);

    /// <summary>The tokens of the instructions that name a method or a field: calls, loads, stores and <c>ldtoken</c>.</summary>
    internal static IReadOnlyList<EntityHandle> MemberTokens(BlobReader il)
    {
        List<EntityHandle> tokens = [];
        while (il.RemainingBytes > 0)
        {
            int code = il.ReadByte();
            if (code == 0xFE)
            {
                code = 0xFE00 | il.ReadByte();
            }

            switch (Operands[code])
            {
                case OperandType.InlineMethod or OperandType.InlineField or OperandType.InlineTok:
                    EntityHandle token = MetadataTokens.EntityHandle(il.ReadInt32());
                    if (token.Kind is not (HandleKind.TypeDefinition or HandleKind.TypeReference or HandleKind.TypeSpecification))
                    {
                        tokens.Add(token);
                    }

                    break;
                case OperandType.InlineSwitch:
                    int targets = il.ReadInt32();
                    il.Offset += 4 * targets;
                    break;
                case OperandType.InlineNone:
                    break;
                case OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar:
                    il.Offset += 1;
                    break;
                case OperandType.InlineVar:
                    il.Offset += 2;
                    break;
                case OperandType.InlineI8 or OperandType.InlineR:
                    il.Offset += 8;
                    break;
                default:
                    il.Offset += 4;
                    break;
            }
        }

        return tokens;
    }
}