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
                    public int Count { get; private set; }

                    /// <summary>
                    /// The maximum number of rich characters the line can hold without resizing.
                    /// </summary>
                    public int Capacity
                    {
                        get { return chars.Length; }
                        set { SetCapacity(value); }
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
                    public IReadOnlyList<char> Chars { get; private set; }

                    /// <summary>
                    /// Read-only list of the formatted glyphs for each character in the line.
                    /// </summary>
                    public IReadOnlyList<FormattedGlyph> FormattedGlyphs { get; private set; }

                    /// <summary>
                    /// Read-only list of the QuadBoards for each character in the line.
                    /// </summary>
                    public readonly IReadOnlyList<BoundedQuadBoard> GlyphBoards;

                    public float _verticalOffset;

                    /// <summary>
                    /// Used to indicate changes to text that would invalidate the TextBoard
                    /// billboard cache.
                    /// </summary>
                    public bool isQuadCacheStale;

                    private char[] chars;
                    private FormattedGlyph[] formattedGlyphs;
                    private readonly List<BoundedQuadBoard> glyphBoards;

                    private Vector2 _size;
                    private readonly TextBuilder builder;

                    /// <summary>
                    /// True if characters appended/inserted between frames are equal to those
                    /// still stored internally. Does not account for changes in length.
                    /// </summary>
                    private bool canTextBeEqual;

                    /// <summary>
                    /// Used to check for changes in line length between frames.
                    /// </summary>
                    private int lastCount;

                    /// <summary>
                    /// Initializes a new TextBuilder Line with capacity for the given number of rich characters.
                    /// </summary>
                    protected Line(TextBuilder builder, int capacity = 0)
                    {
                        this.builder = builder;
                        chars = new char[capacity];
                        formattedGlyphs = new FormattedGlyph[capacity];
                        glyphBoards = new List<BoundedQuadBoard>(capacity);

                        Chars = chars;
                        FormattedGlyphs = formattedGlyphs;
                        GlyphBoards = glyphBoards;

                        isQuadCacheStale = true;
                        canTextBeEqual = false;
                        lastCount = 0;
                        Count = 0;
                    }

                    /// <summary>
                    /// Sets the formatting for the given range of characters
                    /// </summary>
                    public void SetFormatting(GlyphFormat format)
                    {
                        if (Count > 0)
                            SetFormatting(0, Count - 1, format);
                    }

                    /// <summary>
                    /// Sets the formatting for the given range of characters in the line
                    /// </summary>
                    public void SetFormatting(int start, int end, GlyphFormat format)
                    {
                        if (Count == 0 || end < start)
                            return;
                        else if (start < 0 || end < 0 || start >= Count || end >= Count)
                            throw new Exception($"Index was out of range. Start: {start} End: {end} Count: {Count}");

                        for (int n = start; n <= end; n++)
                        {
                            GlyphFormatMembers lastFormat = formattedGlyphs[n].format.Data;
                            bool formatEqual =
                                lastFormat.Item1 == format.Data.Item1
                                && lastFormat.Item2 == format.Data.Item2
                                && lastFormat.Item3 == format.Data.Item3
                                && lastFormat.Item4 == format.Data.Item4;

                            if (!formatEqual)
                            {
                                IFontStyle fontStyle = FontManager.GetFontStyle(format.Data.Item3);
                                float fontSize = format.Data.Item2 * fontStyle.FontScale;
                                Glyph glyph = fontStyle[chars[n]];
                                Vector2 glyphSize = new Vector2(glyph.advanceWidth, fontStyle.Height) * fontSize;

                                formattedGlyphs[n] = new FormattedGlyph
                                {
                                    chSize = glyphSize,
                                    format = format,
                                    glyph = glyph
                                };

                                canTextBeEqual = false;
                            }
                        }
                    }

                    /// <summary>
                    /// Sets the formatting of the character at the given index.
                    /// </summary>
                    public void SetFormattingAt(int index, GlyphFormat format)
                    {
                        SetFormatting(index, index, format);
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
                        if (Count == chars.Length)
                            SetCapacity(Count + 1);

                        if (canTextBeEqual)
                        {
                            if (chars[Count] == line.chars[index])
                            {
                                GlyphFormatMembers format = line.formattedGlyphs[index].format.Data,
                                    lastFormat = formattedGlyphs[Count].format.Data;
                                bool formatEqual =
                                    lastFormat.Item1 == format.Item1
                                    && lastFormat.Item2 == format.Item2
                                    && lastFormat.Item3 == format.Item3
                                    && lastFormat.Item4 == format.Item4;

                                canTextBeEqual = formatEqual;
                            }
                            else
                                canTextBeEqual = false;
                        }

                        if (!canTextBeEqual)
                        {
                            chars[Count] = line.chars[index];
                            formattedGlyphs[Count] = line.formattedGlyphs[index];
                        }

                        Count++;
                    }

                    /// <summary>
                    /// Adds the characters in the line given to the end of this line.
                    /// </summary>
                    public void AddRange(Line newChars) =>
                        InsertRange(Count, newChars);

                    /// <summary>
                    /// Appends the contents of the given rich string to the line
                    /// </summary>
                    public void AppendRichString(RichStringMembers richString, bool allowSpecialChars)
                    {
                        StringBuilder text = richString.Item1;
                        GlyphFormat format = new GlyphFormat(richString.Item2);

                        if (text.Length > 0)
                        {
                            int newCount = text.Length + Count;

                            if (newCount > chars.Length)
                                SetCapacity(newCount);

                            for (int n = 0; n < text.Length; n++)
                            {
                                if (text[n] >= ' ' || allowSpecialChars && (text[n] == '\n' || text[n] == '\t'))
                                {
                                    if (canTextBeEqual)
                                    {
                                        if (chars[Count] == text[n])
                                        {
                                            GlyphFormatMembers lastFormat = formattedGlyphs[Count].format.Data;
                                            bool formatEqual =
                                                lastFormat.Item1 == format.Data.Item1
                                                && lastFormat.Item2 == format.Data.Item2
                                                && lastFormat.Item3 == format.Data.Item3
                                                && lastFormat.Item4 == format.Data.Item4;

                                            canTextBeEqual = formatEqual;
                                        }
                                        else
                                            canTextBeEqual = false;
                                    }

                                    if (!canTextBeEqual)
                                    {
                                        IFontStyle fontStyle = FontManager.GetFontStyle(format.Data.Item3);
                                        float fontSize = format.Data.Item2 * fontStyle.FontScale;
                                        Glyph glyph = fontStyle[text[n]];
                                        var glyphSize = new Vector2(glyph.advanceWidth, fontStyle.Height) * fontSize;
                                        var fGlyph = new FormattedGlyph
                                        {
                                            chSize = glyphSize,
                                            format = format,
                                            glyph = glyph
                                        };

                                        chars[Count] = text[n];
                                        formattedGlyphs[Count] = fGlyph;
                                    }

                                    Count++;
                                }
                            }
                        }
                    }

                    /// <summary>
                    /// Inserts the contents of the line given starting at the specified index.
                    /// </summary>
                    public void InsertRange(int index, Line newChars)
                    {
                        if (newChars.Count > 0)
                        {
                            int newCount = newChars.Count + Count;

                            if (newCount > chars.Length)
                                SetCapacity(newCount);

                            if (Count > index)
                            {
                                Array.Copy(chars, index, chars, index + newChars.Count, Count - index);
                                Array.Copy(formattedGlyphs, index, formattedGlyphs, index + newChars.Count, Count - index);

                                // Non-sequential update
                                canTextBeEqual = false;
                            }

                            if (canTextBeEqual)
                            {
                                for (int i = 0; i < newChars.Count; i++)
                                {
                                    if (canTextBeEqual && newChars.chars[i] != chars[i + index])
                                        canTextBeEqual = false;

                                    chars[i + index] = newChars.chars[i];
                                }
                            }
                            else
                            {
                                Array.Copy(newChars.chars, 0, chars, index, newChars.Count);
                            }

                            if (canTextBeEqual)
                            {
                                for (int i = 0; i < newChars.Count; i++)
                                {
                                    if (canTextBeEqual)
                                    {
                                        GlyphFormatMembers lastFormat = formattedGlyphs[i + index].format.Data,
                                            format = newChars.formattedGlyphs[i].format.Data;
                                        bool formatEqual =
                                            lastFormat.Item1 == format.Item1
                                            && lastFormat.Item2 == format.Item2
                                            && lastFormat.Item3 == format.Item3
                                            && lastFormat.Item4 == format.Item4;

                                        canTextBeEqual = formatEqual;
                                    }

                                    formattedGlyphs[i + index] = newChars.formattedGlyphs[i];
                                }
                            }
                            else
                            {
                                Array.Copy(newChars.formattedGlyphs, 0, formattedGlyphs, index, newChars.Count);
                            }

                            Count = newCount;
                        }
                    }

                    /// <summary>
                    /// Removes a range of characters from the line.
                    /// </summary>
                    public void RemoveRange(int index, int count)
                    {
                        if (count > 0 && Count > 0)
                        {
                            Count -= count;

                            if (index < Count)
                            {
                                Array.Copy(chars, index + count, chars, index, Count - index);
                                Array.Copy(formattedGlyphs, index + count, formattedGlyphs, index, Count - index);
                                canTextBeEqual = false;
                            }

                            if (Count == 0)
                                canTextBeEqual = true;
                        }
                    }

                    /// <summary>
                    /// Removes all characters from the line.
                    /// </summary>
                    public void Clear()
                    {
                        canTextBeEqual = true;
                        Count = 0;
                    }

                    /// <summary>
                    /// Ensures the line's capacity is at least the specified value.
                    /// </summary>
                    public void EnsureCapacity(int minCapacity)
                    {
                        if (chars.Length < minCapacity)
                        {
                            SetCapacity(minCapacity);
                        }
                    }

                    /// <summary>
                    /// Trims line capacity to current length
                    /// </summary>
                    public void TrimExcess()
                    {
                        if (Count > 20 && chars.Length > 5 * Count)
                        {
                            SetCapacity(Count);
                        }
                    }

                    /// <summary>
                    /// Sets the capacity of the line to the given number of characters
                    /// </summary>
                    public void SetCapacity(int newCapacity)
                    {
                        newCapacity = Math.Max(Count + 6, newCapacity);
                        Array.Resize(ref chars, newCapacity);
                        Array.Resize(ref formattedGlyphs, newCapacity);

                        Chars = chars;
                        FormattedGlyphs = formattedGlyphs;
                    }

                    /// <summary>
                    /// Recalculates the width and height of the line.
                    /// </summary>
                    public void UpdateSize()
                    {
                        _size = Vector2.Zero;

                        if (Count > 0)
                        {
                            for (int n = 0; n < Count; n++)
                            {
                                FormattedGlyph fmtGlyph = formattedGlyphs[n];

                                if (fmtGlyph.chSize.Y > _size.Y)
                                    _size.Y = fmtGlyph.chSize.Y;

                                float chWidth = fmtGlyph.chSize.X;

                                if (chars[n] == '\t')
                                {
                                    IFontStyle fontStyle = FontManager.GetFontStyle(fmtGlyph.format.StyleIndex);
                                    float scale = fmtGlyph.format.TextSize * fontStyle.FontScale;

                                    chWidth = formattedGlyphs[n].glyph.advanceWidth * scale;
                                    float rem = _size.X % chWidth;

                                    if (rem < chWidth * .8f)
                                        chWidth -= rem;
                                    else // if it's really close, just skip to the next stop
                                        chWidth += (chWidth - rem);

                                    fmtGlyph.chSize.X = chWidth;
                                    formattedGlyphs[n] = fmtGlyph;
                                }

                                _size.X += chWidth;
                            }
                        }
                    }

                    public void UpdateGlyphBoards()
                    {
                        if (!canTextBeEqual || Count != lastCount || glyphBoards.Count != Count)
                        {
                            isQuadCacheStale = true;

                            glyphBoards.Clear();
                            glyphBoards.EnsureCapacity(Count);

                            for (int i = 0; i < Count; i++)
                            {
                                FormattedGlyph fGlyph = formattedGlyphs[i];
                                IFontStyle fontStyle = FontManager.GetFontStyle(fGlyph.format.Data.Item3);
                                float fontSize = fGlyph.format.Data.Item2 * fontStyle.FontScale;
                                Vector2 bbSize = Vector2.Max(fGlyph.glyph.MatFrame.Material.size * fontSize, fGlyph.chSize);

                                glyphBoards.Add(new BoundedQuadBoard
                                {
                                    bounds = BoundingBox2.CreateFromHalfExtent(Vector2.Zero, .5f * bbSize),
                                    quadBoard = fGlyph.glyph.GetQuadBoard(fGlyph.format, fGlyph.format.Color.GetBbColor())
                                });
                            }

                            if (glyphBoards.Count > 20 && glyphBoards.Capacity > 5 * glyphBoards.Count)
                            {
                                glyphBoards.TrimExcess();
                            }
                        }

                        canTextBeEqual = true;
                        lastCount = Count;

                        TrimExcess();
                    }
                }
            }
        }
    }
}