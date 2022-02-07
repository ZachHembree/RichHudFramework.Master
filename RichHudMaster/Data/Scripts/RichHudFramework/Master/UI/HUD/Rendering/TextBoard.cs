using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using VRage;
using VRageMath;
using RichHudFramework.UI.Server;
using GlyphFormatMembers = VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>;

namespace RichHudFramework
{
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
                    set
                    {
                        value /= Scale;

                        if (Math.Abs(_fixedSize.X - value.X) + Math.Abs(_fixedSize.Y - value.Y) > .1f)
                        {
                            offsetsAreStale = true;
                            _fixedSize = value;
                            LineWrapWidth = _fixedSize.X;
                        }
                    }
                }

                /// <summary>
                /// Used to change the position of the text within the text element. AutoResize must be disabled for this to work.
                /// </summary>
                public Vector2 TextOffset
                {
                    get { return _textOffset * Scale; }
                    set
                    {
                        value /= Scale;

                        if (Math.Abs(_textOffset.X - value.X) + Math.Abs(_textOffset.Y - value.Y) > .1f)
                        {
                            lineRangeIsStale = true;
                            _textOffset = value;
                        }
                    }
                }

                /// <summary>
                /// Returns the range of lines visible.
                /// </summary>
                public Vector2I VisibleLineRange => new Vector2I(startLine, endLine);

                /// <summary>
                /// If true, the text board will automatically resize to fit the text.
                /// </summary>
                public bool AutoResize { get; set; }

                /// <summary>
                /// If true, the text will be vertically aligned to the center of the text board.
                /// </summary>
                public bool VertCenterText { get; set; }

                private int startLine, endLine;
                private bool updateEvent, offsetsAreStale, lineRangeIsStale;
                private Vector2 _size, _textSize, _fixedSize, _textOffset;

                private readonly Stopwatch eventTimer;
                private readonly List<UnderlineBoard> underlines;
                private readonly MatrixD[] matRef;

                public TextBoard()
                {
                    Scale = 1f;
                    endLine = -1;
                    AutoResize = true;
                    VertCenterText = true;

                    Format = GlyphFormat.White;
                    _fixedSize = new Vector2(100f);

                    matRef = new MatrixD[1];
                    underlines = new List<UnderlineBoard>();
                    eventTimer = new Stopwatch();
                    eventTimer.Start();
                }

                /// <summary>
                /// Calculates the minimum offset needed to ensure that the character at the specified index
                /// is within the visible range.
                /// </summary>
                public void MoveToChar(Vector2I index)
                {
                    if (!AutoResize)
                    {
                        index.X = MathHelper.Clamp(index.X, 0, lines.Count - 1);
                        index.Y = MathHelper.Clamp(index.Y, 0, lines[index.X].Count - 1);

                        if (index.X < startLine || index.X > endLine)
                        {
                            if (BuilderMode != TextBuilderModes.Unlined)
                                UpdateVerticalOffset(index.X);
                            else
                                _textOffset.Y = 0f;

                            UpdateLineRange();
                        }

                        if (BuilderMode != TextBuilderModes.Wrapped)
                            _textOffset.X = GetCharRangeOffset(index);
                        else
                            _textOffset.X = 0f;
                    }
                }

                /// <summary>
                /// Finds the first line visible in the range that includes the given line index.
                /// </summary>
                private void UpdateVerticalOffset(int index)
                {
                    Line line = lines[index];

                    if (index > endLine) // Scroll down
                    {
                        _textOffset.Y = -line._verticalOffset;
                        _textOffset.Y += (VertCenterText || AutoResize) ? _textSize.Y * .5f : _fixedSize.Y * .5f;
                        _textOffset.Y -= (_fixedSize.Y - line.UnscaledSize.Y);
                    }
                    else if (index < startLine) // Scroll up
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
                    for (int line = startLine; line <= endLine; line++)
                    {
                        float top = lines[line]._verticalOffset, 
                            bottom = (top - lines[line].UnscaledSize.Y);

                        if (offset >= bottom)
                            return line;
                    }

                    return endLine;
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
                public void Draw(BoundingBox2 box, BoundingBox2 mask, MatrixD[] matrix)
                {
                    ContainmentType containment;
                    mask.Contains(ref box, out containment);

                    if (containment != ContainmentType.Disjoint)
                    {
                        if (offsetsAreStale)
                            UpdateOffsets();
                        else if (lineRangeIsStale || (endLine == -1 && lines.Count > 0))
                            UpdateLineRange();

                        if (AutoResize)
                            _textOffset = Vector2.Zero;

                        if (updateEvent && (eventTimer.ElapsedTicks / TimeSpan.TicksPerMillisecond) > 500)
                        {
                            TextChanged?.Invoke();
                            eventTimer.Restart();
                            updateEvent = false;
                        }

                        BoundingBox2 textMask = box.Intersect(mask);
                        Vector2 offset = box.Center + _textOffset * Scale;

                        var quadBuf = BillBoardUtils.PostQuadBuffer;
                        quadBuf.Clear();

                        DrawCharacters(quadBuf, textMask, offset, ref matrix[0]);
                        DrawUnderlines(quadBuf, textMask, offset, ref matrix[0]);
                        BillBoardUtils.AddQuads(quadBuf);
                    }
                }

                /// <summary>
                /// Adds final transformed character quads w/masking and offset to the buffer for rendering
                /// </summary>
                private void DrawCharacters(List<QuadBoardData> quadBuf, BoundingBox2 mask, Vector2 offset, ref MatrixD matrix)
                {
                    ContainmentType containment;
                    IReadOnlyList<Line> lineList = lines.PooledLines;
                    CroppedBox bb = default(CroppedBox);
                    bb.mask = mask;

                    // Draw glyphs
                    for (int ln = startLine; ln <= endLine && ln < lines.Count; ln++)
                    {
                        Line line = lineList[ln];

                        for (int ch = 0; ch < line.Count; ch++)
                        {
                            var qb = line.GlyphBoards[ch];
                            Vector2 halfSize = qb.bounds.Size * Scale * .5f,
                                pos = offset + qb.bounds.Center * Scale;

                            bb.bounds = BoundingBox2.CreateFromHalfExtent(pos, halfSize);
                            bb.mask.Value.Contains(ref bb.bounds, out containment);

                            if (containment == ContainmentType.Contains)
                                quadBuf.Add(qb.quadBoard.GetQuadData(ref bb, ref matrix));
                            else if (containment != ContainmentType.Disjoint)
                                quadBuf.Add(qb.quadBoard.GetCroppedTexData(ref bb, ref matrix));
                        }
                    }
                }
                
                /// <summary>
                /// Adds final transformed underline quads w/masking and offset to the buffer for rendering
                /// </summary>
                private void DrawUnderlines(List<QuadBoardData> quadBuf, BoundingBox2 mask, Vector2 offset, ref MatrixD matrix)
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

                        if (containment == ContainmentType.Contains)
                            quadBuf.Add(underlineBoard.GetQuadData(ref bb, ref matrix));
                        else if (containment != ContainmentType.Disjoint)
                            quadBuf.Add(underlineBoard.GetCroppedData(ref bb, ref matrix));
                    }
                }

                /// <summary>
                /// Called when the text builder is updated
                /// </summary>
                protected override void AfterFullTextUpdate()
                {
                    UpdateOffsets();
                    updateEvent = true;

                    base.AfterFullTextUpdate();
                }

                protected override void AfterColorUpdate()
                {
                    base.AfterColorUpdate();

                    if (lines.Count > 0)
                    {
                        UpdateUnderlines();
                    }
                }

                /// <summary>
                /// Updates the position and range of visible characters
                /// </summary>
                private void UpdateOffsets()
                {
                    Vector2I range = GetLineRange();
                    startLine = range.X;
                    endLine = range.Y;

                    UpdateVisibleRange();

                    offsetsAreStale = false;
                    lineRangeIsStale = false;
                }

                /// <summary>
                /// Updates the range of visible lines and updates character offsets
                /// if the range has changed.
                /// </summary>
                private void UpdateLineRange()
                {
                    if (!AutoResize)
                    {
                        Vector2I range = GetLineRange();

                        if (range.X != startLine || range.Y != endLine)
                        {
                            startLine = range.X;
                            endLine = range.Y;

                            UpdateVisibleRange();
                        }
                    }

                    lineRangeIsStale = false;
                }

                /// <summary>
                /// Updates the offsets for characters within the visible range of text and updates the
                /// current size of the text box.
                /// </summary>
                private void UpdateVisibleRange()
                {
                    _textSize = GetTextSize();
                    _size = AutoResize ? _textSize : _fixedSize;

                    if (lines.Count > 0)
                    {
                        for (int line = startLine; line <= endLine; line++)
                        {   
                            if (lines[line].Count > 0)
                                UpdateLineOffsets(line, lines[line]._verticalOffset);
                        }

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
                        for (int line = 0; line < lines.Count; line++)
                        {
                            lines[line]._verticalOffset = -tSize.Y;

                            if (lines[line].UnscaledSize.X > tSize.X)
                                tSize.X = lines[line].UnscaledSize.X;

                            tSize.Y += lines[line].UnscaledSize.Y;
                        }

                        Line lastLine = lines[lines.Count - 1];

                        if (lastLine.Count == 1 && lastLine[0].Ch == '\n')
                            tSize.Y -= lastLine.UnscaledSize.Y;

                        float vAlign = ((VertCenterText || AutoResize) ? tSize.Y : _fixedSize.Y) * .5f;

                        for (int line = 0; line < lines.Count; line++)
                            lines[line]._verticalOffset += vAlign;
                    }

                    return tSize;
                }

                /// <summary>
                /// Updates the visible range of lines based on the current text offset.
                /// </summary>
                private Vector2I GetLineRange()
                {
                    if (!AutoResize)
                    {
                        float height = _textOffset.Y;

                        if (VertCenterText)
                            height += MathHelper.Max(0f, _textSize.Y - _size.Y) * .5f;

                        int start = 0, end = -1;

                        for (int line = 0; line < lines.Count; line++)
                        {
                            float lineHeight = lines[line].UnscaledSize.Y;

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

                        return new Vector2I(start, end);
                    }
                    else
                        return new Vector2I(0, lines.Count - 1);
                }

                /// <summary>
                /// Updates the position of each character in the given line.
                /// </summary>
                private void UpdateLineOffsets(int line, float height)
                {
                    float width = 0f,
                        xAlign = GetLineAlignment(lines[line]);

                    height -= GetBaseline(line);

                    for (int ch = 0; ch < lines[line].Count; ch++)
                    {
                        width = UpdateCharOffset(lines[line], ch, ch - 1, new Vector2(width, height), xAlign);
                    }
                }

                /// <summary>
                /// Returns the offset needed for the given line to ensure the given line matches its <see cref="TextAlignment"/>
                /// </summary>
                private float GetLineAlignment(Line line)
                {
                    float offset = 0f;
                    int ch = line.Count > 1 ? 1 : 0;
                    TextAlignment alignment = line[ch].Format.Alignment;

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
                private float GetBaseline(int line)
                {
                    float baseline = 0f;

                    for (int ch = 0; ch < lines[line].Count; ch++)
                    {
                        FormattedGlyph fmtGlyph = lines[line].FormattedGlyphs[ch];

                        if (fmtGlyph.chSize.Y == lines[line].UnscaledSize.Y)
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
                /// Updates the position of the right character.
                /// </summary>
                private float UpdateCharOffset(Line line, int right, int left, Vector2 pos, float xAlign)
                {
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

                    return pos.X + chSize.X;
                }

                /// <summary>
                /// Generates underlines for underlined text
                /// </summary>
                private void UpdateUnderlines()
                {
                    int visRange = endLine - startLine;
                    underlines.Clear();
                    underlines.EnsureCapacity(visRange);

                    for (int ln = startLine; ln <= endLine; ln++)
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

                                        Vector4 color = BillBoardUtils.GetBillBoardBoardColor(formatData.Value.Item4) * .9f;
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
                                return new Vector2I(startLine, endLine);
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