using ParallelTasks;
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
                    /// Read-only list of the QuadBoards for each character in the line.
                    /// </summary>
                    public readonly IReadOnlyList<BoundedQuadBoard> GlyphBoards;

                    public float _verticalOffset;

                    private readonly List<char> chars;
                    private readonly List<FormattedGlyph> formattedGlyphs;
                    private readonly List<BoundedQuadBoard> glyphBoards;

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
                        glyphBoards = new List<BoundedQuadBoard>(capacity);

                        Chars = chars;
                        FormattedGlyphs = formattedGlyphs;
                        GlyphBoards = glyphBoards;
                    }

                    /// <summary>
                    /// Sets the formatting for the given range of characters
                    /// </summary>
                    public void SetFormatting(GlyphFormat format, bool onlyChangeColor)
                    {
                        SetFormatting(0, chars.Count, format, onlyChangeColor);
                    }

                    /// <summary>
                    /// Sets the formatting for the given range of characters in the line
                    /// </summary>
                    public void SetFormatting(int start, int end, GlyphFormat format, bool onlyChangeColor)
                    {
                        Vector4 bbColor = BillBoardUtils.GetBillBoardBoardColor(format.Data.Item4);

                        for (int n = start; n <= end; n++)
                        {
                            if (onlyChangeColor)
                            {
                                var bbData = glyphBoards[n];
                                var fmtGlyph = formattedGlyphs[n];
                                bbData.quadBoard.materialData.bbColor = bbColor;
                                fmtGlyph.format = format;

                                glyphBoards[n] = bbData;
                                formattedGlyphs[n] = fmtGlyph;
                            }
                            else if (!formattedGlyphs[n].format.Equals(format))
                            {
                                IFontStyle fontStyle = FontManager.GetFontStyle(format.Data.Item3);
                                float fontSize = format.Data.Item2 * fontStyle.FontScale;
                                Glyph glyph = fontStyle[chars[n]];
                                Vector2 glyphSize = new Vector2(glyph.advanceWidth, fontStyle.Height) * fontSize,
                                    bbSize = glyph.MatFrame.Material.size * fontSize;

                                formattedGlyphs[n] = new FormattedGlyph 
                                {
                                    chSize = glyphSize,
                                    format = format,
                                    glyph = glyph
                                };
                                glyphBoards[n] = new BoundedQuadBoard 
                                {
                                    bounds = new BoundingBox2(-.5f * bbSize, .5f * bbSize),
                                    quadBoard = glyph.GetQuadBoard(format, bbColor)
                                };
                            }
                        }
                    }

                    /// <summary>
                    /// Sets the formatting of the character at the given index.
                    /// </summary>
                    public void SetFormattingAt(int index, GlyphFormat format, bool onlyChangeColor)
                    {
                        SetFormatting(index, index, format, onlyChangeColor);
                    }

                    public void SetOffsetAt(int index, Vector2 offset)
                    {
                        var qb = glyphBoards[index];
                        var bounds = qb.bounds;
                        Vector2 halfSize = .5f * bounds.Size;

                        qb.bounds = BoundingBox2.CreateFromHalfExtent(offset, halfSize);
                        glyphBoards[index] = qb;
                    }

                    /// <summary>
                    /// Retrieves range of characters in the line specified and adds them to the list
                    /// given.
                    /// </summary>
                    public void GetRangeString(List<RichStringMembers> text, int start, int end)
                    {
                        StringBuilder sb;
                        RichStringMembers lastString = default(RichStringMembers);

                        if (text.Count > 0)
                            lastString = text[text.Count - 1];

                        for (int ch = start; ch <= end; ch++)
                        {
                            GlyphFormat format = formattedGlyphs[ch].format;
                            GlyphFormatMembers lastFormat = lastString.Item2;
                            bool formatEqual = lastString.Item1 != null
                                && lastFormat.Item1 == format.Data.Item1
                                && lastFormat.Item2 == format.Data.Item2
                                && lastFormat.Item3 == format.Data.Item3
                                && lastFormat.Item4 == format.Data.Item4;

                            sb = formatEqual ? lastString.Item1 : builder.sbPool.Get();
                            sb.Append(chars[ch]);
                            var nextString = new RichStringMembers(sb, format.Data);

                            if (lastString.Item1 != nextString.Item1)
                                text.Add(nextString);

                            lastString = nextString;
                        }
                    }

                    /// <summary>
                    /// Adds the character at the index in the line given to the end of this line.
                    /// </summary>
                    public void AddCharFromLine(int index, Line line)
                    {
                        chars.Add(line.chars[index]);
                        formattedGlyphs.Add(line.formattedGlyphs[index]);
                        glyphBoards.Add(line.glyphBoards[index]);

                        TrimExcess();
                    }

                    /// <summary>
                    /// Adds a new character to the end of the line with the given format
                    /// </summary>
                    public void AddNew(char ch, GlyphFormat format, Vector4 color) =>
                        InsertNew(chars.Count, ch, format, color);

                    /// <summary>
                    /// Adds the characters in the line given to the end of this line.
                    /// </summary>
                    public void AddRange(Line newChars) =>
                        InsertRange(chars.Count, newChars);

                    /// <summary>
                    /// Inserts a new character at the index specified with the given format
                    /// </summary>
                    public void InsertNew(int index, char ch, GlyphFormat format, Vector4 bbColor)
                    {
                        IFontStyle fontStyle = FontManager.GetFontStyle(format.Data.Item3);
                        float fontSize = format.Data.Item2 * fontStyle.FontScale;
                        Glyph glyph = fontStyle[ch];
                        Vector2 glyphSize = new Vector2(glyph.advanceWidth, fontStyle.Height) * fontSize,
                            bbSize = glyph.MatFrame.Material.size * fontSize;

                        chars.Insert(index, ch);
                        formattedGlyphs.Insert(index, new FormattedGlyph
                        {
                            chSize = glyphSize,
                            format = format,
                            glyph = glyph
                        });
                        glyphBoards.Insert(index, new BoundedQuadBoard
                        {
                            bounds = BoundingBox2.CreateFromHalfExtent(Vector2.Zero, .5f * bbSize),
                            quadBoard = glyph.GetQuadBoard(format, bbColor)
                        });

                        TrimExcess();
                    }

                    /// <summary>
                    /// Inserts the contents of the line given starting at the specified index.
                    /// </summary>
                    public void InsertRange(int index, Line newChars)
                    {
                        chars.InsertRange(index, newChars.chars);
                        formattedGlyphs.InsertRange(index, newChars.formattedGlyphs);
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
                        glyphBoards.RemoveRange(index, count);
                    }

                    /// <summary>
                    /// Removes all characters from the line.
                    /// </summary>
                    public void Clear()
                    {
                        chars.Clear();
                        formattedGlyphs.Clear();
                        glyphBoards.Clear();
                    }

                    /// <summary>
                    /// Ensures the line's capacity is at least the specified value.
                    /// </summary>
                    public void EnsureCapacity(int minCapacity)
                    {
                        chars.EnsureCapacity(minCapacity);
                        formattedGlyphs.EnsureCapacity(minCapacity);
                        glyphBoards.EnsureCapacity(minCapacity);
                    }

                    public void TrimExcess()
                    {
                        if (chars.Count > 20 && chars.Capacity > 5 * chars.Count)
                        {
                            chars.TrimExcess();
                            formattedGlyphs.TrimExcess();
                            glyphBoards.TrimExcess();
                        }
                    }

                    /// <summary>
                    /// Recalculates the width and height of the line.
                    /// </summary>
                    public void UpdateSize()
                    {
                        _size = Vector2.Zero;

                        if (chars.Count > 0)
                        {
                            for (int n = 0; n < formattedGlyphs.Count; n++)
                            {
                                FormattedGlyph fmtGlyph = formattedGlyphs[n];

                                if (fmtGlyph.chSize.Y > _size.Y)
                                    _size.Y = fmtGlyph.chSize.Y;

                                float chWidth = fmtGlyph.chSize.X;

                                if (chars[n] == '\t')
                                {
                                    BoundedQuadBoard qb = glyphBoards[n];
                                    IFontStyle fontStyle = FontManager.GetFontStyle(fmtGlyph.format.StyleIndex);
                                    float scale = fmtGlyph.format.TextSize * fontStyle.FontScale;

                                    chWidth = formattedGlyphs[n].glyph.advanceWidth * scale;
                                    float rem = _size.X % chWidth;

                                    if (rem < chWidth * .8f)
                                        chWidth -= rem;
                                    else // if it's really close, just skip to the next stop
                                        chWidth += (chWidth - rem);

                                    Vector2 bbSize = qb.bounds.Size;
                                    bbSize.X = chWidth;
                                    qb.bounds = BoundingBox2.CreateFromHalfExtent(qb.bounds.Center, .5f * bbSize);
                                    fmtGlyph.chSize.X = chWidth;

                                    formattedGlyphs[n] = fmtGlyph;
                                    glyphBoards[n] = qb;
                                }

                                _size.X += chWidth;
                            }
                        }
                    }
                }
            }
        }
    }
}