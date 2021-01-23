﻿using RichHudFramework.UI.Rendering;
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

            public readonly List<RichStringMembers> apiData;
            private ObjectPool<StringBuilder> sbPool;

            /// <summary>
            /// Initializes an empty RichText object with the given formatting.
            /// </summary>
            public RichText(GlyphFormat defaultFormat = null)
            {
                this.defaultFormat = defaultFormat ?? GlyphFormat.Empty;
                apiData = new List<RichStringMembers>();
            }

            /// <summary>
            /// Initializes a new RichText instance backed by the given List.
            /// </summary>
            public RichText(List<RichStringMembers> apiData)
            {
                this.apiData = apiData;
                defaultFormat = GlyphFormat.Empty;
            }

            /// <summary>
            /// Initializes a new RichText object with the given text and formatting.
            /// </summary>
            public RichText(string text, GlyphFormat defaultFormat = null)
            {
                this.defaultFormat = defaultFormat ?? GlyphFormat.Empty;
                apiData = new List<RichStringMembers>();
                apiData.Add(new RichStringMembers(new StringBuilder(text), this.defaultFormat.data));
            }

            public IEnumerator<RichStringMembers> GetEnumerator() =>
                apiData.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() =>
                apiData.GetEnumerator();

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
                if (sbPool == null)
                    sbPool = new ObjectPool<StringBuilder>(new StringBuilderPoolPolicy());

                List<RichStringMembers> currentStrings = apiData,
                    newStrings = text.apiData;

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
                if (sbPool == null)
                    sbPool = new ObjectPool<StringBuilder>(new StringBuilderPoolPolicy());

                List<RichStringMembers> richStrings = apiData;
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
                if (sbPool == null)
                    sbPool = new ObjectPool<StringBuilder>(new StringBuilderPoolPolicy());

                List<RichStringMembers> text = apiData;

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
                if (sbPool == null)
                    sbPool = new ObjectPool<StringBuilder>(new StringBuilderPoolPolicy());

                List<RichStringMembers> text = apiData;
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
                List<RichStringMembers> richText = apiData;

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
                left.Add(null, right);
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

            public static implicit operator RichText(string text) =>
                new RichText(text);

            public static implicit operator RichText(List<RichStringMembers> text) =>
                new RichText(text);
        }
    }
}