using System;
using System.Buffers;
using System.Text;

namespace Themis;

/// <summary>
/// Names, references and text as <c>00</c> section 1.5 gives them. A length counts Unicode
/// scalar values, and a string holding a surrogate that is not part of a pair is not text.
/// </summary>
internal static class TextRules
{
    /// <summary>The most characters a name holds.</summary>
    internal const int NameLength = 64;

    /// <summary>The most characters a reference holds.</summary>
    internal const int ReferenceLength = 128;

    /// <summary>
    /// Whether <paramref name="text"/> is a name: 1 to 64 lower-case ASCII letters and digits
    /// separated by single <c>.</c>, <c>-</c> or <c>_</c>, starting with a letter.
    /// </summary>
    internal static bool IsName(string? text)
    {
        if (text is null || text.Length is 0 or > NameLength || !char.IsAsciiLetterLower(text[0]))
        {
            return false;
        }

        bool separated = false;
        foreach (char character in text)
        {
            if (character is '.' or '-' or '_')
            {
                if (separated)
                {
                    return false;
                }

                separated = true;
            }
            else if (char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character))
            {
                separated = false;
            }
            else
            {
                return false;
            }
        }

        return !separated;
    }

    /// <summary>Whether <paramref name="text"/> is a reference: text of 1 to 128 characters, none of them a control character.</summary>
    internal static bool IsReference(string? text)
    {
        if (text is null || Length(text) is not (>= 1 and <= ReferenceLength))
        {
            return false;
        }

        foreach (Rune rune in text.EnumerateRunes())
        {
            if (Rune.IsControl(rune))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Whether <paramref name="text"/> is text: it holds no surrogate that is not part of a pair.</summary>
    internal static bool IsText(string text)
    {
        return Length(text) is not null;
    }

    /// <summary>The number of Unicode scalar values <paramref name="text"/> holds, or none where it is not text.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    internal static int? Length(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        int length = 0;
        ReadOnlySpan<char> rest = text;
        while (!rest.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(rest, out _, out int used) != OperationStatus.Done)
            {
                return null;
            }

            rest = rest[used..];
            length++;
        }

        return length;
    }
}