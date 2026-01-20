#!/usr/bin/env dotnet
#:property TargetFramework=net10.0
#:property PublishAot=true
#:property PublishTrimmed=true
#:property OptimizationPreference=speed
#:package ConsoleAppFramework@5.7.13

using System.Globalization;
using System.Text;
using ConsoleAppFramework;

// Force UTF-8 for redirected stdin/stdout (pipes/files)
Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

var app = ConsoleApp.Create();
app.Add<Commands>();
app.Run(args);

public sealed class Commands
{
    /// <summary>
    /// Normalize Unicode text into lossy ASCII using:
    /// 1) curated substitutions (smart quotes, arrows, dashes, etc.)
    /// 2) NFKD normalization
    /// 3) stripping combining marks
    /// 4) dropping non-ASCII code points
    /// </summary>
    /// <param name="text">
    /// Input text. If omitted, reads all stdin (handy for pipes).
    /// </param>
    /// <param name="collapseWhitespace">Collapse runs of whitespace to single spaces.</param>
    /// <param name="trim">Trim the final output.</param>
    [Command("")]
    public void Normalize(
        [Argument] string? text = null,
        bool collapseWhitespace = false,
        bool trim = false)
    {
        if (string.IsNullOrEmpty(text))
        {
            text = Console.In.ReadToEnd();
            if (string.IsNullOrEmpty(text))
                return;
        }
        var result = AsciiLossyNormalizer.ToAsciiLossy(text, collapseWhitespace, trim);
        Console.Write(result);
    }
}

public static class AsciiLossyNormalizer
{
    private static readonly Dictionary<Rune, string> Subs = new()
    {
        // Apostrophes / single quotes
        [new Rune(0x2018)] = "'", // ‘
        [new Rune(0x2019)] = "'", // ’
        [new Rune(0x201A)] = "'", // ‚
        [new Rune(0x201B)] = "'", // ‛
        [new Rune(0x2032)] = "'", // ′
        [new Rune(0x2035)] = "'", // ‵
        [new Rune(0x00B4)] = "'", // ´

        // Double quotes
        [new Rune(0x201C)] = "\"", // “
        [new Rune(0x201D)] = "\"", // ”
        [new Rune(0x201E)] = "\"", // „
        [new Rune(0x201F)] = "\"", // ‟
        [new Rune(0x2033)] = "\"", // ″
        [new Rune(0x2036)] = "\"", // ‶

        // Dashes / hyphens / minus
        [new Rune(0x2010)] = "-",  // ‐
        [new Rune(0x2011)] = "-",  // -
        [new Rune(0x2012)] = "-",  // ‒
        [new Rune(0x2013)] = "-",  // –
        [new Rune(0x2014)] = "-",  // —
        [new Rune(0x2015)] = "-",  // ―
        [new Rune(0x2212)] = "-",  // −

        // Ellipsis
        [new Rune(0x2026)] = "...", // …

        // Arrows
        [new Rune(0x2192)] = "->",   // →
        [new Rune(0x2190)] = "<-",   // ←
        [new Rune(0x2194)] = "<->",  // ↔
        [new Rune(0x21D2)] = "=>",   // ⇒
        [new Rune(0x21D0)] = "<=",   // ⇐
        [new Rune(0x21D4)] = "<=>",  // ⇔

        // Bullets / middle dots
        [new Rune(0x2022)] = "*", // •
        [new Rune(0x00B7)] = "*", // ·
        [new Rune(0x2219)] = "*", // ∙

        // Common spaces -> normal space
        [new Rune(0x00A0)] = " ", // NBSP
        [new Rune(0x202F)] = " ", // NNBSP
        [new Rune(0x3000)] = " ", // ideographic space
        [new Rune(0x2000)] = " ", [new Rune(0x2001)] = " ", [new Rune(0x2002)] = " ",
        [new Rune(0x2003)] = " ", [new Rune(0x2004)] = " ", [new Rune(0x2005)] = " ",
        [new Rune(0x2006)] = " ", [new Rune(0x2007)] = " ", [new Rune(0x2008)] = " ",
        [new Rune(0x2009)] = " ", [new Rune(0x200A)] = " ", [new Rune(0x205F)] = " ",

        // Misc math-ish
        [new Rune(0x00D7)] = "x", // ×
        [new Rune(0x00F7)] = "/", // ÷
    };

    public static string ToAsciiLossy(string input, bool collapseWhitespace, bool trim)
    {
        if (input.Length == 0) return input;

        var substituted = SubstituteByRunes(input);
        var normalized = substituted.Normalize(NormalizationForm.FormKD);
        var stripped = StripMarksAndControls(normalized);
        var ascii = KeepAsciiOnly(stripped);

        if (collapseWhitespace)
            ascii = CollapseWhitespace(ascii);

        if (trim)
            ascii = ascii.Trim();

        return ascii;
    }

    private static string SubstituteByRunes(string s)
    {
        var sb = new StringBuilder(s.Length);

        for (int i = 0; i < s.Length;)
        {
            if (!Rune.TryGetRuneAt(s, i, out var rune))
            {
                sb.Append(s[i]);
                i++;
                continue;
            }

            if (Subs.TryGetValue(rune, out var replacement))
                sb.Append(replacement);
            else
                sb.Append(rune.ToString());

            i += rune.Utf16SequenceLength;
        }

        return sb.ToString();
    }

    private static string StripMarksAndControls(string s)
    {
        var sb = new StringBuilder(s.Length);

        for (int i = 0; i < s.Length;)
        {
            if (!Rune.TryGetRuneAt(s, i, out var rune))
            {
                i++;
                continue;
            }

            var cat = Rune.GetUnicodeCategory(rune);

            // Remove diacritic/combining marks
            if (cat is UnicodeCategory.NonSpacingMark
                or UnicodeCategory.SpacingCombiningMark
                or UnicodeCategory.EnclosingMark)
            {
                i += rune.Utf16SequenceLength;
                continue;
            }

            // Remove most control chars; keep \t \n \r
            if (cat == UnicodeCategory.Control)
            {
                if (rune.Value is 0x09 or 0x0A or 0x0D)
                    sb.Append(rune.ToString());

                i += rune.Utf16SequenceLength;
                continue;
            }

            sb.Append(rune.ToString());
            i += rune.Utf16SequenceLength;
        }

        return sb.ToString();
    }

    private static string KeepAsciiOnly(string s)
    {
        var sb = new StringBuilder(s.Length);

        for (int i = 0; i < s.Length;)
        {
            Rune.TryGetRuneAt(s, i, out var r);

            if (r.Value <= 0x7F)
                sb.Append((char)r.Value);

            i += r.Utf16SequenceLength;
        }

        return sb.ToString();
    }

    private static string CollapseWhitespace(string s)
    {
        var sb = new StringBuilder(s.Length);
        bool inWs = false;

        foreach (char c in s)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!inWs) sb.Append(' ');
                inWs = true;
            }
            else
            {
                sb.Append(c);
                inWs = false;
            }
        }

        return sb.ToString();
    }
}
