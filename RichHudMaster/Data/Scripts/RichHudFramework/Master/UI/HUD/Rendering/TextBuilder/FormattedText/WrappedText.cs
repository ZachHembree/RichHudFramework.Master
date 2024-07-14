using System;
using System.Collections.Generic;
using System.Text;
using VRage;
using VRageMath;
using GlyphFormatMembers = VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>;

namespace RichHudFramework.UI.Rendering.Server
{
    using RichStringMembers = MyTuple<StringBuilder, GlyphFormatMembers>;

    public partial class TextBuilder
    {
        private class WrappedText : FormattedTextBase
        {
            private readonly List<Line> lineBuf;

            public WrappedText(LinePool lines) : base(lines, true)
            {
                lineBuf = new List<Line>();
                Rewrap();
            }

            /// <summary>
            /// Sets the maximum width for a line in the text before its wrapped to the next line and updates
            /// text wrapping.
            /// </summary>
            public void SetWrapWidth(float width)
            {
                if (Math.Abs(width - MaxLineWidth) > 1f)
                {
                    MaxLineWidth = width;
                    Rewrap();
                }
            }

            /// <summary>
            /// Inserts text at a given position in the document.
            /// </summary>
            /// <param name="start">X = line; Y = ch</param>
            public override void Insert(IList<RichStringMembers> text, Vector2I start)
            {
                start = ClampIndex(start);

                charBuffer.Clear();
                int insertStart = PrependPreceeding(start);

                for (int n = 0; n < text.Count; n++)
                    charBuffer.AppendRichString(text[n], AllowSpecialChars);

                if (lines.Count > 0)
                {
                    charBuffer.AddRange(lines[start.X], start.Y, lines[start.X].Count - start.Y);
                    lines.RemoveRange(insertStart, start.X - insertStart + 1);
                }

                GenerateLines();
                InsertLines(insertStart);
            }

            /// <summary>
            /// Inserts a string starting on a given line at a given position.
            /// </summary>
            public override void Insert(RichStringMembers text, Vector2I start) =>
                Insert(new RichStringMembers[] { text }, start);

            /// <summary>
            /// Applies glyph formatting to a range of characters.
            /// </summary>
            /// <param name="start">Position of the first character being formatted.</param>
            /// <param name="end">Position of the last character being formatted.</param>
            public override void SetFormatting(Vector2I start, Vector2I end, GlyphFormat formatting)
            {
                base.SetFormatting(start, end, formatting);
                RewrapRange(start.X, end.X);
            }

            /// <summary>
            /// Removes characters within a specified range.
            /// </summary>
            public override void RemoveRange(Vector2I start, Vector2I end)
            {
                base.RemoveRange(start, end);
                int index = start.X;

                while (index < lines.Count - 1 && TryPullToLine(index))
                    index++;
            }

            /// <summary>
            /// Regenerates text wrapping for the entire document.
            /// </summary>
            private void Rewrap()
            {
                int charCount = 0;

                for (int n = 0; n < Count; n++)
                    charCount += lines[n].Count;

                charBuffer.EnsureCapacity(charCount);
                charBuffer.Clear();

                for (int n = 0; n < Count; n++)
                    charBuffer.AddRange(lines[n]);

                lines.Clear();
                GenerateLines();
                lines.AddRange(lineBuf);

                for (int n = 0; n < lines.Count; n++)
                    lines.PooledLines[n].UpdateSize();
            }

            /// <summary>
            /// Regenerates text wrapping for the specified range of lines.
            /// </summary>
            private void RewrapRange(int start, int end)
            {
                int charCount = 0;

                for (int n = start; n <= end; n++)
                    charCount += lines[n].Count;

                charBuffer.EnsureCapacity(charCount);
                charBuffer.Clear();

                int insertStart = PrependPreceeding(new Vector2I(start, 0));

                for (int n = start; n <= end; n++)
                    charBuffer.AddRange(lines[n]);

                lines.RemoveRange(insertStart, end - insertStart + 1);

                GenerateLines();
                InsertLines(insertStart);
            }

            /// <summary>
            /// Finds the start of the word associated with the character at the given index then
            /// prepends everything between the start of that line and the given index to the character
            /// buffer.
            /// </summary>
            private int PrependPreceeding(Vector2I splitStart)
            {
                Vector2I splitEnd;

                if (lines.TryGetLastIndex(splitStart, out splitEnd))
                {
                    // Retrieve the index of the first character in the word
                    Vector2I wordStart = GetWordStart(splitEnd);
                    wordStart.Y = 0; // Grab the whole line

                    for (int i = wordStart.X; i < splitEnd.X; i++)
                        charBuffer.AddRange(lines.PooledLines[i]);

                    charBuffer.AddRange(lines.PooledLines[splitStart.X], 0, splitStart.Y);
                    return wordStart.X;
                }
                else
                    return splitStart.X;
            }

            /// <summary>
            /// Generates a new list of wrapped <see cref="Line"/>s from the contents of the character buffer.
            /// </summary>
            private void GenerateLines()
            {
                lineBuf.TrimExcess();
                lineBuf.Clear();
                charBuffer.UpdateFormat();

                float wordWidth, spaceRemaining = MaxLineWidth;
                int wordEnd, lineStart = 0, lineEnd = -1;

                for (int wordStart = 0; TryGetWordEnd(wordStart, out wordEnd, out wordWidth); wordStart = wordEnd + 1)
                {
                    bool isWrapping =
                        (spaceRemaining < wordWidth && wordWidth <= MaxLineWidth)
                        || charBuffer.Chars[wordStart] == '\n';

                    for (int n = wordStart; n <= wordEnd; n++)
                    {
                        // Start new line beginning with the nth character, usually wordStart
                        if (spaceRemaining < charBuffer.FormattedGlyphs[n].chSize.X || isWrapping)
                        {
                            // Start new line
                            if (lineEnd != -1)
                            {
                                Line lastLine = lines.GetNewLine();
                                lastLine.AddRange(charBuffer, lineStart, lineEnd - lineStart + 1);
                                lineBuf.Add(lastLine);
                            }

                            lineStart = n;
                            spaceRemaining = MaxLineWidth;
                            isWrapping = false;
                        }

                        lineEnd = n;
                        spaceRemaining -= charBuffer.FormattedGlyphs[n].chSize.X;
                    }
                }

                if (lineEnd != -1)
                {
                    Line nextLine = lines.GetNewLine();
                    nextLine.AddRange(charBuffer, lineStart, lineEnd - lineStart + 1);
                    lineBuf.Add(nextLine);
                }
            }

            /// <summary>
            /// Inserts a list of lines at the specified starting index and updates the wrapping of the lines following
            /// as needed.
            /// </summary>
            private void InsertLines(int index)
            {
                for (int n = 0; n < lineBuf.Count; n++)
                    lineBuf[n].UpdateSize();

                lines.InsertRange(index, lineBuf);

                // Pull text from the lines following the insert to maintain proper text wrapping
                index += Math.Max(lineBuf.Count - 1, 0);

                if (index > 0)
                    TryPullToLine(index - 1);

                while (index < lines.Count - 1 && TryPullToLine(index))
                    index++;
            }

            /// <summary>
            /// Attempts to pull text from the lines following to the one specified while maintaining proper word wrapping.
            /// </summary>
            private bool TryPullToLine(int line)
            {
                float spaceRemaining = MaxLineWidth - lines[line].UnscaledSize.X;
                Vector2I wordStart = new Vector2I(line + 1, 0),
                    wordEnd, end = Vector2I.Zero;

                while (lines[wordStart.X].Chars[wordStart.Y] != '\n' && TryGetWordEnd(wordStart, out wordEnd, ref spaceRemaining))
                {
                    end = wordEnd;

                    if (wordStart.X == wordEnd.X) // Add one word
                    {
                        lines.PooledLines[line].AddRange(lines.PooledLines[wordStart.X], wordStart.Y, wordEnd.Y - wordStart.Y + 1);
                    }
                    else // Compact multi-line word
                    {
                        int startCount = lines.PooledLines[wordStart.X].Count - wordStart.Y;
                        lines.PooledLines[line].AddRange(lines.PooledLines[wordStart.X], wordStart.Y, startCount);

                        for (int i = wordStart.X + 1; i < wordEnd.X; i++)
                            lines.PooledLines[line].AddRange(lines.PooledLines[i]);

                        lines.PooledLines[line].AddRange(lines.PooledLines[wordEnd.X], 0, wordEnd.Y + 1);
                    }

                    if (!lines.TryGetNextIndex(wordEnd, out wordStart))
                        break;
                }

                if (end.X > line)
                {
                    if (end.Y < lines[end.X].Count - 1)
                    {
                        // Remove pulled chars on ending line
                        lines.PooledLines[end.X].RemoveRange(0, end.Y + 1);
                        lines.PooledLines[end.X].UpdateSize();
                        // Remove intervening emptied lines
                        lines.RemoveRange(line + 1, end.X - line - 1);
                    }
                    else
                    {
                        // Remove intervening empty lines
                        lines.RemoveRange(line + 1, end.X - line);
                    }

                    lines.PooledLines[line].UpdateSize();
                    return true;
                }
                else
                {
                    return false;
                }
            }

            /// <summary>
            /// Gets the position of the beginning of a word given the index of one of its characters.
            /// </summary>
            private Vector2I GetWordStart(Vector2I end)
            {
                Vector2I start;

                while (lines.TryGetLastIndex(end, out start))
                {
                    char left = lines.PooledLines[start.X].Chars[start.Y],
                        right = lines.PooledLines[end.X].Chars[end.Y];
                    bool isWordBreak =
                        (left == ' ' || left == '-' || left == '_') &&
                        !(right == ' ' || right == '-' || right == '_');

                    if (left == '\n' || isWordBreak)
                    {
                        return end;
                    }
                    else
                    {
                        end = start;
                    }
                }

                return end;
            }

            /// <summary>
            /// Attempts to retrieve the position of the end of a word without exceeding the given space remaining.
            /// </summary>
            /// <param name="start">Where the search begins, not necessarily the beginning of the word.</param>
            /// <param name="end">Somewhere to the right of or equal to the start.</param>
            private bool TryGetWordEnd(Vector2I start, out Vector2I end, ref float spaceRemaining)
            {
                spaceRemaining -= lines.PooledLines[start.X].FormattedGlyphs[start.Y].chSize.X;

                while (lines.TryGetNextIndex(start, out end) && spaceRemaining > 0f)
                {
                    char left = lines.PooledLines[start.X].Chars[start.Y],
                        right = lines.PooledLines[end.X].Chars[end.Y];
                    bool isWordBreak =
                        (left == ' ' || left == '-' || left == '_') &&
                        !(right == ' ' || right == '-' || right == '_');

                    if (right == '\n' || isWordBreak)
                    {
                        end = start;
                        break;
                    }
                    else
                    {
                        spaceRemaining -= lines.PooledLines[end.X].FormattedGlyphs[end.Y].chSize.X;
                        start = end;
                    }
                }

                return spaceRemaining > 0f;
            }

            /// <summary>
            /// Tries to find the end of a word in the character buffer starting at the given index.
            /// </summary>
            private bool TryGetWordEnd(int start, out int wordEnd, out float width)
            {
                wordEnd = -1;
                width = 0f;

                for (int n = start; n < charBuffer.Count; n++)
                {
                    width += charBuffer.FormattedGlyphs[n].chSize.X;

                    if (n == (charBuffer.Count - 1) || charBuffer.Chars[n + 1] == '\n')
                    {
                        wordEnd = n;
                        return true;
                    }
                    else
                    {
                        char left = charBuffer.Chars[n],
                            right = charBuffer.Chars[n + 1];
                        bool isWordBreak =
                            (left == ' ' || left == '-' || left == '_') &&
                            !(right == ' ' || right == '-' || right == '_');

                        if (isWordBreak)
                        {
                            wordEnd = n;
                            return true;
                        }
                    }
                }

                return false;
            }
        }
    }
}