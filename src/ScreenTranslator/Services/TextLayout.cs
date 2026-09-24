using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using ScreenTranslator.Models;

namespace ScreenTranslator.Services;

/// <summary>Turns OCR lines back into paragraphs so the translator gets whole sentences, not wrapped fragments.</summary>
public static partial class TextLayout
{
    /// <summary>Flatten lines to text: paragraphs separated by newlines, wrapped lines joined with spaces.</summary>
    public static string Compose(IReadOnlyList<OcrLine> lines)
        => string.Join("\n", GroupBlocks(lines).Select(b => b.Text));

    /// <summary>Group consecutive lines into paragraph blocks with their on-screen rectangles.</summary>
    public static List<OcrBlock> GroupBlocks(IReadOnlyList<OcrLine> rawLines)
    {
        var blocks = new List<OcrBlock>();
        if (rawLines.Count == 0) return blocks;

        var lines = MergeSplitLines(rawLines);
        double avgHeight = lines.Average(l => l.Rect.Height);

        var text = new StringBuilder();
        Rectangle rect = Rectangle.Empty;
        int count = 0;
        OcrLine? prev = null;

        void Flush()
        {
            if (count == 0) return;
            blocks.Add(new OcrBlock(text.ToString(), rect, count, rect.Height / (double)count));
            text.Clear();
            count = 0;
        }

        foreach (var line in lines)
        {
            bool startNew = prev is null || !Continues(prev, line, avgHeight);

            if (startNew)
            {
                Flush();
                text.Append(line.Text);
                rect = line.Rect;
                count = 1;
            }
            else
            {
                if (text.Length > 0 && text[text.Length - 1] == '-')
                    text.Length--;               // hyphenated line break
                else
                    text.Append(' ');
                text.Append(line.Text);
                rect = Rectangle.Union(rect, line.Rect);
                count++;
            }
            prev = line;
        }
        Flush();
        return blocks;
    }

    /// <summary>
    /// Windows OCR sometimes splits one visual line into several (around inline code chips, links, bold runs).
    /// Stitch pieces back together when they share a baseline and sit close horizontally.
    /// </summary>
    private static List<OcrLine> MergeSplitLines(IReadOnlyList<OcrLine> lines)
    {
        var sorted = lines.OrderBy(l => l.Rect.Top).ThenBy(l => l.Rect.Left).ToList();
        var result = new List<OcrLine>();

        foreach (var line in sorted)
        {
            int idx = result.FindIndex(r => SameRow(r, line));
            if (idx < 0)
            {
                result.Add(line);
                continue;
            }

            var row = result[idx];
            var (left, right) = row.Rect.Left <= line.Rect.Left ? (row, line) : (line, row);
            result[idx] = new OcrLine(left.Text + " " + right.Text, Rectangle.Union(row.Rect, line.Rect));
        }

        return result.OrderBy(l => l.Rect.Top).ThenBy(l => l.Rect.Left).ToList();

        static bool SameRow(OcrLine a, OcrLine b)
        {
            int overlap = Math.Min(a.Rect.Bottom, b.Rect.Bottom) - Math.Max(a.Rect.Top, b.Rect.Top);
            int minH = Math.Min(a.Rect.Height, b.Rect.Height);
            if (overlap < minH * 0.6) return false;

            int gap = Math.Max(a.Rect.Left, b.Rect.Left) - Math.Min(a.Rect.Right, b.Rect.Right);
            return gap < minH * 2.0;
        }
    }

    /// <summary>Does <paramref name="line"/> look like the next wrapped line of the paragraph that ends with <paramref name="prev"/>?</summary>
    private static bool Continues(OcrLine prev, OcrLine line, double avgHeight)
    {
        int gap = line.Rect.Top - prev.Rect.Bottom;
        if (gap > avgHeight * 0.75) return false;                 // blank line between paragraphs
        if (line.Rect.Top < prev.Rect.Top) return false;           // jumped up: new column / unrelated element

        // Must overlap horizontally (same column).
        int overlap = Math.Min(prev.Rect.Right, line.Rect.Right) - Math.Max(prev.Rect.Left, line.Rect.Left);
        if (overlap < Math.Min(prev.Rect.Width, line.Rect.Width) * 0.3) return false;

        // Wildly different text height = different element (heading vs body).
        double ratio = (double)line.Rect.Height / Math.Max(1, prev.Rect.Height);
        if (ratio > 1.6 || ratio < 0.6) return false;

        if (LooksLikeListItem(line.Text)) return false;
        return true;
    }

    private static bool LooksLikeListItem(string text)
    {
        if (text.Length < 2) return false;
        char c = text[0];
        if (c == '•' || c == '-' || c == '*' || c == '·' || c == '▪' || c == '●') return true;
        return char.IsDigit(c) && text.Length > 2 && (text[1] == '.' || text[1] == ')');
    }

    /// <summary>Is there anything here worth sending to a translator? Filters numbers, symbols, and OCR garbage.</summary>
    public static bool IsTranslatable(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        int letters = 0, total = 0, arabic = 0, latin = 0;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch)) continue;
            total++;
            if (!char.IsLetter(ch)) continue;
            letters++;
            if (ch is >= '؀' and <= 'ۿ' or >= 'ﭐ' and <= '﻿') arabic++;
            else if (ch < 'ɐ') latin++;
        }
        if (letters < 2) return false;                 // "42", "->", "%"
        if (letters < total * 0.4) return false;       // mostly symbols/digits: probably garbage
        if (arabic > letters / 2) return false;        // already Persian/Arabic

        // English OCR reading Persian produces strings of accented Latin junk; real Latin words have vowels.
        if (latin == letters)
            return LatinWord().IsMatch(text);

        return true;                                   // Cyrillic, CJK, etc. — let the translator decide
    }

    [GeneratedRegex(@"\b[A-Za-z]*[aeiouyAEIOUY][A-Za-z]*\b")]
    private static partial Regex LatinWord();
}
