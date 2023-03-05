using System;
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
            public abstract partial class TextBuilder : ITextBuilder
            {
                public IRichChar this[Vector2I index] => lines[index.X][index.Y];

                /// <summary>
                /// Gets the line at the specified index.
                /// </summary>
                public ILine this[int index] => lines[index];

                /// <summary>
                /// Number of lines in the text.
                /// </summary>
                public int Count => lines.Count;

                public abstract float Scale { get; set; }

                /// <summary>
                /// Default text format. Applied to strings added without any other formatting specified.
                /// </summary>
                public GlyphFormat Format { get; set; }

                /// <summary>
                /// Gets or sets the maximum line width before text will wrap to the next line. Word wrapping must be enabled for
                /// this to apply.
                /// </summary>
                public float LineWrapWidth { get { return wrapWidth; } set { wrapWidth = value; SetWrapWidth(value); } }

                /// <summary>
                /// Determines the formatting mode of the text.
                /// </summary>
                public TextBuilderModes BuilderMode
                {
                    get { return builderMode; }
                    set
                    {
                        if (value != builderMode)
                        {
                            FormattedTextBase newFormatter = null;

                            if (value == TextBuilderModes.Unlined)
                            {
                                wrappedText = null;
                                newFormatter = new UnlinedText(lines);
                            }
                            else if (value == TextBuilderModes.Lined)
                            {
                                wrappedText = null;
                                newFormatter = new LinedText(lines);
                            }
                            else if (value == TextBuilderModes.Wrapped)
                            {
                                wrappedText = new WrappedText(lines);
                                wrappedText.SetWrapWidth(wrapWidth);
                                newFormatter = wrappedText;
                            }

                            if (formatter != null)
                                AfterFullTextUpdate();

                            formatter = newFormatter;
                            builderMode = value;
                        }
                    }
                }

                protected readonly LinePool lines;
                protected TextBuilderModes builderMode;

                private FormattedTextBase formatter;
                private WrappedText wrappedText;
                private float wrapWidth;

                private readonly ObjectPool<StringBuilder> sbPool;
                private RichText lastText;
                private List<RichStringMembers> lastTextData;

                public TextBuilder()
                {
                    lines = new LinePool(this);
                    sbPool = new ObjectPool<StringBuilder>(new StringBuilderPoolPolicy());
                    BuilderMode = TextBuilderModes.Unlined;
                    Format = GlyphFormat.White;
                }

                protected virtual void AfterFullTextUpdate()
                { }

                protected virtual void AfterColorUpdate()
                { }

                protected void SetWrapWidth(float width)
                {
                    if (BuilderMode == TextBuilderModes.Wrapped && (width < wrappedText.MaxLineWidth - 2f || width > wrappedText.MaxLineWidth + 4f))
                    {
                        wrappedText.SetWrapWidth(width);
                        AfterFullTextUpdate();
                    }
                }

                /// <summary>
                /// Replaces the current text with the <see cref="RichText"/> given
                /// </summary>
                public void SetText(RichText text)
                {
                    SetData(text.apiData);
                    lastText = text;
                }

                /// <summary>
                /// Clears current text and appends a copy of the <see cref="StringBuilder"/> given.
                /// </summary>
                public void SetText(StringBuilder text, GlyphFormat? format = null)
                {
                    if (lastText == null)
                        lastText = new RichText();

                    lastText.Clear();
                    lastText.Add(text, format ?? Format);
                    SetData(lastText.apiData);
                }

                /// <summary>
                /// Clears current text and appends a copy of the <see cref="string"/> given.
                /// </summary>
                public void SetText(string text, GlyphFormat? format = null)
                {
                    if (lastText == null)
                        lastText = new RichText();

                    lastText.Clear();
                    lastText.Add(text, format ?? Format);
                    SetData(lastText.apiData);
                }

                protected void SetData(IList<RichStringMembers> text)
                {
                    for (int n = 0; n < text.Count; n++)
                    {
                        GlyphFormatMembers format = text[n].Item2,
                            empty = GlyphFormat.Empty.Data;
                        bool formatEmpty = format.Item1 == empty.Item1
                            && format.Item2 == empty.Item2
                            && format.Item3 == empty.Item3
                            && format.Item4 == empty.Item4;

                        if (formatEmpty)
                            text[n] = new RichStringMembers(text[n].Item1, Format.Data);
                        else if (n == 0)
                            Format = new GlyphFormat(text[0].Item2);
                    }

                    lastTextData = text as List<RichStringMembers>;

                    if (!GetIsTextEqual(lastTextData))
                    {
                        Clear();
                        formatter.Append(text);
                        AfterFullTextUpdate();
                    }
                }

                /// <summary>
                /// Appends the given <see cref="RichText"/>
                /// </summary>
                public void Append(RichText text)
                {
                    AppendData(text.apiData);
                }

                /// <summary>
                /// Appends a copy of the text in the <see cref="StringBuilder"/>
                /// </summary>
                public void Append(StringBuilder text, GlyphFormat? format = null)
                {
                    if (lastText == null)
                        lastText = new RichText();

                    lastText.Clear();
                    lastText.Add(text, format ?? Format);
                    AppendData(lastText.apiData);
                }

                /// <summary>
                /// Appends a copy of the <see cref="string"/>
                /// </summary>
                public void Append(string text, GlyphFormat? format = null)
                {
                    if (lastText == null)
                        lastText = new RichText();

                    lastText.Clear();
                    lastText.Add(text, format ?? Format);
                    AppendData(lastText.apiData);
                }

                /// <summary>
                /// Appends the given <see cref="char"/>
                /// </summary>
                public void Append(char ch, GlyphFormat? format = null)
                {
                    if (lastText == null)
                        lastText = new RichText();

                    lastText.Clear();
                    lastText.Add(ch, format ?? Format);
                    AppendData(lastText.apiData);
                }

                protected void AppendData(IList<RichStringMembers> text)
                {
                    for (int n = 0; n < text.Count; n++)
                    {
                        GlyphFormatMembers format = text[n].Item2,
                            empty = GlyphFormat.Empty.Data;
                        bool formatEmpty = format.Item1 == empty.Item1
                            && format.Item2 == empty.Item2
                            && format.Item3 == empty.Item3
                            && format.Item4 == empty.Item4;

                        if (formatEmpty)
                            text[n] = new RichStringMembers(text[n].Item1, Format.Data);
                    }

                    formatter.Append(text);
                    AfterFullTextUpdate();
                }

                /// <summary>
                /// Inserts the given <see cref="RichText"/> starting at the specified starting index
                /// </summary>
                public void Insert(RichText text, Vector2I start)
                {
                    InsertData(text.apiData, start);
                }

                /// <summary>
                /// Inserts a copy of the given <see cref="StringBuilder"/> starting at the specified starting index
                /// </summary>
                public void Insert(StringBuilder text, Vector2I start, GlyphFormat? format = null)
                {
                    if (lastText == null)
                        lastText = new RichText();

                    lastText.Clear();
                    lastText.Add(text, format ?? Format);
                    InsertData(lastText.apiData, start);
                }

                /// <summary>
                /// Inserts a copy of the given <see cref="string"/> starting at the specified starting index
                /// </summary>
                public void Insert(string text, Vector2I start, GlyphFormat? format = null)
                {
                    if (lastText == null)
                        lastText = new RichText();

                    lastText.Clear();
                    lastText.Add(text, format ?? Format);
                    InsertData(lastText.apiData, start);
                }

                /// <summary>
                /// Inserts the given <see cref="char"/> starting at the specified starting index
                /// </summary>
                public void Insert(char ch, Vector2I start, GlyphFormat? format = null)
                {
                    if (lastText == null)
                        lastText = new RichText();

                    lastText.Clear();
                    lastText.Add(ch, format ?? Format);
                    InsertData(lastText.apiData, start);
                }

                protected void InsertData(IList<RichStringMembers> text, Vector2I start)
                {
                    for (int n = 0; n < text.Count; n++)
                    {
                        GlyphFormatMembers format = text[n].Item2,
                            empty = GlyphFormat.Empty.Data;
                        bool formatEmpty = format.Item1 == empty.Item1
                            && format.Item2 == empty.Item2
                            && format.Item3 == empty.Item3
                            && format.Item4 == empty.Item4;

                        if (formatEmpty)
                            text[n] = new RichStringMembers(text[n].Item1, Format.Data);
                    }

                    formatter.Insert(text, start);
                    AfterFullTextUpdate();
                }

                /// <summary>
                /// Changes the formatting of the entire text to the given format.
                /// </summary>
                public void SetFormatting(GlyphFormat format)
                {
                    Format = format;

                    if (lines.Count > 0 && lines[lines.Count - 1].Count > 0)
                        SetFormattingData(Vector2I.Zero, new Vector2I(lines.Count - 1, lines[lines.Count - 1].Count - 1), format.Data);
                }

                /// <summary>
                /// Changes the formatting for the text within the given range to the given format.
                /// </summary>
                public void SetFormatting(Vector2I start, Vector2I end, GlyphFormat format) =>
                    SetFormattingData(start, end, format.Data);

                protected void SetFormattingData(Vector2I start, Vector2I end, GlyphFormatMembers format)
                {
                    bool isOtherEqual, isColorEqual;
                    GetIsFormatEqual(format, start, end, out isOtherEqual, out isColorEqual);

                    if (isOtherEqual && !isColorEqual)
                    {
                        formatter.SetFormatting(start, end, new GlyphFormat(format), true);
                        AfterColorUpdate();
                    }
                    else if (!isOtherEqual)
                    {
                        formatter.SetFormatting(start, end, new GlyphFormat(format), false);
                        AfterFullTextUpdate();
                    }
                }

                /// <summary>
                /// Returns the contents of the text as <see cref="RichText"/>.
                /// </summary>
                public RichText GetText()
                {
                    if (lines.Count > 0 && lines[lines.Count - 1].Count > 0)
                    {
                        List<RichStringMembers> nextTextData;

                        if (!GetIsTextEqual(lastTextData))
                        {
                            Vector2I end = new Vector2I(lines.Count - 1, lines[lines.Count - 1].Count - 1);
                            nextTextData = GetTextRangeData(Vector2I.Zero, end);
                        }
                        else
                            nextTextData = lastTextData;

                        if (lastText == null || nextTextData != lastText.apiData)
                            lastText = new RichText(lastText?.apiData);

                        return lastText;
                    }
                    else
                        return null;
                }

                /// <summary>
                /// Returns the specified range of characters from the text as <see cref="RichText"/>.
                /// </summary>
                public RichText GetTextRange(Vector2I start, Vector2I end) 
                {
                    List<RichStringMembers> nextTextData = GetTextRangeData(start, end);

                    if (nextTextData == lastText.apiData)
                        return lastText;
                    else
                        return new RichText(lastText.apiData);
                }

                private List<RichStringMembers> GetTextRangeData(Vector2I start, Vector2I end)
                {
                    List<RichStringMembers> text = lastTextData ?? new List<RichStringMembers>();
                    sbPool.ReturnRange(text, 0, text.Count);
                    text.Clear();

                    if (end.X > start.X)
                    {
                        lines[start.X].GetRangeString(text, start.Y, lines[start.X].Count - 1);

                        for (int line = start.X + 1; line <= end.X - 1; line++)
                            lines[line].GetRangeString(text, 0, lines[line].Count - 1);

                        lines[end.X].GetRangeString(text, 0, end.Y);
                    }
                    else
                        lines[start.X].GetRangeString(text, start.Y, end.Y);

                    return text;
                }

                public void RemoveAt(Vector2I index) =>
                    RemoveRange(index, index);

                /// <summary>
                /// Removes all text within the specified range.
                /// </summary>
                public void RemoveRange(Vector2I start, Vector2I end)
                {
                    formatter.RemoveRange(start, end);
                    AfterFullTextUpdate();
                }

                /// <summary>
                /// Clears all existing text.
                /// </summary>
                public void Clear()
                {
                    if (lines.Count > 0)
                    {
                        formatter.Clear();
                        AfterFullTextUpdate();
                    }
                }

                /// <summary>
                /// Compares text formatting in the given range to the new formatting given and returns true
                /// if the formatting is equivalent.
                /// </summary>
                protected void GetIsFormatEqual(GlyphFormatMembers newFormat, Vector2I start, Vector2I end, out bool isOtherEqual, out bool isColorEqual)
                {
                    Vector2I i = start;
                    isOtherEqual = false;
                    isColorEqual = true;

                    for (int x = start.X; x < lines.Count; x++)
                    {
                        int chStart = x == start.X ? start.Y : 0;

                        for (int y = chStart; y < lines[x].Count; y++)
                        {
                            // Compare formatting
                            GlyphFormat currentFormat = lines[i.X].FormattedGlyphs[i.Y].format;

                            isOtherEqual = currentFormat.Data.Item1 == newFormat.Item1
                                && currentFormat.Data.Item2 == newFormat.Item2
                                && currentFormat.Data.Item3 == newFormat.Item3;

                            if (currentFormat.Data.Item4 != newFormat.Item4)
                                isColorEqual = false;

                            if (!isOtherEqual)
                                break;

                            // Increment idnex
                            if (i.X < Count && i.Y + 1 < lines[i.X].Count)
                                i.Y++;
                            else if (i.X + 1 < Count)
                                i = new Vector2I(i.X + 1, 0);

                            if (i.X > start.X || i.Y > end.Y)
                                break;
                        }
                    }
                }

                /// <summary>
                /// Returns true if the text supplied is equivalent to the contents of the TextBuilder
                /// </summary>
                protected bool GetIsTextEqual(IReadOnlyList<RichStringMembers> text)
                {
                    if (text != null && GetIsTextLengthEqual(text))
                    {
                        Vector2I i = Vector2I.Zero;

                        for (int x = 0; x < text.Count; x++)
                        {
                            StringBuilder newChars = text[x].Item1;
                            GlyphFormatMembers newFormat = text[x].Item2;

                            for (int y = 0; y < newChars.Length; y++)
                            {
                                if (formatter.AllowSpecialChars || newChars[y] >= ' ')
                                {
                                    // Compare formatting
                                    GlyphFormat currentFormat = lines[i.X].FormattedGlyphs[i.Y].format;

                                    bool formatEqual = currentFormat.Data.Item1 == newFormat.Item1
                                        && currentFormat.Data.Item2 == newFormat.Item2
                                        && currentFormat.Data.Item3 == newFormat.Item3
                                        && currentFormat.Data.Item4 == newFormat.Item4;

                                    if (!formatEqual)
                                        return false;

                                    // Compare text
                                    char ch = lines[i.X].Chars[i.Y];

                                    if (ch != newChars[y])
                                        return false;

                                    // Increment index
                                    if (i.X < Count && i.Y + 1 < lines[i.X].Count)
                                        i.Y++;
                                    else if (i.X + 1 < Count)
                                        i = new Vector2I(i.X + 1, 0);
                                }
                            }
                        }

                        return true;
                    }
                    else
                        return false;
                }

                /// <summary>
                /// Returns true if the text supplied has the same number of characters as
                /// the text builder.
                /// </summary>
                protected bool GetIsTextLengthEqual(IReadOnlyList<RichStringMembers> text)
                {
                    int newTextLength = 0, currentLength = 0;

                    for (int i = 0; i < text.Count; i++)
                    {
                        if (formatter.AllowSpecialChars)
                            newTextLength += text[i].Item1.Length;
                        else
                        {
                            for (int j = 0; j < text[i].Item1.Length; j++)
                            {
                                char ch = text[i].Item1[j];

                                if (ch >= ' ')
                                    newTextLength++;
                            }
                        }
                    }

                    for (int n = 0; n < lines.Count; n++)
                        currentLength += lines[n].Chars.Count;

                    return newTextLength == currentLength;
                }

                /// <summary>
                /// Facilitates general purpose access to TextBuilder members.
                /// </summary>
                protected virtual object GetOrSetMember(object data, int memberEnum)
                {
                    switch ((TextBuilderAccessors)memberEnum)
                    {
                        case TextBuilderAccessors.LineWrapWidth:
                            {
                                if (data == null)
                                    return LineWrapWidth;
                                else
                                    LineWrapWidth = (float)data;

                                break;
                            }
                        case TextBuilderAccessors.BuilderMode:
                            {
                                if (data == null)
                                    return BuilderMode;
                                else
                                    BuilderMode = (TextBuilderModes)data;

                                break;
                            }
                        case TextBuilderAccessors.GetRange:
                            {
                                var range = (MyTuple<Vector2I, Vector2I>)data;
                                return GetTextRangeData(range.Item1, range.Item2);
                            }
                        case TextBuilderAccessors.SetFormatting:
                            {
                                var input = (MyTuple<Vector2I, Vector2I, GlyphFormatMembers>)data;

                                SetFormattingData(input.Item1, input.Item2, input.Item3);
                                break;
                            }
                        case TextBuilderAccessors.RemoveRange:
                            {
                                var range = (MyTuple<Vector2I, Vector2I>)data;

                                RemoveRange(range.Item1, range.Item2);
                                break;
                            }
                        case TextBuilderAccessors.Format:
                            {
                                if (data == null)
                                    return Format.Data;
                                else
                                {
                                    Format = new GlyphFormat((GlyphFormatMembers)data);
                                    break;
                                }
                            }
                        case TextBuilderAccessors.ToString:
                            return ToString();
                    }

                    return null;
                }

                /// <summary>
                /// Facilitates access to Line members.
                /// </summary>
                protected object GetLineMember(int index, int memberEnum)
                {
                    switch ((LineAccessors)memberEnum)
                    {
                        case LineAccessors.Count:
                            return lines[index].Count;
                        case LineAccessors.Size:
                            return lines[index].Size;
                        case LineAccessors.VerticalOffset:
                            return lines[index].VerticalOffset;
                    }

                    return null;
                }

                /// <summary>
                /// Facilitates access to RichChar members.
                /// </summary>
                protected object GetRichCharMember(Vector2I i, int memberEnum)
                {
                    switch ((RichCharAccessors)memberEnum)
                    {
                        case RichCharAccessors.Ch:
                            return lines[i.X].Chars[i.Y];
                        case RichCharAccessors.Format:
                            return lines[i.X].FormattedGlyphs[i.Y].format.Data;
                        case RichCharAccessors.Offset:
                            {
                                if (lines[i.X].GlyphBoards.Count <= i.Y)
                                    return Vector2.Zero;
                                else
                                    return lines[i.X].GlyphBoards[i.Y].bounds.Center * Scale;
                            }
                        case RichCharAccessors.Size:
                            return lines[i.X].FormattedGlyphs[i.Y].chSize * Scale;
                    }

                    return null;
                }

                public TextBuilderMembers GetApiData()
                {
                    return new TextBuilderMembers()
                    {
                        Item1 = new MyTuple<Func<int, int, object>, Func<int>>(GetLineMember, () => lines.Count),
                        Item2 = GetRichCharMember,
                        Item3 = GetOrSetMember,
                        Item4 = InsertData,
                        Item5 = SetData,
                        Item6 = Clear
                    };
                }

                /// <summary>
                /// Returns the contents of the <see cref="ITextBuilder"/> as an unformatted string.
                /// </summary>
                public override string ToString()
                {
                    int charCount = 0;

                    for (int i = 0; i < lines.Count; i++)
                        charCount += lines[i].Chars.Count;

                    StringBuilder sb = new StringBuilder();
                    sb.EnsureCapacity(charCount);

                    for (int i = 0; i < lines.Count; i++)
                    {
                        for (int j = 0; j < lines[i].Chars.Count; j++)
                            sb.Append(lines[i].Chars[j]);
                    }

                    return sb.ToString();
                }
            }
        }
    }
}