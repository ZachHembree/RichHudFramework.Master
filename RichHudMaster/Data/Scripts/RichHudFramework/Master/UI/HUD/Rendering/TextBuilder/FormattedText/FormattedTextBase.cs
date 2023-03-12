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
            public Line this[int index] => lines[index];

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
                    start.Y = Math.Max(0, lines[start.X].Count);

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
            public virtual void SetFormatting(Vector2I start, Vector2I end, GlyphFormat formatting, bool onlyChangeColor)
            {
                if (lines.Count > 0)
                {
                    if (end.X > start.X)
                    {
                        lines[start.X].SetFormatting(start.Y, lines[start.X].Count - 1, formatting, onlyChangeColor);

                        for (int x = start.X + 1; x < end.X; x++)
                            lines[x].SetFormatting(formatting, onlyChangeColor);

                        lines[end.X].SetFormatting(0, end.Y, formatting, onlyChangeColor);
                    }
                    else
                    {
                        lines[start.X].SetFormatting(start.Y, end.Y, formatting, onlyChangeColor);
                    }

                    for (int n = start.X; n <= end.X; n++)
                        lines[n].UpdateSize();
                }
            }

            /// <summary>
            /// Removes characters within a specified range.
            /// </summary>
            public virtual void RemoveRange(Vector2I start, Vector2I end)
            {
                if (start.X < lines.Count && lines[start.X].Count > 0)
                {
                    if (end.X > start.X)
                    {
                        if (end.Y == (lines[end.X].Count - 1))
                            lines.RemoveAt(end.X);
                        else
                        {
                            lines[end.X].RemoveRange(0, lines[end.X].Count - end.Y);
                            lines[end.X].UpdateSize();
                        }

                        if (start.X + 1 < end.X)
                            lines.RemoveRange(start.X + 1, end.X - start.X - 1);

                        if (start.X > 0)
                            lines.RemoveAt(start.X);
                        else
                        {
                            lines[start.X].RemoveRange(start.Y, lines[start.X].Count - start.Y);
                            lines[start.X].UpdateSize();
                        }
                    }
                    else
                    {
                        if (start.X > 0 && start.Y == 0 && end.Y == (lines[start.X].Count - 1))
                            lines.RemoveAt(start.X);
                        else
                        {
                            lines[start.X].RemoveRange(start.Y, end.Y - start.Y + 1);
                            lines[start.X].UpdateSize();
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

            /// <summary>
            /// Determines whether the two characters at the indices provided indicate a word break.
            /// </summary>
            protected bool IsWordBreak(Vector2I iLeft, Vector2I iRight)
            {
                char left = lines.PooledLines[iLeft.X].Chars[iLeft.Y], 
                    right = lines.PooledLines[iRight.X].Chars[iRight.Y];

                return (left == ' ' || left == '-' || left == '_') && !(right == ' ' || right == '-' || right == '_');
            }

            /// <summary>
            /// Determines whether the two characters at the charBuffer indices provided indicate a word break.
            /// </summary>
            protected bool IsWordBreak(int iLeft, int iRight)
            {
                char left = charBuffer.Chars[iLeft],
                    right = charBuffer.Chars[iRight];

                return (left == ' ' || left == '-' || left == '_') && !(right == ' ' || right == '-' || right == '_');
            }
        }
    }
}