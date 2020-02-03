using System;
using System.Collections.Generic;
using System.Text;
using VRage;
using VRageMath;
using GlyphFormatMembers = VRage.MyTuple<VRageMath.Vector2I, int, VRageMath.Color, float>;

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
            Action<RichStringMembers, Vector2I>, // Insert
            Action // Clear
        >;

        namespace Rendering.Server
        {
            using TextBoardMembers = MyTuple<
                TextBuilderMembers,
                FloatProp, // Scale
                Func<Vector2>, // Size
                Func<Vector2>, // TextSize
                Vec2Prop, // FixedSize
                Action<Vector2> // UpdateText, Draw 
            >;

            public class TextBoard : TextBuilder, ITextBoard
            {
                public event Action OnTextChanged;

                /// <summary>
                /// Text size
                /// </summary>
                public override float Scale
                {
                    set
                    {
                        float scale = (value / base.Scale);

                        Size *= scale;
                        TextSize *= scale;
                        FixedSize *= scale;
                        textOffset *= scale;

                        if (base.Scale != value)
                        {
                            base.Scale = value;
                            UpdateOffsets();
                        }
                    }
                }

                /// <summary>
                /// Size of the text box as rendered
                /// </summary>
                public Vector2 Size { get; protected set; }

                /// <summary>
                /// Full text size including any text outside the visible range.
                /// </summary>
                public Vector2 TextSize { get; protected set; }

                /// <summary>
                /// Size of the text box when AutoResize is set to false. Does nothing otherwise.
                /// </summary>
                public Vector2 FixedSize
                {
                    get { return fixedSize; }
                    set
                    {
                        if (fixedSize != value)
                        {
                            fixedSize = value;
                            LineWrapWidth = fixedSize.X;
                            UpdateOffsets();
                        }
                    }
                }

                /// <summary>
                /// Used to change the position of the text within the text element. AutoResize must be disabled for this to work.
                /// </summary>
                public Vector2 TextOffset
                {
                    get { return textOffset; }
                    set
                    {
                        if (!AutoResize)
                        {
                            UpdateLineRange();
                            textOffset = value;
                        }
                    }
                }

                /// <summary>
                /// If true, the text board will automatically resize to fit the text.
                /// </summary>
                public bool AutoResize { get; set; }

                /// <summary>
                /// If true, the text will be vertically aligned to the center of the text board.
                /// </summary>
                public bool VertCenterText { get; set; }

                private int startLine, endLine;
                private bool updateEvent;
                private Vector2 fixedSize, textOffset;
                private readonly Utils.Stopwatch eventTimer;

                public TextBoard()
                {
                    Scale = 1f;
                    endLine = -1;
                    AutoResize = true;
                    VertCenterText = true;

                    Format = GlyphFormat.Black;
                    FixedSize = new Vector2(200f, 200f);

                    eventTimer = new Utils.Stopwatch();
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
                        if (index.X < startLine || index.X > endLine)
                        {
                            if (BuilderMode != TextBuilderModes.Unlined)
                                UpdateVerticalOffset(index.X);
                            else
                                textOffset.Y = 0f;

                            UpdateLineRange();
                        }

                        if (BuilderMode != TextBuilderModes.Wrapped)
                            textOffset.X = GetCharRangeOffset(index);
                        else
                            textOffset.X = 0f;
                    }
                }

                /// <summary>
                /// Finds the first line visible in the range that includes the given line index.
                /// </summary>
                private void UpdateVerticalOffset(int line)
                {
                    if (line > endLine)
                        UpdateLineOffsetFromEnd(line);
                    else if (line < startLine)
                        UpdateLineOffsetFromStart(line);
                }

                /// <summary>
                /// Calculates the vertical text offset given the index of the last visible line.
                /// </summary>
                private void UpdateLineOffsetFromStart(int start)
                {
                    float dist = 0f;

                    for (int line = 0; line < start; line++)
                    {
                        dist += lines[line].Size.Y;
                    }

                    textOffset.Y = dist;
                }

                /// <summary>
                /// Calculates the vertical text offset given the index of the last visible line.
                /// </summary>
                private void UpdateLineOffsetFromEnd(int end)
                {
                    float height = 0f, bottom = 0f;

                    for (int line = end; line >= 0; line--)
                    {
                        if (height <= (FixedSize.Y - lines[line].Size.Y))
                        {
                            height += lines[line].Size.Y;
                        }

                        bottom += lines[line].Size.Y;
                    }

                    textOffset.Y = bottom - height;
                }

                /// <summary>
                /// Calculates the horizontal offset needed to ensure that the character specified is within
                /// the visible range.
                /// </summary>
                private float GetCharRangeOffset(Vector2I index)
                {
                    Line line = lines[index.X];
                    float offset = textOffset.X;

                    if (line.Count > 0)
                    {
                        if (index.Y != 0)
                        {
                            IRichCharFull ch = line[index.Y];
                            float minOffset = -ch.Offset.X + ch.Size.X / 2f - fixedSize.X / 2f,
                                maxOffset = minOffset - ch.Size.X + fixedSize.X;

                            offset = Utils.Math.Clamp(offset, minOffset, maxOffset);
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
                    float height;
                    int line = startLine;

                    if (VertCenterText)
                        height = TextSize.Y / 2f;
                    else
                        height = Size.Y / 2f;

                    height -= lines[0].Size.Y;

                    for (int n = startLine; n <= endLine; n++)
                    {
                        line = n;

                        if (offset <= height && offset > (height - lines[n].Size.Y))
                            break;

                        height -= lines[n].Size.Y;
                    }

                    return line;
                }

                /// <summary>
                /// Returns the index of the character on the given line closest to the given offset.
                /// </summary>
                private int GetCharAt(int line, float offset)
                {
                    float last, next, min = -Size.X / 2f - 2f, max = -min;
                    int ch = 0;

                    for (int n = 0; n < lines[line].Count; n++)
                    {
                        IRichCharFull richChar = lines[line][n];
                        QuadBoard glyphBoard = richChar.GlyphBoard;

                        float
                            xPos = richChar.Offset.X + textOffset.X,
                            edge = lines[line][ch].Size.X / 2f;

                        if ((xPos - edge) >= min && (xPos + edge) <= max)
                        {
                            ch = n;

                            if (n - 1 >= 0) // Make sure the index is in range
                                last = lines[line][n - 1].Offset.X + textOffset.X;
                            else
                                last = float.MinValue;

                            if (n + 1 < lines[line].Count)
                                next = lines[line][n + 1].Offset.X + textOffset.X;
                            else
                                next = float.MaxValue;

                            if (offset > last && offset < next)
                                break;
                        }
                    }

                    return ch;
                }

                public void Draw(Vector2 origin)
                {
                    if (updateEvent && eventTimer.ElapsedMilliseconds > 500)
                    {
                        OnTextChanged?.Invoke();
                        eventTimer.Reset();
                        updateEvent = false;
                    }

                    if (AutoResize)
                        textOffset = Vector2.Zero;

                    float min = -Size.X / 2f - 2f, max = -min;

                    for (int line = startLine; line <= endLine && line < lines.Count; line++)
                    {
                        for (int ch = 0; ch < lines[line].Count; ch++)
                        {
                            IRichCharFull richChar = lines[line][ch];

                            float 
                                xPos = richChar.Offset.X + textOffset.X,
                                edge = lines[line][ch].Size.X / 2f;

                            if ((xPos - edge) >= min && (xPos + edge) <= max)
                            {
                                richChar.GlyphBoard.Draw(richChar.BbSize, origin + richChar.Offset + textOffset);
                            }
                        }
                    }
                }

                protected override void AfterTextUpdate()
                {
                    UpdateOffsets();
                    updateEvent = true;
                }
               
                private void UpdateOffsets()
                {
                    Vector2I range = GetLineRange();

                    startLine = range.X;
                    endLine = range.Y;

                    UpdateVisibleRange();
                }

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
                }

                /// <summary>
                /// Updates the offsets for characters within the visible range of text and updates the
                /// current size of the text box.
                /// </summary>
                private void UpdateVisibleRange()
                {
                    TextSize = GetTextSize();
                    Size = AutoResize ? TextSize : fixedSize;

                    if (lines.Count > 0)
                    {
                        float height;

                        if (VertCenterText)
                            height = TextSize.Y / 2f;
                        else
                            height = Size.Y / 2f;

                        for (int line = 0; line <= endLine; line++)
                        {
                            if (line >= startLine && lines[line].Count > 0)
                            {
                                UpdateLineOffsets(line, height);
                            }

                            height -= lines[line].Size.Y;
                        }
                    }
                }

                /// <summary>
                /// Updates the visible range of lines based on the current text offset.
                /// </summary>
                private Vector2I GetLineRange()
                {
                    if (!AutoResize)
                    {
                        float height = textOffset.Y;

                        int start = 0;
                        int end = -1;

                        for (int line = 0; line < lines.Count; line++)
                        {
                            if (height <= 2f)
                            {
                                if (end == -1)
                                {
                                    start = line;
                                    end = line;
                                }
                                else if (height > -FixedSize.Y + lines[line].Size.Y - 2f)
                                {
                                    end = line;
                                }
                                else
                                    break;
                            }

                            height -= lines[line].Size.Y;
                        }

                        return new Vector2I(start, end);
                    }
                    else
                        return new Vector2I(0, lines.Count - 1);
                }

                /// <summary>
                /// Calculates the current size of the text box.
                /// </summary>
                private Vector2 GetTextSize()
                {
                    float width = 0f, height = 0f;

                    for (int line = 0; line < lines.Count; line++)
                    {
                        if (lines[line].Size.X > width)
                            width = lines[line].Size.X;

                        height += lines[line].Size.Y;
                    }

                    return new Vector2(width, height);
                }

                /// <summary>
                /// Updates the position of each character in the given line.
                /// </summary>
                private void UpdateLineOffsets(int line, float height)
                {
                    IRichCharFull leftChar = null, rightChar;
                    float width = 0f,
                        xAlign = GetLineAlignment(lines[line]);

                    height -= GetBaseline(line);

                    for (int ch = 0; ch < lines[line].Count; ch++)
                    {
                        rightChar = lines[line][ch];
                        width = UpdateCharOffset(rightChar, leftChar, new Vector2(width, height), xAlign);

                        leftChar = rightChar;
                    }
                }

                /// <summary>
                /// Returns the offset needed for the given line to ensure the given line matches its <see cref="TextAlignment"/>
                /// </summary>
                private float GetLineAlignment(Line line)
                {
                    float offset = 0f, lineWidth = Math.Min(line.Size.X, Size.X);
                    TextAlignment alignment = line[0].Format.Alignment; // the first character determines alignment

                    if (alignment == TextAlignment.Left)
                        offset = -Size.X / 2f;
                    else if (alignment == TextAlignment.Center)
                        offset = -lineWidth / 2f;
                    else if (alignment == TextAlignment.Right)
                        offset = (Size.X / 2f) - lineWidth;

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
                        if (lines[line][ch].Size.Y == lines[line].Size.Y)
                        {
                            GlyphFormat format = lines[line][ch].Format;
                            IFontStyle fontStyle = FontManager.Fonts[format.StyleIndex.X][format.StyleIndex.Y];

                            baseline = (fontStyle.BaseLine - (fontStyle.Height - fontStyle.BaseLine) / 2f) * (format.TextSize * fontStyle.FontScale * Scale);
                        }
                    }

                    return baseline.Round();
                }

                /// <summary>
                /// Updates the position of the right character.
                /// </summary>
                private float UpdateCharOffset(IRichCharFull rightChar, IRichCharFull leftChar, Vector2 pos, float xAlign)
                {
                    GlyphFormat format = rightChar.Format;
                    IFontStyle fontStyle = FontManager.Fonts[format.StyleIndex.X][format.StyleIndex.Y];
                    float scale = format.TextSize * fontStyle.FontScale * Scale;

                    if (leftChar != null && CanUseKernings(leftChar.Format, format))
                        pos.X += fontStyle.GetKerningAdjustment(leftChar.Ch, rightChar.Ch) * scale;

                    rightChar.Offset = new Vector2()
                    {
                        X = pos.X + rightChar.BbSize.X / 2f + (rightChar.Glyph.leftSideBearing * scale) + xAlign,
                        Y = pos.Y - (rightChar.BbSize.Y / 2f) + (fontStyle.BaseLine * scale)
                    };

                    pos.X += rightChar.Size.X;
                    return pos.X;
                }

                /// <summary>
                /// Determines whether the formatting of the characters given allows for the use of kerning pairs.
                /// </summary>
                private bool CanUseKernings(GlyphFormat left, GlyphFormat right) =>
                     left.StyleIndex == right.StyleIndex && left.TextSize == right.TextSize;

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
                                        OnTextChanged += args.Item2;
                                    else
                                        OnTextChanged -= args.Item2;

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
            }
        }

        namespace Rendering.Client
        { }
    }
}