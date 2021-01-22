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
        /// Intermediate type used to convert to/from rich text types. You probably shouldn't be
        /// instantiating these directly.
        /// </summary>
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
    }
}