using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using VRage;
using VRageMath;
using GlyphFormatMembers = VRage.MyTuple<VRageMath.Vector2I, int, VRageMath.Color, float>;

namespace RichHudFramework
{
    using RichStringMembers = MyTuple<StringBuilder, GlyphFormatMembers>;

    namespace UI
    {
        namespace Rendering.Server
        {
            public abstract partial class TextBuilder
            {
                protected class Line : IReadOnlyCollection<RichChar>, ILine
                {
                    IRichChar IIndexedCollection<IRichChar>.this[int index] => chars[index];
                    public RichChar this[int index] { get { return chars[index]; } set { chars[index] = value; } }
                    public int Count => chars.Count;
                    public int Capacity => chars.Capacity;
                    public Vector2 Size => size;

                    private readonly List<RichChar> chars;
                    private readonly List<MatBoard> glyphBoards;
                    private Vector2 size;

                    public Line(int capacity = 6)
                    {
                        chars = new List<RichChar>(capacity);
                    }

                    public IEnumerator<RichChar> GetEnumerator() =>
                        chars.GetEnumerator();

                    IEnumerator IEnumerable.GetEnumerator() =>
                        GetEnumerator();

                    public List<RichStringMembers> GetString()
                    {
                        List<RichStringMembers> text = new List<RichStringMembers>();
                        GetRangeString(text, 0, Count - 1);

                        return text;
                    }

                    public void GetRangeString(List<RichStringMembers> text, int start, int end)
                    {
                        for (int ch = start; ch <= end; ch++)
                        {
                            StringBuilder richString = new StringBuilder();
                            GlyphFormat format = chars[ch].Format;
                            ch--;

                            do
                            {
                                ch++;
                                richString.Append(chars[ch].Ch);
                            }
                            while (ch + 1 <= end && format.Equals(chars[ch + 1].Format));

                            text.Add(new RichStringMembers(richString, format.data));
                        }
                    }

                    public void Add(RichChar ch) =>
                        chars.Add(ch);

                    public void AddRange(IList<RichChar> newChars) =>
                        chars.AddRange(newChars);

                    public void Insert(int index, RichChar ch) =>
                        chars.Insert(index, ch);

                    public void InsertRange(int index, IList<RichChar> newChars) =>
                        chars.InsertRange(index, newChars);

                    public void RemoveRange(int index, int count) =>
                        chars.RemoveRange(index, count);

                    public void Clear() =>
                        chars.Clear();

                    public void Rescale(float scale)
                    {
                        size *= scale;

                        for (int ch = 0; ch < chars.Count; ch++)
                        {
                            chars[ch].Rescale(scale);
                        }
                    }

                    /// <summary>
                    /// Recalculates the width and height of the line.
                    /// </summary>
                    public void UpdateSize()
                    {
                        size = Vector2.Zero;

                        for (int n = 0; n < chars.Count; n++)
                        {
                            if (chars[n].Size.Y > size.Y)
                            {
                                size.Y = chars[n].Size.Y;
                            }

                            size.X += chars[n].Size.X;
                        }
                    }

                    public override string ToString()
                    {
                        StringBuilder sb = new StringBuilder(Count);

                        for (int n = 0; n < chars.Count; n++)
                            sb.Append(chars[n].Ch);

                        return sb.ToString();
                    }
                }
            }
        }
    }
}