using RichHudFramework.UI.Rendering;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System;
using VRage;
using GlyphFormatMembers = VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>;

namespace RichHudFramework
{
    using RichStringMembers = MyTuple<StringBuilder, GlyphFormatMembers>;

    namespace UI
    {
        public struct RichTextMin : IEnumerable<RichStringMembers>
        {
            public List<RichStringMembers> apiData;

            public RichTextMin(string text)
            {
                apiData = new List<RichStringMembers>(1);
                apiData.Add(new RichStringMembers(new StringBuilder(text), GlyphFormat.Empty.data));
            }

            public RichTextMin(RichStringMembers text)
            {
                apiData = new List<RichStringMembers>(1);
                apiData.Add(text);
            }

            public RichTextMin(List<RichStringMembers> ApiData)
            {
                this.apiData = ApiData;
            }

            public IEnumerator<RichStringMembers> GetEnumerator() =>
                apiData.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() =>
                apiData.GetEnumerator();

            /// <summary>
            /// Returns the contents of the <see cref="RichText"/> as an unformatted <see cref="string"/>.
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

            public static implicit operator RichTextMin(string text) =>
                new RichTextMin(text);

            public static implicit operator RichTextMin(RichStringMembers text) =>
                new RichTextMin(text);

            public static implicit operator RichTextMin(List<RichStringMembers> text) =>
                new RichTextMin(text);
        }

        /// <summary>
        /// A collection of rich strings. RichStringMembers and <see cref="string"/>s can be implicitly
        /// cast to this type. Collection-initializer syntax can be used with this type.
        /// </summary>
        public class RichText : IEnumerable<RichStringMembers>
        {
            public GlyphFormat defaultFormat;

            private readonly RichTextMin minText;

            public RichText(GlyphFormat defaultFormat = null)
            {
                minText.apiData = new List<RichStringMembers>();
                this.defaultFormat = defaultFormat ?? GlyphFormat.Empty;
            }

            public RichText(RichTextMin minText)
            {
                this.minText = minText;
                this.minText.apiData = new List<RichStringMembers>();
                this.defaultFormat = GlyphFormat.Empty;
            }

            public RichText(List<RichStringMembers> richStrings)
            {
                minText.apiData = richStrings;
                defaultFormat = GlyphFormat.Empty;
            }

            public RichText(RichStringMembers text)
            {
                defaultFormat = new GlyphFormat(text.Item2);
                minText.apiData = new List<RichStringMembers>();
                minText.apiData.Add(text);
            }

            public RichText(string text, GlyphFormat defaultFormat = null)
            {
                if (defaultFormat != null)
                    this.defaultFormat = defaultFormat;
                else
                    this.defaultFormat = GlyphFormat.Empty;

                minText.apiData = new List<RichStringMembers>();
                var format = defaultFormat?.data ?? GlyphFormat.Empty.data;
                minText.apiData.Add(new RichStringMembers(new StringBuilder(text), format));
            }

            public IEnumerator<RichStringMembers> GetEnumerator() =>
                minText.apiData.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() =>
                minText.apiData.GetEnumerator();

            /// <summary>
            /// Adds a <see cref="string"/> to the text using the default format.
            /// </summary>
            public void Add(string text)
            {
                var format = defaultFormat?.data ?? GlyphFormat.Empty.data;
                var richString = new RichStringMembers(new StringBuilder(text), format);
                minText.apiData.Add(richString);
            }

            /// <summary>
            /// Adds a <see cref="RichText"/> to the collection using the formatting specified in the <see cref="RichText"/>.
            /// </summary>
            public void Add(RichText text)
            {
                minText.apiData.AddRange(text.minText.apiData);
            }

            /// <summary>
            /// Adds a <see cref="RichString"/> to the collection using the formatting specified in the <see cref="RichString"/>.
            /// </summary>
            public void Add(RichStringMembers text)
            {
                minText.apiData.Add(text);
            }

            /// <summary>
            /// Adds a <see cref="string"/> using the given <see cref="GlyphFormat"/>.
            /// </summary>
            public void Add(GlyphFormat formatting, string text)
            {
                var format = formatting?.data ?? GlyphFormat.Empty.data;
                var richString = new RichStringMembers(new StringBuilder(text), format);
                minText.apiData.Add(richString);
            }

            /// <summary>
            /// Adds a <see cref="string"/> using the given <see cref="GlyphFormat"/>.
            /// </summary>
            public void Add(string text, GlyphFormat formatting)
            {
                var format = formatting?.data ?? GlyphFormat.Empty.data;
                var richString = new RichStringMembers(new StringBuilder(text), format);
                minText.apiData.Add(richString);
            }

            /// <summary>
            /// Returns the contents of the <see cref="RichText"/> as an unformatted <see cref="string"/>.
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
            /// Adds a <see cref="string"/> to the text using the default format.
            /// </summary>
            public static RichText operator +(RichText left, string right)
            {
                var format = left?.defaultFormat.data ?? GlyphFormat.Empty.data;
                var richString = new RichStringMembers(new StringBuilder(right), format);
                left.minText.apiData.Add(richString);

                return left;
            }

            /// <summary>
            /// Adds a <see cref="RichString"/> to the collection using the formatting specified in the <see cref="RichString"/>.
            /// </summary>
            public static RichText operator +(RichText left, RichStringMembers right)
            {
                left.minText.apiData.Add(right);
                return left;
            }

            /// <summary>
            /// Adds a <see cref="RichText"/> to the collection using the formatting specified in the <see cref="RichText"/>.
            /// </summary>
            public static RichText operator +(RichText left, RichText right)
            {
                left.minText.apiData.AddRange(right.minText.apiData);
                return left;
            }

            public static implicit operator RichText(RichTextMin textData) =>
                new RichText(textData);

            public static implicit operator RichTextMin(RichText textData) =>
                textData.minText;
        }
    }
}