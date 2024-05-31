using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using VRage;
using VRageMath;
using RichHudFramework.UI.Server;
using GlyphFormatMembers = VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>;
using VRage.Utils;
using System.Linq;

namespace RichHudFramework
{
    using static VRageRender.MyBillboard;
    using BoolProp = MyTuple<Func<bool>, Action<bool>>;
    using FloatProp = MyTuple<Func<float>, Action<float>>;
    using RichStringMembers = MyTuple<StringBuilder, GlyphFormatMembers>;
    using Vec2Prop = MyTuple<Func<Vector2>, Action<Vector2>>;

    namespace UI
    {
        using TextBuilderMembers = MyTuple<
            MyTuple<Func<int, int, object>, Func<int>>, // GetLineMember, GetLineCount
            Func<Vector2I, int, object>, // GetCharMember
            Func<object, int, object>, // GetOrSetMember
            Action<IList<RichStringMembers>, Vector2I>, // Insert
            Action<IList<RichStringMembers>>, // SetText
            Action // Clear
        >;
        using FlatTriangleBillboardData = MyTuple<
            BlendTypeEnum, // blendType
            Vector2I, // bbID + matrixID
            MyStringId, // material
            MyTuple<Vector4, BoundingBox2?>, // color + mask
            MyTuple<Vector2, Vector2, Vector2>, // texCoords
            MyTuple<Vector2, Vector2, Vector2> // flat pos
        >;

        namespace Rendering.Server
        {
            using TextBoardMembers8 = MyTuple<
                TextBuilderMembers,
                FloatProp, // Scale
                Func<Vector2>, // Size
                Func<Vector2>, // TextSize
                Vec2Prop, // FixedSize
                Action<Vector2, MatrixD> // Draw 
            >;
            using TextBoardMembers = MyTuple<
                TextBuilderMembers,
                FloatProp, // Scale
                Func<Vector2>, // Size
                Func<Vector2>, // TextSize
                Vec2Prop, // FixedSize
                Action<BoundingBox2, BoundingBox2, MatrixD[]> // Draw 
            >;

            public class TextBoard : TextBuilder, ITextBoard
            {
                /// <summary>
                /// Raised when a change is made to the text.
                /// </summary>
                public event Action TextChanged;

                /// <summary>
                /// Base text size. Compounds text scaling specified by <see cref="GlyphFormat"/>ting.
                /// </summary>
                public override float Scale { get; set; }

                /// <summary>
                /// Size of the text box as rendered. If AutoResize == true, Size == TextSize, otherwise
                /// Size == FixedSize
                /// </summary>
                public Vector2 Size => (AutoResize ? _textSize : _fixedSize) * Scale;

                /// <summary>
                /// Full text size including any text outside the visible range.
                /// </summary>
                public Vector2 TextSize => _textSize * Scale;

                /// <summary>
                /// Size of the text box when AutoResize is set to false. Does nothing otherwise.
                /// </summary>
                public Vector2 FixedSize
                {
                    get { return _fixedSize * Scale; }
                    set { _fixedSize = value / Scale; }
                }

                /// <summary>
                /// Used to change the position of the text within the text element. AutoResize must be disabled for this to work.
                /// </summary>
                public Vector2 TextOffset
                {
                    get { return _textOffset * Scale; }
                    set { _textOffset = value / Scale; }
                }

                /// <summary>
                /// Returns the range of lines visible.
                /// </summary>
                public Vector2I VisibleLineRange => lineRange;

                /// <summary>
                /// If true, the text board will automatically resize to fit the text.
                /// </summary>
                public bool AutoResize { get; set; }

                /// <summary>
                /// If true, the text will be vertically aligned to the center of the text board.
                /// </summary>
                public bool VertCenterText { get; set; }

                private Vector2I lineRange, lastLineRange;
                private BoundingBox2 lastBox, lastMask;
                private Vector2 
                    _size, 
                    _textSize, 
                    _fixedSize, 
                    _textOffset, 
                    lastFixedSize,
                    lastTextSize,
                    lastTextOffset;
                private bool 
                    isUpdateEventPending,
                    areOffsetsStale,
                    isQuadCacheStale,
                    isLineRangeStale,
                    isBbCacheStale;

                private readonly Stopwatch eventTimer;
                private readonly List<UnderlineBoard> underlines;
                private readonly List<FlatTriangleBillboardData> bbCache;
                private readonly MatrixD[] matRef;

                public TextBoard()
                {
                    Scale = 1f;
                    AutoResize = true;
                    VertCenterText = true;

                    Format = GlyphFormat.White;
                    _fixedSize = new Vector2(100f);

                    matRef = new MatrixD[1];
                    underlines = new List<UnderlineBoard>();
                    bbCache = new List<FlatTriangleBillboardData>();

                    eventTimer = new Stopwatch();
                    eventTimer.Start();

                    lineRange.Y = -1;
                    lastFixedSize = Vector2.PositiveInfinity;
                    lastTextOffset = Vector2.PositiveInfinity;

                    areOffsetsStale = true;
                    isQuadCacheStale = true;
                    isLineRangeStale = true;
                    isBbCacheStale = true;
                }

                protected override void SetWrapWidth(float width)
                {
                    base.SetWrapWidth(width);

                    if (builderMode == TextBuilderModes.Wrapped)
                        _fixedSize.X = width;
                }

                /// <summary>
                /// Calculates the minimum offset needed to ensure that the character at the specified index
                /// is within the visible range.
                /// </summary>
                public void MoveToChar(Vector2I index)
                {
                    if (!AutoResize && lines.Count > 0)
                    {
                        index.X = MathHelper.Clamp(index.X, 0, lines.Count - 1);
                        index.Y = MathHelper.Clamp(index.Y, 0, lines[index.X].Count - 1);

                        if (lines[index.X].Count > 0)
                        {
                            UpdateGlyphs();

                            if (index.X < lineRange.X || index.X > lineRange.Y)
                            {
                                if (BuilderMode != TextBuilderModes.Unlined)
                                    UpdateVerticalOffset(index.X);
                                else
                                    _textOffset.Y = 0f;
                            }

                            if (BuilderMode != TextBuilderModes.Wrapped)
                                _textOffset.X = GetCharRangeOffset(index);
                            else
                                _textOffset.X = 0f;
                        }
                    }
                }

                /// <summary>
                /// Finds the first line visible in the range that includes the given line index.
                /// </summary>
                private void UpdateVerticalOffset(int index)
                {
                    Line line = lines[index];

                    if (index > lineRange.Y) // Scroll down
                    {
                        _textOffset.Y = -line._verticalOffset;
                        _textOffset.Y += (VertCenterText || AutoResize) ? _textSize.Y * .5f : _fixedSize.Y * .5f;
                        _textOffset.Y -= (_fixedSize.Y - line.UnscaledSize.Y);
                    }
                    else if (index < lineRange.X) // Scroll up
                    {
                        _textOffset.Y = -line._verticalOffset;
                        _textOffset.Y += (VertCenterText || AutoResize) ? _textSize.Y * .5f : _fixedSize.Y * .5f;
                    }
                }

                /// <summary>
                /// Calculates the horizontal offset needed to ensure that the character specified is within
                /// the visible range.
                /// </summary>
                private float GetCharRangeOffset(Vector2I index)
                {
                    Line line = lines[index.X];
                    float offset = _textOffset.X;

                    if (line.Count > 0)
                    {
                        if (index.Y != 0)
                        {
                            line.UpdateGlyphBoards();

                            Vector2 chSize = line.FormattedGlyphs[index.Y].chSize,
                                bbOffset = line.GlyphBoards[index.Y].bounds.Center;

                            float minOffset = -bbOffset.X + chSize.X * .5f - _fixedSize.X * .5f,
                                maxOffset = minOffset - chSize.X + _fixedSize.X;

                            offset = MathHelper.Clamp(offset, minOffset, maxOffset);
                        }
                        else
                            offset = 0f;
                    }

                    return offset;
                }

                /// <summary>
                /// Returns the index of the character closest to the given offset.
                /// </summary>
                public Vector2I GetCharAtOffset(Vector2 charOffset)
                {
                    int line = 0, ch = 0;
                    charOffset /= Scale;
                    charOffset = Vector2.Clamp(charOffset, -_size * .5f, _size * .5f);
                    charOffset -= _textOffset;

                    if (lines.Count > 0)
                    {
                        line = GetLineAt(charOffset.Y);
                        ch = GetCharAt(line, charOffset.X);
                    }

                    return new Vector2I(line, ch);
                }

                /// <summary>
                /// Returns the index of the line closest to the given offset.
                /// </summary>
                private int GetLineAt(float offset)
                {
                    for (int line = lineRange.X; line <= lineRange.Y; line++)
                    {
                        float top = lines[line]._verticalOffset, 
                            bottom = (top - lines[line].UnscaledSize.Y);

                        if (offset >= bottom)
                            return line;
                    }

                    return lineRange.Y;
                }

                /// <summary>
                /// Returns the index of the character on the given line closest to the given offset.
                /// </summary>
                private int GetCharAt(int ln, float offset)
                {
                    float last = 0f;

                    for (int ch = 0; ch < lines[ln].Count; ch++)
                    {
                        Line line = lines[ln];
                        float pos = line.GlyphBoards[ch].bounds.Center.X;

                        if (((offset >= last || ch == 0) && offset < pos) || ch == lines[ln].Count - 1)
                            return ch;

                        last = pos;
                    }

                    return 0;
                }

                /// <summary>
                /// Draws the text board in screen space with an offset given in pixels.
                /// </summary>
                public void Draw(Vector2 origin)
                {
                    Vector2 halfSize = _size * .5f * Scale;
                    BoundingBox2 box = new BoundingBox2(origin - halfSize, origin + halfSize);

                    Draw(box, CroppedBox.defaultMask, HudMain.PixelToWorldRef);
                }

                /// <summary>
                /// Draws the text board in world space on the XY plane of the matrix, facing in the +Z
                /// direction.
                /// </summary>
                public void Draw(Vector2 offset, MatrixD matrix)
                {
                    Vector2 halfSize = _size * .5f * Scale;
                    BoundingBox2 box = new BoundingBox2(offset - halfSize, offset + halfSize);
                    matRef[0] = matrix;

                    Draw(box, CroppedBox.defaultMask, matRef);
                }

                /// <summary>
                /// Draws the text board in world space on the XY plane of the matrix, facing in the +Z
                /// direction.
                /// </summary>
                public void Draw(Vector2 offset, ref MatrixD matrix)
                {
                    Vector2 halfSize = _size * .5f * Scale;
                    BoundingBox2 box = new BoundingBox2(offset - halfSize, offset + halfSize);
                    matRef[0] = matrix;

                    Draw(box, CroppedBox.defaultMask, matRef);
                }

                /// <summary>
                /// Draws the text board in world space on the XY plane of the matrix, facing in the +Z
                /// direction.
                /// </summary>
                public void Draw(BoundingBox2 box, BoundingBox2 mask, ref MatrixD matrix)
                {
                    matRef[0] = matrix;
                    Draw(box, mask, matRef);
                }

                /// <summary>
                /// Draws the text board in world space on the XY plane of the matrix, facing in the +Z
                /// direction.
                /// </summary>
                public void Draw(BoundingBox2 box, BoundingBox2 mask, MatrixD[] matrixRef)
                {
                    ContainmentType containment;
                    mask.Contains(ref box, out containment);

                    // If it's not visible, dont bother
                    if (containment != ContainmentType.Disjoint)
                    {
                        if (AutoResize)
                            _textOffset = Vector2.Zero;                        

                        // Check for changes in position, size or masking
                        if (lastBox != box || lastMask != mask)
                        {
                            isBbCacheStale = true;
                            lastBox = box;
                            lastMask = mask;
                        }

                        if (!AutoResize)
                            UpdateGlyphs();

                        // Update bb data cache
                        if (isBbCacheStale)
                            UpdateBbCache(box, mask);

                        // Draw text from cached bb data
                        BillBoardUtils.AddTriangleData(bbCache, matrixRef);

                        // If self-resizing, then defer glyph updates to allow the rest of the UI
                        // time to catch up
                        if (AutoResize) 
                            UpdateGlyphs();

                        // Invoke update callback
                        if (isUpdateEventPending && (eventTimer.ElapsedTicks / TimeSpan.TicksPerMillisecond) > 500)
                        {
                            TextChanged?.Invoke();
                            eventTimer.Restart();
                            isUpdateEventPending = false;
                        }
                    }
                }

                private void UpdateBbCache(BoundingBox2 box, BoundingBox2 mask)
                {
                    BoundingBox2 textMask = box.Intersect(mask);
                    Vector2 offset = box.Center + _textOffset * Scale;

                    bbCache.Clear();
                    UpdateCharCache(textMask, offset);
                    UpdateUnderlineCache(textMask, offset);

                    if (bbCache.Capacity > 2 * bbCache.Count && bbCache.Count > 20)
                        bbCache.TrimExcess();

                    isBbCacheStale = false;
                }

                /// <summary>
                /// Adds final character quads w/masking and offset to the buffer for rendering
                /// </summary>
                private void UpdateCharCache(BoundingBox2 mask, Vector2 offset)
                {
                    IReadOnlyList<Line> lineList = lines.PooledLines;

                    for (int ln = lineRange.X; ln <= lineRange.Y && ln < lines.Count; ln++)
                    {
                        BillBoardUtils.GetTriangleData(lineList[ln].GlyphBoards, bbCache, mask, offset, Scale);
                    }
                }
                
                /// <summary>
                /// Adds final underline quads w/masking and offset to the buffer for rendering
                /// </summary>
                private void UpdateUnderlineCache(BoundingBox2 mask, Vector2 offset)
                {
                    ContainmentType containment;
                    QuadBoard underlineBoard = QuadBoard.Default;
                    CroppedBox bb = default(CroppedBox);
                    bb.mask = mask;

                    for (int n = 0; n < underlines.Count; n++)
                    {
                        Vector2 halfSize = underlines[n].size * Scale * .5f,
                            pos = offset + underlines[n].offset * Scale;

                        bb.bounds = new BoundingBox2(pos - halfSize, pos + halfSize);
                        bb.mask.Value.Contains(ref bb.bounds, out containment);
                        underlineBoard.materialData.bbColor = underlines[n].color;

                        if (containment != ContainmentType.Disjoint)
                            BillBoardUtils.GetTriangleData(ref underlineBoard, ref bb, bbCache);
                    }
                }

                /// <summary>
                /// Called when the text builder is updated
                /// </summary>
                protected override void AfterTextUpdate()
                {
                    isUpdateEventPending = true;
                }

                /// <summary>
                /// Updates character position, visible range and regenerates glyph data as needed.
                /// </summary>
                private void UpdateGlyphs()
                {
                    UpdateLineRange();
                    UpdateGlyphBoards();

                    // Check for external resizing and offset changes
                    if (Vector2.DistanceSquared(lastFixedSize, _fixedSize) > .1f || 
                        Vector2.DistanceSquared(lastTextSize, _textSize) > .1f ||
                        Vector2.DistanceSquared(lastTextOffset, _textOffset) > .1f
                    )
                    {
                        isLineRangeStale = true;
                        isBbCacheStale = true;
                        areOffsetsStale = true;
                    }

                    if (isQuadCacheStale || areOffsetsStale || isLineRangeStale)
                    {
                        UpdateVisibleRange();

                        isLineRangeStale = false;
                        areOffsetsStale = false;
                        isQuadCacheStale = false;
                    }

                    lastTextOffset = _textOffset;
                    lastTextSize = _textSize;
                    lastFixedSize = _fixedSize;
                    LineWrapWidth = _fixedSize.X;
                }

                private void UpdateLineRange()
                {
                    int start = 0,
                        end;

                    _textSize = GetTextSize();

                    if (!AutoResize)
                    {
                        float height = _textOffset.Y;

                        if (VertCenterText)
                            height += MathHelper.Max(0f, _textSize.Y - _size.Y) * .5f;

                        end = -1;

                        for (int line = 0; line < lines.PooledLines.Count; line++)
                        {
                            float lineHeight = lines.PooledLines[line].UnscaledSize.Y;

                            if (height <= lineHeight)
                            {
                                if (end == -1)
                                {
                                    start = line;
                                    end = line;
                                }
                                else if (height > -_fixedSize.Y)
                                {
                                    end = line;
                                }
                                else
                                    break;
                            }

                            height -= lineHeight;
                        }
                    }
                    else
                    {
                        end = lines.Count - 1;
                    }

                    lastLineRange = lineRange;
                    lineRange.X = start;
                    lineRange.Y = end;

                    if (lastLineRange != lineRange)
                        isLineRangeStale = true;
                }

                private void UpdateGlyphBoards()
                {
                    for (int ln = lineRange.X; ln <= lineRange.Y; ln++)
                    {
                        Line line = lines.PooledLines[ln];
                        line.UpdateGlyphBoards();

                        if (line.isQuadCacheStale)
                            isQuadCacheStale = true;

                        if (line.lastIndex != ln)
                            areOffsetsStale = true;

                        line.lastIndex = ln;
                    }
                }

                /// <summary>
                /// Updates the offsets for characters within the visible range of text and updates the
                /// current size of the text box.
                /// </summary>
                private void UpdateVisibleRange()
                {
                    _size = AutoResize ? _textSize : _fixedSize;
                    isBbCacheStale = true;

                    if (lines.Count > 0)
                    {
                        for (int ln = lineRange.X; ln <= lineRange.Y; ln++)
                        {
                            Line line = lines.PooledLines[ln];

                            // Line alignment is determined partly by text board size.
                            // If the textboard is resized, the entire visible range has to
                            // be recalculated.
                            if (areOffsetsStale || line.isQuadCacheStale)
                                UpdateLineOffsets(line);

                            line.isQuadCacheStale = false;
                        }

                        // Underlines are only generated for the range of lines currently visible.
                        // If that range changes, they need to be regenerated.
                        if (isBbCacheStale || isLineRangeStale)
                            UpdateUnderlines();
                    }
                }

                /// <summary>
                /// Calculates the total size of the text, both visible and not.
                /// </summary>
                private Vector2 GetTextSize()
                {
                    Vector2 tSize = new Vector2();

                    if (lines.Count > 0)
                    {
                        foreach (Line line in lines.PooledLines)
                        {
                            line._verticalOffset = -tSize.Y;

                            if (line.UnscaledSize.X > tSize.X)
                                tSize.X = line.UnscaledSize.X;

                            tSize.Y += line.UnscaledSize.Y;
                        }

                        Line lastLine = lines[lines.Count - 1];

                        if (lastLine.Count == 1 && lastLine[0].Ch == '\n')
                            tSize.Y -= lastLine.UnscaledSize.Y;

                        float vAlign = ((VertCenterText || AutoResize) ? tSize.Y : _fixedSize.Y) * .5f;

                        foreach (Line line in lines.PooledLines)
                            line._verticalOffset += vAlign;
                    }

                    return tSize;
                }

                /// <summary>
                /// Updates the position of each character in the given line.
                /// </summary>
                private void UpdateLineOffsets(Line line)
                {
                    float width = 0f,
                        height = line._verticalOffset - GetBaseline(line),
                        xAlign = GetLineAlignment(line);

                    for (int i = 0; i < line.Count; i++)
                    {
                        int right = i, left = i - 1;
                        Vector2 pos = new Vector2(width, height);

                        char ch = line.Chars[right];
                        FormattedGlyph formattedGlyph = line.FormattedGlyphs[right];
                        IFontStyle fontStyle = FontManager.GetFontStyle(formattedGlyph.format.StyleIndex);

                        float textSize = formattedGlyph.format.TextSize,
                            formatScale = textSize * fontStyle.FontScale,
                            // Quick fix for CJK characters in Space Engineers font data
                            cjkOffset = (formattedGlyph.format.StyleIndex.X == 0 && ch >= 0x4E00) ? (-4f * textSize) : 0f;

                        // Kerning adjustment
                        if (left >= 0)
                        {
                            GlyphFormat leftFmt = line.FormattedGlyphs[left].format, rightFmt = formattedGlyph.format;

                            if (leftFmt.StyleIndex == rightFmt.StyleIndex && leftFmt.TextSize == rightFmt.TextSize)
                                pos.X += fontStyle.GetKerningAdjustment(line.Chars[left], ch) * formatScale;
                        }

                        Vector2 chSize = line.FormattedGlyphs[right].chSize,
                            bbSize = line.GlyphBoards[right].bounds.Size;

                        line.SetOffsetAt(right, new Vector2
                        {
                            X = pos.X + bbSize.X * .5f + (formattedGlyph.glyph.leftSideBearing * formatScale) + xAlign,
                            Y = pos.Y - (bbSize.Y * .5f) + (fontStyle.BaseLine * formatScale) + cjkOffset
                        });

                        width = pos.X + chSize.X;
                    }
                }

                /// <summary>
                /// Returns the offset needed for the given line to ensure the given line matches its <see cref="TextAlignment"/>
                /// </summary>
                private float GetLineAlignment(Line line)
                {
                    float offset = 0f;
                    int ch = line.Count > 1 ? 1 : 0;
                    TextAlignment alignment = line.FormattedGlyphs[ch].format.Alignment;

                    if (alignment == TextAlignment.Left)
                        offset = -_size.X * .5f;
                    else if (alignment == TextAlignment.Center)
                        offset = (MathHelper.Max(0f, _textSize.X - _size.X) - line.UnscaledSize.X) * .5f;
                    else if (alignment == TextAlignment.Right)
                        offset = MathHelper.Max(_size.X, _textSize.X) - (_size.X * .5f) - line.UnscaledSize.X;

                    return offset;
                }

                /// <summary>
                /// Calculates the baseline to be shared by each character in the line.
                /// </summary>
                private float GetBaseline(Line line)
                {
                    float baseline = 0f;

                    for (int ch = 0; ch < line.Count; ch++)
                    {
                        FormattedGlyph fmtGlyph = line.FormattedGlyphs[ch];

                        if (fmtGlyph.chSize.Y == line.UnscaledSize.Y)
                        {
                            GlyphFormat format = fmtGlyph.format;
                            IFontStyle fontStyle = FontManager.Fonts[format.StyleIndex.X][format.StyleIndex.Y];

                            baseline = (fontStyle.BaseLine - (fontStyle.Height - fontStyle.BaseLine) * .5f) * (format.TextSize * fontStyle.FontScale);
                            break;
                        }
                    }

                    return baseline;
                }

                /// <summary>
                /// Generates underlines for underlined text inside the visible line range
                /// </summary>
                private void UpdateUnderlines()
                {
                    int visRange = lineRange.Y - lineRange.X;
                    underlines.Clear();
                    underlines.EnsureCapacity(visRange);

                    for (int ln = lineRange.X; ln <= lineRange.Y; ln++)
                    {
                        Line line = lines[ln];

                        if (line.Count > 0)
                        {
                            GlyphFormatMembers? formatData = line.FormattedGlyphs[0].format.Data;
                            int startCh = 0;

                            for (int ch = 0; ch < lines[ln].Count; ch++)
                            {
                                GlyphFormatMembers? nextFormat = null;

                                if (ch != line.Count - 1)
                                    nextFormat = line.FormattedGlyphs[ch + 1].format.Data;

                                bool formatEqual = nextFormat != null 
                                    && formatData.Value.Item1 == nextFormat.Value.Item1
                                    && formatData.Value.Item2 == nextFormat.Value.Item2
                                    && formatData.Value.Item3 == nextFormat.Value.Item3
                                    && formatData.Value.Item4 == nextFormat.Value.Item4;

                                if (!formatEqual)
                                {
                                    if (((FontStyles)formatData.Value.Item3.Y & FontStyles.Underline) > 0)
                                    {
                                        Vector2 startSize = line.FormattedGlyphs[startCh].chSize,
                                            endSize = line.FormattedGlyphs[ch].chSize,
                                            startPos = line.GlyphBoards[startCh].bounds.Center,
                                            endPos = line.GlyphBoards[ch].bounds.Center;

                                        Vector2 pos = new Vector2
                                        (
                                            (startPos.X + endPos.X) * .5f,
                                            endPos.Y - (endSize.Y * .5f - (1f * formatData.Value.Item2))
                                        );

                                        Vector2 size = new Vector2
                                        (
                                            (endPos.X - startPos.X) + (endSize.X + startSize.X) * .5f,
                                            Math.Max((int)formatData.Value.Item2, 1)
                                        );

                                        Vector4 color = formatData.Value.Item4.GetBbColor() * .9f;
                                        underlines.Add(new UnderlineBoard(size, pos, color));
                                    }

                                    startCh = ch;
                                    formatData = nextFormat;
                                }
                            }
                        }
                    }

                    if (visRange > 9 && underlines.Capacity > 3 * underlines.Count && underlines.Capacity > visRange)
                        underlines.TrimExcess();

                    isBbCacheStale = true;
                }

                /// <summary>
                /// General purpose method used to allow the API to access various members not included in this type's
                /// associated tuple.
                /// </summary>
                protected override object GetOrSetMember(object data, int memberEnum)
                {
                    if (memberEnum <= 128)
                        return base.GetOrSetMember(data, memberEnum);
                    else
                    {
                        switch ((TextBoardAccessors)memberEnum)
                        {
                            case TextBoardAccessors.AutoResize:
                                {
                                    if (data == null)
                                        return AutoResize;
                                    else
                                        AutoResize = (bool)data;

                                    break;
                                }
                            case TextBoardAccessors.VertAlign:
                                {
                                    if (data == null)
                                        return VertCenterText;
                                    else
                                        VertCenterText = (bool)data;

                                    break;
                                }
                            case TextBoardAccessors.MoveToChar:
                                MoveToChar((Vector2I)data);
                                break;
                            case TextBoardAccessors.GetCharAtOffset:
                                return GetCharAtOffset((Vector2)data);
                            case TextBoardAccessors.OnTextChanged:
                                {
                                    var args = (MyTuple<bool, Action>)data;

                                    if (args.Item1)
                                        TextChanged += args.Item2;
                                    else
                                        TextChanged -= args.Item2;

                                    break;
                                }
                            case TextBoardAccessors.TextOffset:
                                {
                                    if (data == null)
                                        return TextOffset;
                                    else
                                        TextOffset = (Vector2)data;

                                    break;
                                }
                            case TextBoardAccessors.VisibleLineRange:
                                return new Vector2I(lineRange.X, lineRange.Y);
                        }

                        return null;
                    }
                }

                /// <summary>
                /// Returns a collection of members needed to access this object via the HUD API as a tuple.
                /// </summary>
                public new TextBoardMembers GetApiData()
                {
                    return new TextBoardMembers()
                    {
                        Item1 = base.GetApiData(),
                        Item2 = new FloatProp(() => Scale, x => Scale = x),
                        Item3 = () => Size,
                        Item4 = () => TextSize,
                        Item5 = new Vec2Prop(() => FixedSize, x => FixedSize = x),
                        Item6 = Draw
                    };
                }

                /// <summary>
                /// Returns a collection of members needed to access this object via the HUD API as a tuple.
                /// </summary>
                public TextBoardMembers8 GetApiData8()
                {
                    return new TextBoardMembers8()
                    {
                        Item1 = base.GetApiData(),
                        Item2 = new FloatProp(() => Scale, x => Scale = x),
                        Item3 = () => Size,
                        Item4 = () => TextSize,
                        Item5 = new Vec2Prop(() => FixedSize, x => FixedSize = x),
                        Item6 = Draw
                    };
                }

                private struct UnderlineBoard
                {
                    public Vector2 size;
                    public Vector2 offset;
                    public Vector4 color;

                    public UnderlineBoard(Vector2 size, Vector2 offset, Vector4 color)
                    {
                        this.size = size;
                        this.offset = offset;
                        this.color = color;
                    }
                }
            }
        }

        namespace Rendering.Client
        { }
    }
}