using System;
using System.Collections.Generic;
using VRageMath;
using VRage;
using System.Text;
using GlyphFormatMembers = VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>;
using System.Collections;

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
                if (width < MaxLineWidth - 2f || width > MaxLineWidth + 4f)
                {
                    // Not ideal; I'm regenerating the whole collection just to update the wrapping.
                    // Here's to hoping I can get away with it!
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
                int insertStart = GetInsertStart(start);

                for (int n = 0; n < text.Count; n++)
                    charBuffer.AppendRichString(text[n], AllowSpecialChars);

                InsertChars(insertStart, start);
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

                if (start.X < lines.Count - 1)
                    TryPullToLine(start.X);
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

                int insertStart = GetInsertStart(new Vector2I(start, 0));

                for (int n = start; n <= end; n++)
                    charBuffer.AddRange(lines[n]);

                lines.RemoveRange(insertStart, end - insertStart + 1);

                GenerateLines();
                InsertLines(insertStart);
            }

            /// <summary>
            /// Retrieves the index of the line where the word immediately preceeding the location of the
            /// insert begins and adds the intervening text to the character buffer.
            /// </summary>
            private int GetInsertStart(Vector2I splitStart)
            {
                Vector2I splitEnd;

                if (lines.TryGetLastIndex(splitStart, out splitEnd))
                {
                    // Retrieve the index of the first character in the word just before
                    // the split.
                    splitStart = GetWordStart(splitEnd); 
                    splitStart.Y = 0; // Ensure the entire line is added

                    Vector2I i = splitStart;

                    do
                    {
                        charBuffer.AddCharFromLine(i.Y, lines[i.X]);
                    }
                    while (lines.TryGetNextIndex(i, out i) && (i.X < splitEnd.X || (i.X == splitEnd.X && i.Y <= splitEnd.Y)));
                }

                return splitStart.X;
            }

            /// <summary>
            /// Inserts the contents of the charBuffer starting at the given index and updates wrapping of
            /// the surrounding text.
            /// </summary>
            private void InsertChars(int startLine, Vector2I splitStart)
            {
                if (lines.Count > 0)
                {
                    for (int y = splitStart.Y; y < lines[splitStart.X].Count; y++)
                        charBuffer.AddCharFromLine(y, lines[splitStart.X]);

                    lines.RemoveRange(startLine, splitStart.X - startLine + 1);
                }

                GenerateLines();
                InsertLines(startLine);
            }

            /// <summary>
            /// Generates a new list of wrapped <see cref="Line"/>s from the contents of the character buffer.
            /// </summary>
            private void GenerateLines()
            {
                lineBuf.TrimExcess();
                lineBuf.Clear();

                Line currentLine = null;
                float wordWidth, spaceRemaining = -1f;
                int wordEnd;

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
                            spaceRemaining = MaxLineWidth;
                            currentLine = lines.GetNewLine();
                            lineBuf.Add(currentLine);

                            isWrapping = false;
                        }

                        currentLine.AddCharFromLine(n, charBuffer);
                        spaceRemaining -= charBuffer.FormattedGlyphs[n].chSize.X;
                    }
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
                charBuffer.Clear();

                // Pull text from the lines following the insert to maintain proper text wrapping
                index += lineBuf.Count - 1;

                while (index < lines.Count - 1 && TryPullToLine(index))
                    index++;               
            }

            /// <summary>
            /// Attempts to pull text from the lines following to the one specified while maintaining proper word wrapping.
            /// </summary>
            private bool TryPullToLine(int line)
            {
                float spaceRemaining = MaxLineWidth - lines[line].UnscaledSize.X;
                Vector2I i = new Vector2I(line + 1, 0), wordEnd, end = new Vector2I();

                while (TryGetWordEnd(i, out wordEnd, ref spaceRemaining) && lines[i.X].Chars[i.Y] != '\n')
                {
                    end = wordEnd;

                    do
                    {
                        lines.PooledLines[line].AddCharFromLine(i.Y, lines[i.X]);
                    }
                    while (lines.TryGetNextIndex(i, out i) && (i.X < wordEnd.X || (i.X == wordEnd.X && i.Y <= wordEnd.Y)));
                }

                if (end.X > line)
                {
                    if (end.Y < lines[end.X].Count - 1)
                    {
                        lines.PooledLines[end.X].RemoveRange(0, end.Y + 1);
                        lines.PooledLines[end.X].UpdateSize();
                        lines.RemoveRange(line + 1, end.X - line - 1);
                    }
                    else
                        lines.RemoveRange(line + 1, end.X - line);

                    lines.PooledLines[line].UpdateSize();
                    return true;
                }
                else
                    return false;
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

                    if (!(lines[end.X].Chars[end.Y] == '\n' || isWordBreak))
                    {
                        end = start;
                    }
                    else
                        break;
                }

                return start;
            }

            /// <summary>
            /// Attempts to retrieve the position of the end of a word without exceeding the given space remaining.
            /// </summary>
            /// <param name="start">Where the search begins, not necessarily the beginning of the word.</param>
            /// <param name="end">Somewhere to the right of or equal to the start.</param>
            private bool TryGetWordEnd(Vector2I start, out Vector2I end, ref float spaceRemaining)
            {
                spaceRemaining -= lines[start.X].FormattedGlyphs[start.Y].chSize.X;

                while (lines.TryGetNextIndex(start, out end) && spaceRemaining > 0f)
                {
                    char left = lines.PooledLines[start.X].Chars[start.Y],
                        right = lines.PooledLines[end.X].Chars[end.Y];
                    bool isWordBreak =
                        (left == ' ' || left == '-' || left == '_') &&
                        !(right == ' ' || right == '-' || right == '_');

                    if (!(lines[end.X].Chars[end.Y] == '\n' || isWordBreak))
                    {
                        spaceRemaining -= lines[end.X].FormattedGlyphs[end.Y].chSize.X;
                        start = end;
                    }
                    else
                        break;
                }

                end = start;
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

                        width += charBuffer.FormattedGlyphs[n].chSize.X;

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