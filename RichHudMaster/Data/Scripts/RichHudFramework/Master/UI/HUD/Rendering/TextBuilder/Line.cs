using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using VRage;
using VRageMath;
using GlyphFormatMembers = VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>;

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
                protected abstract partial class Line : ILine
                {
                    public IRichChar this[int index] 
                    {
                        get
                        {
                            if (index >= 0 && index < Count)
                                return new RichChar(builder, this, index);
                            else
                                throw new Exception("Index was out of range. Must be non-negative and less than the size of the collection.");
                        }
                    }

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
                    /// Physical size of the line as rendered
                    /// </summary>
                    public Vector2 Size => _size * builder.Scale;

                    /// <summary>
                    /// Size of the line before scaling
                    /// </summary>
                    public Vector2 UnscaledSize => _size;

                    /// <summary>
                    /// Starting vertical position of the line starting from the center of the text element, sans text offset.
                    /// </summary>
                    public float VerticalOffset => _verticalOffset * builder.Scale;

                    /// <summary>
                    /// Read-only list of the characters in the line.
                    /// </summary>
                    public readonly IReadOnlyList<char> Chars;

                    /// <summary>
                    /// Read-only list of the formatted glyphs for each character in the line.
                    /// </summary>
                    public readonly IReadOnlyList<FormattedGlyph> FormattedGlyphs;

                    /// <summary>
                    /// Read-only list of the location and size information for each character in the line.
                    /// </summary>
                    public readonly IReadOnlyList<GlyphLocData> LocData;

                    /// <summary>
                    /// Read-only list of the QuadBoards for each character in the line.
                    /// </summary>
                    public readonly IReadOnlyList<QuadBoard> GlyphBoards;

                    public float _verticalOffset;

                    private readonly List<char> chars;
                    private readonly List<FormattedGlyph> formattedGlyphs;
                    private readonly List<GlyphLocData> locData;
                    private readonly List<QuadBoard> glyphBoards;

                    private Vector2 _size;
                    private readonly TextBuilder builder;

                    /// <summary>
                    /// Initializes a new TextBuilder Line with capacity for the given number of rich characters.
                    /// </summary>
                    protected Line(TextBuilder builder, int capacity = 6)
                    {
                        this.builder = builder;
                        chars = new List<char>(capacity);
                        formattedGlyphs = new List<FormattedGlyph>(capacity);
                        locData = new List<GlyphLocData>(capacity);
                        glyphBoards = new List<QuadBoard>(capacity);

                        Chars = chars;
                        FormattedGlyphs = formattedGlyphs;
                        LocData = locData;
                        GlyphBoards = glyphBoards;
                    }

                    /// <summary>
                    /// Sets the formatting for the given range of characters
                    /// </summary>
                    public void SetFormatting(GlyphFormat format)
                    {
                        for (int n = 0; n < Count; n++)
                        {
                            SetFormattingAt(n, format);
                        }
                    }

                    /// <summary>
                    /// Sets the formatting for the given range of characters in the line
                    /// </summary>
                    public void SetFormatting(int start, int end, GlyphFormat format)
                    {
                        for (int n = start; n <= end; n++)
                        {
                            SetFormattingAt(n, format);
                        }
                    }

                    /// <summary>
                    /// Sets the formatting of the character at the given index.
                    /// </summary>
                    public void SetFormattingAt(int index, GlyphFormat format)
                    {
                        if (!formattedGlyphs[index].format.Equals(format.data))
                        {
                            IFontStyle fontStyle = FontManager.GetFontStyle(format.StyleIndex);
                            Vector2 glyphSize;
                            Glyph glyph;

                            float scale = format.TextSize * fontStyle.FontScale;

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
                            glyphBoards[index] = glyph.GetQuadBoard(scale, format);
                        }
                    }

                    public void SetOffsetAt(int index, Vector2 offset)
                    {
                        var oldData = locData[index];
                        locData[index] = new GlyphLocData(oldData.bbSize, oldData.chSize, offset);
                    }

                    /// <summary>
                    /// Retrieves range of characters in the line specified and adds them to the list
                    /// given.
                    /// </summary>
                    public void GetRangeString(List<RichStringMembers> text, int start, int end)
                    {
                        StringBuilder sb;
                        RichStringMembers? lastString = null;

                        if (text.Count > 0)
                            lastString = text[text.Count - 1];

                        for (int ch = start; ch <= end; ch++)
                        {
                            GlyphFormat format = formattedGlyphs[ch].format;
                            GlyphFormatMembers lastFormat = lastString.Value.Item2;
                            bool formatEqual = lastString != null
                                && lastFormat.Item1 == format.data.Item1
                                && lastFormat.Item2 == format.data.Item2
                                && lastFormat.Item3 == format.data.Item3
                                && lastFormat.Item4 == format.data.Item4;

                            sb = formatEqual ? lastString.Value.Item1 : builder.sbPool.Get();
                            sb.Append(chars[ch]);
                            var nextString = new RichStringMembers(sb, format.data);

                            if (lastString == null || lastString.Value.Item1 != nextString.Item1)
                                text.Add(nextString);
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
                    /// Adds a new character to the end of the line with the given format
                    /// </summary>
                    public void AddNew(char ch, GlyphFormat format) =>
                        InsertNew(Count, ch, format);

                    /// <summary>
                    /// Adds the characters in the line given to the end of this line.
                    /// </summary>
                    public void AddRange(Line newChars) =>
                        InsertRange(Count, newChars);

                    /// <summary>
                    /// Inserts a new character at the index specified with the given format
                    /// </summary>
                    public void InsertNew(int index, char ch, GlyphFormat format)
                    {
                        IFontStyle fontStyle = FontManager.GetFontStyle(format.StyleIndex);
                        Vector2 glyphSize;
                        Glyph glyph;

                        float scale = format.TextSize * fontStyle.FontScale;

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
                        glyphBoards.Insert(index, glyph.GetQuadBoard(scale, format));

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
                    /// Recalculates the width and height of the line.
                    /// </summary>
                    public void UpdateSize()
                    {
                        _size = Vector2.Zero;

                        for (int n = 0; n < locData.Count; n++)
                        {
                            if (locData[n].chSize.Y > _size.Y)
                            {
                                _size.Y = locData[n].chSize.Y;
                            }

                            _size.X += locData[n].chSize.X;
                        }
                    }

                    public void GetText(StringBuilder stringBuilder)
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