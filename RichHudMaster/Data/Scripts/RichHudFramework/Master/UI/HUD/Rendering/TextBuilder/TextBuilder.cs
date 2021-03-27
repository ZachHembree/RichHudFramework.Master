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
                                AfterTextUpdate();

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
                private bool canReuseFormatting;

                public TextBuilder()
                {
                    lines = new LinePool(this);
                    sbPool = new ObjectPool<StringBuilder>(new StringBuilderPoolPolicy());
                    BuilderMode = TextBuilderModes.Unlined;
                    Format = GlyphFormat.White;
                }

                protected virtual void AfterTextUpdate()
                { }

                protected void SetWrapWidth(float width)
                {
                    if (BuilderMode == TextBuilderModes.Wrapped && (width < wrappedText.MaxLineWidth - 2f || width > wrappedText.MaxLineWidth + 4f))
                    {
                        wrappedText.SetWrapWidth(width);
                        AfterTextUpdate();
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
                public void SetText(StringBuilder text, GlyphFormat format = null)
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
                public void SetText(string text, GlyphFormat format = null)
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
                            empty = GlyphFormat.Empty.data;
                        bool formatEmpty = format.Item1 == empty.Item1
                            && format.Item2 == empty.Item2
                            && format.Item3 == empty.Item3
                            && format.Item4 == empty.Item4;

                        if (formatEmpty)
                            text[n] = new RichStringMembers(text[n].Item1, Format.data);
                    }

                    lastTextData = text as List<RichStringMembers>;

                    if (!IsTextEqual(lastTextData))
                    {
                        Clear();
                        formatter.Append(text);
                        AfterTextUpdate();
                        canReuseFormatting = false;
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
                public void Append(StringBuilder text, GlyphFormat format = null)
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
                public void Append(string text, GlyphFormat format = null)
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
                public void Append(char ch, GlyphFormat format = null)
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
                            empty = GlyphFormat.Empty.data;
                        bool formatEmpty = format.Item1 == empty.Item1
                            && format.Item2 == empty.Item2
                            && format.Item3 == empty.Item3
                            && format.Item4 == empty.Item4;

                        if (formatEmpty)
                            text[n] = new RichStringMembers(text[n].Item1, Format.data);
                    }

                    formatter.Append(text);
                    AfterTextUpdate();
                    canReuseFormatting = false;
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
                public void Insert(StringBuilder text, Vector2I start, GlyphFormat format = null)
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
                public void Insert(string text, Vector2I start, GlyphFormat format = null)
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
                public void Insert(char ch, Vector2I start, GlyphFormat format = null)
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
                            empty = GlyphFormat.Empty.data;
                        bool formatEmpty = format.Item1 == empty.Item1
                            && format.Item2 == empty.Item2
                            && format.Item3 == empty.Item3
                            && format.Item4 == empty.Item4;

                        if (formatEmpty)
                            text[n] = new RichStringMembers(text[n].Item1, Format.data);
                    }

                    formatter.Insert(text, start);
                    AfterTextUpdate();
                    canReuseFormatting = false;
                }

                /// <summary>
                /// Changes the formatting of the entire text to the given format.
                /// </summary>
                public void SetFormatting(GlyphFormat format)
                {
                    if (lines.Count > 0 && lines[lines.Count - 1].Count > 0)
                        SetFormattingData(Vector2I.Zero, new Vector2I(lines.Count - 1, lines[lines.Count - 1].Count - 1), format.data);
                }

                /// <summary>
                /// Changes the formatting for the text within the given range to the given format.
                /// </summary>
                public void SetFormatting(Vector2I start, Vector2I end, GlyphFormat format) =>
                    SetFormattingData(start, end, format.data);

                protected void SetFormattingData(Vector2I start, Vector2I end, GlyphFormatMembers format)
                {
                    bool formatEqual = Format.data.Item1 == format.Item1
                        && Format.data.Item2 == format.Item2
                        && Format.data.Item3 == format.Item3
                        && Format.data.Item4 == format.Item4;

                    Format = new GlyphFormat(format);

                    if (!(canReuseFormatting && formatEqual))
                    {
                        formatter.SetFormatting(start, end, Format);
                        AfterTextUpdate();
                    }
                    
                    canReuseFormatting = true;
                }

                /// <summary>
                /// Returns the contents of the text as <see cref="RichText"/>.
                /// </summary>
                public RichText GetText()
                {
                    if (lines.Count > 0 && lines[lines.Count - 1].Count > 0)
                    {
                        List<RichStringMembers> nextTextData;

                        if (!IsTextEqual(lastTextData))
                        {
                            Vector2I end = new Vector2I(lines.Count - 1, lines[lines.Count - 1].Count - 1);
                            nextTextData = GetTextRangeData(Vector2I.Zero, end);
                        }
                        else
                            nextTextData = lastTextData;

                        if (lastText == null || nextTextData != lastText.apiData)
                            lastText = new RichText(lastText.apiData);

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
                    AfterTextUpdate();
                    canReuseFormatting = false;
                }

                /// <summary>
                /// Clears all existing text.
                /// </summary>
                public void Clear()
                {
                    if (lines.Count > 0)
                    {
                        formatter.Clear();
                        AfterTextUpdate();
                        canReuseFormatting = false;
                    }
                }

                protected bool IsTextEqual(List<RichStringMembers> text)
                {
                    if (IsTextLengthEqual(text))
                    {
                        Vector2I i = Vector2I.Zero;
                        GlyphFormat lastFormat = null;

                        for (int x = 0; x < text.Count; x++)
                        {
                            StringBuilder newChars = text[x].Item1;
                            GlyphFormatMembers newFormat = text[x].Item2;

                            for (int y = 0; y < newChars.Length; y++)
                            {
                                // Compare formatting
                                GlyphFormat currentFormat = lines[i.X].FormattedGlyphs[i.Y].format;

                                if (lastFormat == null || lastFormat != currentFormat)
                                {
                                    bool formatEqual = currentFormat.data.Item1 == newFormat.Item1
                                        && currentFormat.data.Item2 == newFormat.Item2
                                        && currentFormat.data.Item3 == newFormat.Item3
                                        && currentFormat.data.Item4 == newFormat.Item4;

                                    if (formatEqual)
                                        lastFormat = currentFormat;
                                    else
                                        return false;
                                }

                                // Compare text
                                char ch = lines[i.X].Chars[i.Y];

                                if (ch != newChars[y])
                                    return false;

                                lines.TryGetNextIndex(i, out i);
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
                private bool IsTextLengthEqual(List<RichStringMembers> text)
                {
                    int newTextLength = 0, currentLength = 0;

                    for (int n = 0; n < text.Count; n++)
                        newTextLength += text[n].Item1.Length;

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
                                    return Format.data;
                                else
                                    Format = new GlyphFormat((GlyphFormatMembers)data);

                                break;
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
                            return lines[i.X].FormattedGlyphs[i.Y].format.data;
                        case RichCharAccessors.Offset:
                            return lines[i.X].LocData[i.Y].bbOffset * Scale;
                        case RichCharAccessors.Size:
                            return lines[i.X].LocData[i.Y].chSize * Scale;
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