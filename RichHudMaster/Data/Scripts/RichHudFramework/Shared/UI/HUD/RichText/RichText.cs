using RichHudFramework.UI.Rendering;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using VRage;
using GlyphFormatMembers = VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>;

namespace RichHudFramework
{
    using RichStringMembers = MyTuple<StringBuilder, GlyphFormatMembers>;

    namespace UI
    {
        /// <summary>
        /// Reusable rich text builder
        /// </summary>
        public class RichText : IEnumerable<RichStringMembers>
        {
            /// <summary>
            /// Default text formatting. Applied to strings with no other formatting given.
            /// Optional.
            /// </summary>
            public GlyphFormat defaultFormat;

            private readonly RichTextMin minText;
            private readonly ObjectPool<StringBuilder> sbPool;

            /// <summary>
            /// Initializes an empty RichText object with the given formatting.
            /// </summary>
            public RichText(GlyphFormat defaultFormat = null)
            {
                this.defaultFormat = defaultFormat ?? GlyphFormat.Empty;
                minText.apiData = new List<RichStringMembers>();
                sbPool = new ObjectPool<StringBuilder>(new StringBuilderPoolPolicy());
            }

            /// <summary>
            /// Initializes a new RichText instance backed by the given RichTextMin object.
            /// </summary>
            public RichText(RichTextMin minText)
            {
                this.minText = minText;
                this.minText.apiData = new List<RichStringMembers>();
                defaultFormat = GlyphFormat.Empty;
                sbPool = new ObjectPool<StringBuilder>(new StringBuilderPoolPolicy());
            }

            /// <summary>
            /// Initializes a new RichText object with the given text and formatting.
            /// </summary>
            public RichText(string text, GlyphFormat defaultFormat = null)
            {
                this.defaultFormat = defaultFormat ?? GlyphFormat.Empty;
                minText.apiData = new List<RichStringMembers>();
                sbPool = new ObjectPool<StringBuilder>(new StringBuilderPoolPolicy());

                GlyphFormatMembers format = defaultFormat.data;
                StringBuilder sb = sbPool.Get();
                sb.Append(text);

                minText.apiData.Add(new RichStringMembers(sb, format));
            }

            public IEnumerator<RichStringMembers> GetEnumerator() =>
                minText.apiData.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() =>
                minText.apiData.GetEnumerator();

            /// <summary>
            /// Appends a string to the end of the text. If the formatting given is equivalent to 
            /// that of the last string appended, then it will use the same StringBuilder.
            /// </summary>
            public void Add(string text) =>
                Add(defaultFormat, text);

            /// <summary>
            /// Copies and appends the contents of the given RichText object.
            /// </summary>
            public void Add(RichText text)
            {
                List<RichStringMembers> currentStrings = minText.apiData,
                    newStrings = text.minText.apiData;

                if (newStrings.Count > 0)
                {
                    int index = 0, end = newStrings.Count - 1;

                    // Attempt to use last StringBuilder if the formatting matches
                    if (currentStrings.Count > 0)
                    {
                        int currentEnd = currentStrings.Count - 1;
                        GlyphFormatMembers newFormat = newStrings[0].Item2,
                            endFormat = currentStrings[currentEnd].Item2;
                        bool formatEqual = newFormat.Item1 == endFormat.Item1
                            && newFormat.Item2 == endFormat.Item2
                            && newFormat.Item3 == endFormat.Item3
                            && newFormat.Item4 == endFormat.Item4;

                        if (formatEqual)
                        {
                            StringBuilder sb = currentStrings[currentEnd].Item1,
                                newSb = newStrings[0].Item1;

                            sb.EnsureCapacity(sb.Length + newSb.Length);

                            for (int i = 0; i < newSb.Length; i++)
                                sb.Append(newSb[i]);

                            index++;
                        }
                    }

                    // Copy the remaining text
                    for (int i = index; i <= end; i++)
                    {
                        StringBuilder sb = sbPool.Get(),
                            newSb = newStrings[i].Item1;

                        sb.EnsureCapacity(sb.Length + newSb.Length);
                        currentStrings.Add(new RichStringMembers(sb, newStrings[i].Item2));

                        for (int j = 0; j < newSb.Length; j++)
                            sb.Append(newSb[j]);
                    }
                }
            }

            /// <summary>
            /// Appends a string to the end of the text. If the formatting given is equivalent to 
            /// that of the last string appended, then it will use the same StringBuilder.
            /// </summary>
            public void Add(GlyphFormat newFormat, string text)
            {
                List<RichStringMembers> richStrings = minText.apiData;
                GlyphFormatMembers format = newFormat?.data ?? GlyphFormat.Empty.data;
                int last = richStrings.Count - 1;
                bool formatEqual = false;

                // Test formatting
                if (richStrings.Count > 0)
                {
                    GlyphFormatMembers lastFormat = richStrings[last].Item2;
                    formatEqual = format.Item1 == lastFormat.Item1
                        && format.Item2 == lastFormat.Item2
                        && format.Item3 == lastFormat.Item3
                        && format.Item4 == lastFormat.Item4;
                }

                StringBuilder sb;

                // If format is equal, reuse last StringBuilder
                if (formatEqual)
                    sb = richStrings[last].Item1;
                else
                {
                    sb = sbPool.Get();
                    var richString = new RichStringMembers(sb, format);
                    richStrings.Add(richString);
                }

                sb.Append(text);
            }

            /// <summary>
            /// Appends a string to the end of the text. If the formatting given is equivalent to 
            /// that of the last string appended, then it will use the same StringBuilder.
            /// </summary>
            public void Add(string text, GlyphFormat newFormat) =>
                Add(newFormat, text);

            /// <summary>
            /// Sets the capacity of the StringBuilders and object pool to match their current
            /// lengths.
            /// </summary>
            public void TrimExcess()
            {
                List<RichStringMembers> text = minText.apiData;

                for (int n = 0; n < text.Count; n++)
                    text[n].Item1.Capacity = text[n].Item1.Length;

                sbPool.TrimExcess();
                text.TrimExcess();
            }

            /// <summary>
            /// Clears current text
            /// </summary>
            public void Clear()
            {
                List<RichStringMembers> text = minText.apiData;
                sbPool.ReturnRange(text, 0, text.Count);
                text.Clear();
            }

            /// <summary>
            /// Returns a copy of the contents of the <see cref="RichText"/> as an unformatted 
            /// <see cref="string"/>.
            /// </summary>
            public override string ToString()
            {
                StringBuilder rawText = new StringBuilder();
                List<RichStringMembers> richText = minText.apiData;

                for (int a = 0; a < richText.Count; a++)
                {
                    rawText.EnsureCapacity(rawText.Length + richText[a].Item1.Length);

                    for (int b = 0; b < richText[a].Item1.Length; b++)
                        rawText.Append(richText[a].Item1[b]);
                }

                return rawText.ToString();
            }

            /// <summary>
            /// Appends a string to the end of the left RichText object. If the formatting given 
            /// is equivalent to that of the last string appended, then it will use the same 
            /// StringBuilder.
            /// </summary>>
            public static RichText operator +(RichText left, string right)
            {
                left.Add(right);
                return left;
            }

            /// <summary>
            /// Copies and appends the contents of the right RichText to the left RichText object.
            /// </summary>
            public static RichText operator +(RichText left, RichText right)
            {
                left.Add(right);
                return left;
            }

            public static explicit operator RichText(RichTextMin textData) =>
                new RichText(textData);

            public static implicit operator RichTextMin(RichText textData) =>
                textData.minText;

            protected class StringBuilderPoolPolicy : IPooledObjectPolicy<StringBuilder>
            {
                public StringBuilder GetNewObject()
                {
                    return new StringBuilder();
                }

                public void ResetObject(StringBuilder obj)
                {
                    obj.Clear();
                }

                public void ResetRange(IReadOnlyList<StringBuilder> objects, int index, int count)
                {
                    for (int n = 0; (n < count && (index + n) < objects.Count); n++)
                    {
                        objects[index + n].Clear();
                    }
                }

                public void ResetRange<T2>(IReadOnlyList<MyTuple<StringBuilder, T2>> objects, int index, int count)
                {
                    for (int n = 0; (n < count && (index + n) < objects.Count); n++)
                    {
                        objects[index + n].Item1.Clear();
                    }
                }
            }
        }
    }
}