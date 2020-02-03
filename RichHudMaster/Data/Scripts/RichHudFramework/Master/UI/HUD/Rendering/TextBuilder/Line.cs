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
                /// <summary>
                /// Defines a line of formatted characters in a TextBuidler.
                /// </summary>
                protected abstract partial class Line : IIndexedCollection<IRichChar>, ILine
                {
                    IRichChar IIndexedCollection<IRichChar>.this[int index] => new RichChar(this, index);
                    internal RichChar this[int index] => new RichChar(this, index);

                    /// <summary>
                    /// The number of rich characters currently in the collection.
                    /// </summary>
                    public int Count => chars.Count;

                    /// <summary>
                    /// The maximum number of rich characters the line can hold without resizing.
                    /// </summary>
                    public int Capacity 
                    { 
                        get { return chars.Capacity; } 
                        set 
                        {
                            chars.Capacity = value;
                            formattedGlyphs.Capacity = value;
                            locData.Capacity = value;
                            glyphBoards.Capacity = value;
                        } 
                    }

                    /// <summary>
                    /// The total size of the line in pixels.
                    /// </summary>
                    public Vector2 Size => size;

                    private readonly List<char> chars;
                    private readonly List<FormattedGlyph> formattedGlyphs;
                    private readonly List<GlyphLocData> locData;
                    private readonly List<QuadBoard> glyphBoards;

                    private Vector2 size;

                    /// <summary>
                    /// Initializes a new TextBuilder Line with capacity for the given number of rich characters.
                    /// </summary>
                    protected Line(int capacity = 6)
                    {
                        chars = new List<char>(capacity);
                        formattedGlyphs = new List<FormattedGlyph>(capacity);
                        locData = new List<GlyphLocData>(capacity);
                        glyphBoards = new List<QuadBoard>(capacity);
                    }

                    /// <summary>
                    /// Sets the formatting for the given range of characters in the line using the given
                    /// scale.
                    /// </summary>
                    public void SetFormatting(GlyphFormat format, float scale)
                    {
                        for (int n = 0; n < Count; n++)
                        {
                            SetFormattingAt(n, format, scale);
                        }
                    }

                    /// <summary>
                    /// Sets the formatting for the given range of characters in the line using the given
                    /// scale.
                    /// </summary>
                    public void SetFormatting(int start, int end, GlyphFormat format, float scale)
                    {
                        for (int n = start; n <= end; n++)
                        {
                            SetFormattingAt(n, format, scale);
                        }
                    }

                    /// <summary>
                    /// Sets the formatting of the character at the given index.
                    /// </summary>
                    public void SetFormattingAt(int index, GlyphFormat format, float scale)
                    {
                        IFontStyle fontStyle = FontManager.GetFontStyle(format.StyleIndex);
                        Vector2 glyphSize;
                        Glyph glyph;

                        scale *= format.TextSize * fontStyle.FontScale;

                        if (chars[index] == '\n')
                        {
                            glyph = fontStyle[' '];
                            glyphSize = new Vector2(0f, fontStyle.Height) * scale;
                        }
                        else
                        {
                            glyph = fontStyle[chars[index]];
                            glyphSize = new Vector2(glyph.advanceWidth, fontStyle.Height) * scale;
                        }

                        formattedGlyphs[index] = new FormattedGlyph(glyph, format);
                        locData[index] = new GlyphLocData(glyph.material.size * scale, glyphSize);
                        glyphBoards[index] = glyph.GetQuadBoard(scale, format.Color);
                    }

                    /// <summary>
                    /// Retrieves range of characters in the line specified and adds them to the list
                    /// given.
                    /// </summary>
                    public void GetRangeString(IList<RichStringMembers> text, int start, int end)
                    {
                        for (int ch = start; ch <= end; ch++)
                        {
                            StringBuilder richString = new StringBuilder();
                            GlyphFormat format = formattedGlyphs[ch].format;
                            ch--;

                            do
                            {
                                ch++;
                                richString.Append(chars[ch]);
                            }
                            while (ch + 1 <= end && format.Equals(formattedGlyphs[ch + 1].format));

                            text.Add(new RichStringMembers(richString, format.data));
                        }
                    }

                    /// <summary>
                    /// Adds the character at the index in the line given to the end of this line.
                    /// </summary>
                    public void AddCharFromLine(int index, Line line)
                    {
                        chars.Add(line.chars[index]);
                        formattedGlyphs.Add(line.formattedGlyphs[index]);
                        locData.Add(line.locData[index]);
                        glyphBoards.Add(line.glyphBoards[index]);

                        TrimExcess();
                    }

                    /// <summary>
                    /// Adds a new character to the end of the line with the given format and scale.
                    /// </summary>
                    public void AddNew(char ch, GlyphFormat format, float scale) =>
                        InsertNew(Count, ch, format, scale);

                    /// <summary>
                    /// Adds the characters in the line given to the end of this line.
                    /// </summary>
                    public void AddRange(Line newChars) =>
                        InsertRange(Count, newChars);

                    /// <summary>
                    /// Inserts a new character at the index specified with the given format and scale.
                    /// </summary>
                    public void InsertNew(int index, char ch, GlyphFormat format, float scale)
                    {
                        IFontStyle fontStyle = FontManager.GetFontStyle(format.StyleIndex);
                        Vector2 glyphSize;
                        Glyph glyph;

                        scale *= format.TextSize * fontStyle.FontScale;

                        if (ch == '\n')
                        {
                            glyph = fontStyle[' '];
                            glyphSize = new Vector2(0f, fontStyle.Height) * scale;
                        }
                        else
                        {
                            glyph = fontStyle[ch];
                            glyphSize = new Vector2(glyph.advanceWidth, fontStyle.Height) * scale;
                        }

                        chars.Insert(index, ch);
                        formattedGlyphs.Insert(index, new FormattedGlyph(glyph, format));
                        locData.Insert(index, new GlyphLocData(glyph.material.size * scale, glyphSize));
                        glyphBoards.Insert(index, glyph.GetQuadBoard(scale, format.Color));

                        TrimExcess();
                    }

                    /// <summary>
                    /// Inserts the contents of the line given starting at the specified index.
                    /// </summary>
                    public void InsertRange(int index, Line newChars)
                    {
                        chars.InsertRange(index, newChars.chars);
                        formattedGlyphs.InsertRange(index, newChars.formattedGlyphs);
                        locData.InsertRange(index, newChars.locData);
                        glyphBoards.InsertRange(index, newChars.glyphBoards);

                        TrimExcess();
                    }

                    /// <summary>
                    /// Removes a range of characters from the line.
                    /// </summary>
                    public void RemoveRange(int index, int count)
                    {
                        chars.RemoveRange(index, count);
                        formattedGlyphs.RemoveRange(index, count);
                        locData.RemoveRange(index, count);
                        glyphBoards.RemoveRange(index, count);
                    }

                    /// <summary>
                    /// Removes all characters from the line.
                    /// </summary>
                    public void Clear()
                    {
                        chars.Clear();
                        formattedGlyphs.Clear();
                        locData.Clear();
                        glyphBoards.Clear();
                    }

                    /// <summary>
                    /// Ensures the line's capacity is at least the specified value.
                    /// </summary>
                    public void EnsureCapacity(int minCapacity)
                    {
                        chars.EnsureCapacity(minCapacity);
                        formattedGlyphs.EnsureCapacity(minCapacity);
                        locData.EnsureCapacity(minCapacity);
                        glyphBoards.EnsureCapacity(minCapacity);
                    }

                    public void TrimExcess()
                    {
                        if (Count > 20 && Capacity > 5 * Count)
                        {
                            chars.TrimExcess();
                            formattedGlyphs.TrimExcess();
                            locData.TrimExcess();
                            glyphBoards.TrimExcess();
                        }
                    }

                    /// <summary>
                    /// Resizes the offsets and sizes of the line and its characters using the given
                    /// scale.
                    /// </summary>
                    public void Rescale(float scale)
                    {
                        size *= scale;

                        for (int ch = 0; ch < chars.Count; ch++)
                        {
                            locData[ch] = locData[ch].Rescale(scale);
                        }
                    }

                    /// <summary>
                    /// Recalculates the width and height of the line.
                    /// </summary>
                    public void UpdateSize()
                    {
                        size = Vector2.Zero;

                        for (int n = 0; n < locData.Count; n++)
                        {
                            if (locData[n].chSize.Y > size.Y)
                            {
                                size.Y = locData[n].chSize.Y;
                            }

                            size.X += locData[n].chSize.X;
                        }
                    }

                    internal void GetText(StringBuilder stringBuilder)
                    {
                        stringBuilder.EnsureCapacity(stringBuilder.Length + chars.Count);

                        for (int n = 0; n < chars.Count; n++)
                            stringBuilder.Append(chars[n]);
                    }

                    /// <summary>
                    /// Returns the contents of the line as an unformatted string.
                    /// </summary>
                    public override string ToString()
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.EnsureCapacity(chars.Count);

                        for (int n = 0; n < chars.Count; n++)
                            sb.Append(chars[n]);

                        return sb.ToString();
                    }
                }
            }
        }
    }
}