using System;
using System.Collections.Generic;
using System.Text;
using VRage;
using VRageMath;
using GlyphFormatMembers = VRage.MyTuple<VRageMath.Vector2I, int, VRageMath.Color, float>;

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
            public float Scale { get { return scale; } set { RescaleText(value / scale); scale = value; } }

            protected IRichCharFull this[Vector2I index] => lines[index.X][index.Y];
            protected readonly LinePool lines;
            protected readonly Line charBuffer;
            private float scale;

            protected FormattedTextBase(LinePool lines)
            {
                this.lines = lines;
                scale = 1f;
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
            public virtual void SetFormatting(Vector2I start, Vector2I end, GlyphFormat formatting)
            {
                if (lines.Count > 0)
                {
                    if (end.X > start.X)
                    {
                        lines[start.X].SetFormatting(start.Y, lines[start.X].Count - 1, formatting, Scale);

                        for (int x = start.X + 1; x < end.X; x++)
                            lines[x].SetFormatting(formatting, Scale);

                        lines[start.X].SetFormatting(0, end.Y, formatting, Scale);
                    }
                    else
                    {
                        lines[start.X].SetFormatting(start.Y, end.Y, formatting, Scale);
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
            /// Builds a list of <see cref="IRichCharFull"/>s from RichString data.
            /// </summary>
            protected static void GetRichChars(RichStringMembers richString, Line charBuffer, GlyphFormat previous, float scale, Func<char, bool> FilterFunc)
            {
                StringBuilder text = richString.Item1;
                GlyphFormat format;

                if (previous == null || !previous.data.Equals(richString.Item2))
                    format = new GlyphFormat(richString.Item2);
                else
                    format = previous;

                charBuffer.EnsureCapacity(charBuffer.Count + text.Length);

                for (int n = 0; n < text.Length; n++)
                {
                    if (FilterFunc(text[n]))
                        charBuffer.AddNew(text[n], format, scale);
                }
            }

            /// <summary>
            /// Sets the text to the given scale.
            /// </summary>
            protected virtual void RescaleText(float scale)
            {
                for (int line = 0; line < lines.Count; line++)
                {
                    lines[line].Rescale(scale);
                }
            }

            /// <summary>
            /// Returns the glyph formatting of the character immediately preceding the one
            /// at the index given, if it exists.
            /// </summary>
            protected GlyphFormat GetPreviousFormat(Vector2I index)
            {
                if (TryGetLastIndex(index, out index))
                    return this[index].Format;
                else
                    return null;
            }

            /// <summary>
            /// Clamps the given index within the range of valid indices.
            /// </summary>
            protected Vector2I ClampIndex(Vector2I index)
            {
                index.X = Utils.Math.Clamp(index.X, 0, lines.Count);
                index.Y = Utils.Math.Clamp(index.Y, 0, (lines.Count > 0) ? lines[index.X].Count : 0);

                return index;
            }

            /// <summary>
            /// Retrieves the index of the character immediately preceeding the index given. Returns
            /// true if successful. Otherwise, the beginning of the collection has been reached.
            /// </summary>
            protected virtual bool TryGetLastIndex(Vector2I index, out Vector2I lastIndex)
            {
                if (index.Y > 0)
                    lastIndex = new Vector2I(index.X, index.Y - 1);
                else if (index.X > 0)
                    lastIndex = new Vector2I(index.X - 1, lines[index.X - 1].Count - 1);
                else
                {
                    lastIndex = index;
                    return false;
                }

                return true;
            }

            /// <summary>
            /// Retrieves the index of the character immediately following the index given. Returns
            /// true if successful. Otherwise, the end of the collection has been reached.
            /// </summary>
            protected virtual bool TryGetNextIndex(Vector2I index, out Vector2I nextIndex)
            {
                if (index.X < lines.Count && index.Y + 1 < lines[index.X].Count)
                    nextIndex = new Vector2I(index.X, index.Y + 1);
                else if (index.X + 1 < lines.Count)
                    nextIndex = new Vector2I(index.X + 1, 0);
                else
                {
                    nextIndex = index;
                    return false;
                }

                return true;
            }
        }
    }
}