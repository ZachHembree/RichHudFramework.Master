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
        private abstract class FormattedTextBase : IIndexedCollection<Line>
        {
            public Line this[int index] => lines.PooledLines[index];

            public virtual int Count => lines.Count;

            public virtual float MaxLineWidth { get; protected set; }

            public bool AllowSpecialChars { get; }

            protected readonly LinePool lines;
            protected readonly Line charBuffer;

            protected FormattedTextBase(LinePool lines, bool allowSpecialChars)
            {
                this.lines = lines;
                AllowSpecialChars = allowSpecialChars;
                MaxLineWidth = 0f;
                charBuffer = lines.GetNewLine();
            }

            /// <summary>
            /// Appends text to the end of the document.
            /// </summary>
            public virtual void Append(IList<RichStringMembers> text)
            {
                Insert(text, GetAppendStartIndex());
            }

            /// <summary>
            /// Appends text to the end of the document.
            /// </summary>
            public virtual void Append(RichStringMembers text)
            {
                Insert(text, GetAppendStartIndex());
            }

            protected Vector2I GetAppendStartIndex()
            {
                Vector2I start = new Vector2I(Math.Max(0, lines.Count - 1), 0);

                if (lines.Count > 0)
                    start.Y = Math.Max(0, lines.PooledLines[start.X].Count);

                return start;
            }

            /// <summary>
            /// Clears all existing text.
            /// </summary>
            public virtual void Clear() =>
                lines.Clear();

            /// <summary>
            /// Inserts a string starting on a given line at a given position.
            /// </summary>
            /// <param name="start">X = line; Y = ch</param>
            public abstract void Insert(RichStringMembers text, Vector2I start);

            /// <summary>
            /// Inserts text at a given position in the document.
            /// </summary>
            /// <param name="start">X = line; Y = ch</param>
            public abstract void Insert(IList<RichStringMembers> text, Vector2I start);

            /// <summary>
            /// Applies glyph formatting to a range of characters.
            /// </summary>
            /// <param name="start">Position of the first character being formatted.</param>
            /// <param name="end">Position of the last character being formatted.</param>
            public virtual void SetFormatting(Vector2I start, Vector2I end, GlyphFormat formatting)
            {
                if (lines.Count > 0)
                {
                    if (end.X > start.X)
                    {
                        lines.PooledLines[start.X].SetFormatting(start.Y, lines.PooledLines[start.X].Count - 1, formatting);

                        for (int x = start.X + 1; x < end.X; x++)
                            lines.PooledLines[x].SetFormatting(formatting);

                        lines.PooledLines[end.X].SetFormatting(0, end.Y, formatting);
                    }
                    else
                    {
                        lines.PooledLines[start.X].SetFormatting(start.Y, end.Y, formatting);
                    }

                    for (int n = start.X; n <= end.X; n++)
                        lines.PooledLines[n].UpdateSize();
                }
            }

            /// <summary>
            /// Removes characters within a specified range.
            /// </summary>
            /// <param name="start">X = line; Y = ch</param>
            public virtual void RemoveRange(Vector2I start, Vector2I end)
            {
                if (start.X < lines.Count && lines.PooledLines[start.X].Count > 0)
                {
                    if (end.X > start.X) // Multi-line removal
                    {
                        int prefixCount = end.Y + 1;

                        if (prefixCount > lines.PooledLines[end.X].Count)
                            prefixCount = lines.PooledLines[end.X].Count;

                        lines.PooledLines[end.X].RemoveRange(0, prefixCount);
                        lines.PooledLines[end.X].UpdateSize();

                        // Start Line
                        int suffixCount = lines.PooledLines[start.X].Count - start.Y;

                        if (suffixCount > 0)
                        {
                            lines.PooledLines[start.X].RemoveRange(start.Y, suffixCount);
                            lines.PooledLines[start.X].UpdateSize();
                        }

                        // 3. Remove middle
                        int linesToRemove = end.X - start.X - 1;

                        if (linesToRemove > 0)
                            lines.RemoveRange(start.X + 1, linesToRemove);
                    }
                    else // One line removal
                    {
                        if (start.X > 0 && start.Y == 0 && end.Y == (lines.PooledLines[start.X].Count - 1))
                            lines.RemoveAt(start.X);
                        else
                        {
                            lines.PooledLines[start.X].RemoveRange(start.Y, end.Y - start.Y + 1);
                            lines.PooledLines[start.X].UpdateSize();
                        }
                    }
                }
            }

            /// <summary>
            /// Clamps the given index within the range of valid indices. Char indicies == char count are used to
            /// indicated appends.
            /// </summary>
            protected Vector2I ClampIndex(Vector2I index)
            {
                index.X = MathHelper.Clamp(index.X, 0, lines.PooledLines.Count - 1);
                index.Y = MathHelper.Clamp(index.Y, 0, (index.X < lines.PooledLines.Count) ? lines.PooledLines[index.X].Count : 0);

                return index;
            }
        }
    }
}