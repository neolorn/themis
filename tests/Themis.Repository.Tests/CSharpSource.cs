using System;
using System.Text;

namespace Themis.Repository.Tests;

/// <summary>
/// A C# source file's code: comments and the contents of string and character literals are
/// blanked in <see cref="Code"/>, line breaks kept, so a declaration a comment or a literal
/// quotes is no declaration.
/// </summary>
/// <remarks>
/// It reads the literal forms C# has: regular, verbatim (<c>@"..."</c>), interpolated
/// (<c>$"..."</c>, <c>$@"..."</c>, <c>@$"..."</c>) and raw (<c>"""..."""</c>, any number of
/// quotes and dollars), an interpolated literal's holes taken as part of its contents.
/// </remarks>
internal sealed class CSharpSource
{
    private CSharpSource(string code)
    {
        Code = code;
    }

    internal string Code { get; }

    internal static CSharpSource Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        StringBuilder code = new(text.Length);
        int index = 0;
        while (index < text.Length)
        {
            char current = text[index];
            char next = index + 1 < text.Length ? text[index + 1] : '\0';
            if (current == '/' && next == '/')
            {
                int end = text.IndexOf('\n', index);
                end = end < 0 ? text.Length : end;
                Blank(code, text, index, end);
                index = end;
            }
            else if (current == '/' && next == '*')
            {
                int end = text.IndexOf("*/", index + 2, StringComparison.Ordinal);
                end = end < 0 ? text.Length : end + 2;
                Blank(code, text, index, end);
                index = end;
            }
            else if (current == '\'')
            {
                int end = index + 1;
                while (end < text.Length && text[end] != '\'' && text[end] != '\n')
                {
                    end += text[end] == '\\' ? 2 : 1;
                }

                end = Math.Min(end + 1, text.Length);
                Blank(code, text, index, end);
                index = end;
            }
            else if (StringStart(text, index) is (int prefix, bool verbatim))
            {
                int start = index + prefix;
                int end = verbatim ? VerbatimEnd(text, start) : RawOrRegularEnd(text, start);
                Blank(code, text, index, end);
                index = end;
            }
            else
            {
                code.Append(current);
                index++;
            }
        }

        return new CSharpSource(code.ToString());
    }

    /// <summary>The length of a string literal's prefix (<c>$</c>, <c>@</c>) up to its first quote, and whether it is verbatim, or none where no literal starts.</summary>
    private static (int Prefix, bool Verbatim)? StringStart(string text, int index)
    {
        int at = index;
        bool verbatim = false;
        while (at < text.Length && (text[at] == '$' || text[at] == '@'))
        {
            verbatim |= text[at] == '@';
            at++;
        }

        if (at >= text.Length || text[at] != '"')
        {
            return null;
        }

        // An identifier or number running into the prefix is no literal's start.
        if (at > index && index > 0 && (char.IsLetterOrDigit(text[index - 1]) || text[index - 1] == '_'))
        {
            return null;
        }

        return (at - index, verbatim);
    }

    /// <summary>Where a regular or raw literal opening at <paramref name="quote"/> ends, after its closing quotes.</summary>
    private static int RawOrRegularEnd(string text, int quote)
    {
        int quotes = Quotes(text, quote);
        if (quotes >= 3)
        {
            string closing = new('"', quotes);
            int close = text.IndexOf(closing, quote + quotes, StringComparison.Ordinal);
            return close < 0 ? text.Length : close + quotes;
        }

        int at = quote + 1;
        while (at < text.Length && text[at] != '"' && text[at] != '\n')
        {
            at += text[at] == '\\' ? 2 : 1;
        }

        return Math.Min(at + 1, text.Length);
    }

    /// <summary>Where a verbatim literal opening at <paramref name="quote"/> ends; a doubled quote is one quote of its contents.</summary>
    private static int VerbatimEnd(string text, int quote)
    {
        int at = quote + 1;
        while (at < text.Length)
        {
            if (text[at] == '"')
            {
                if (at + 1 < text.Length && text[at + 1] == '"')
                {
                    at += 2;
                    continue;
                }

                return at + 1;
            }

            at++;
        }

        return text.Length;
    }

    private static int Quotes(string text, int at)
    {
        int count = 0;
        while (at + count < text.Length && text[at + count] == '"')
        {
            count++;
        }

        // Two quotes alone are an empty regular literal, not a raw one.
        return count == 2 ? 1 : count;
    }

    private static void Blank(StringBuilder code, string text, int start, int end)
    {
        for (int at = start; at < end; at++)
        {
            code.Append(text[at] is '\n' or '\r' ? text[at] : ' ');
        }
    }
}