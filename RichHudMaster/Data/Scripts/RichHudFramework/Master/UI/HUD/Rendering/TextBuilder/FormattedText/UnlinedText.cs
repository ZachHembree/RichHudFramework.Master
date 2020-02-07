using System.Collections.Generic;
using System.Text;
using VRage;
using VRageMath;
using GlyphFormatMembers = VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>;

namespace RichHudFramework.UI.Rendering.Server
{
    using RichStringMembers = MyTuple<StringBuilder, GlyphFormatMembers>;

    public partial class TextBuilder
    {
        private class UnlinedText : FormattedTextBase
        {
            public UnlinedText(LinePool lines) : base(lines)
            {
                if (lines.Count > 1)
                    FlattenText();
            }

            /// <summary>
            /// Appends any text after the first line to the first line and removes any line breaks.
            /// </summary>
            private void FlattenText()
            {
                int charCount = 0;

                for (int n = 0; n < Count; n++)
                    charCount += lines[n].Count;

                Line chars = lines.GetNewLine(charCount);

                for (int line = 0; line < Count; line++)
                {
                    for (int ch = 0; ch < lines[line].Count; ch++)
                    {
                        if (lines[line][ch].Ch >= ' ')
                            chars.AddCharFromLine(ch, lines[line]);
                    }
                }

                chars.UpdateSize();

                lines.Clear();
                lines.Add(chars);
            }

            /// <summary>
            /// Inserts text at a given position in the document.
            /// </summary>
            /// <param name="start">X = line; Y = ch</param>
            public override void Insert(IList<RichStringMembers> text, Vector2I start)
            {
                if (lines.Count == 0)
                    lines.AddNewLine();

                start.X = 0;
                start = ClampIndex(start);
                GlyphFormat previous = GetPreviousFormat(start);
                charBuffer.Clear();

                for (int n = 0; n < text.Count; n++)
                    GetRichChars(text[n], charBuffer, previous, Scale, x => x >= ' ');

                lines[0].InsertRange(start.Y, charBuffer);
                lines[0].UpdateSize();
                charBuffer.Clear();
            }

            /// <summary>
            /// Inserts a string starting on a given line at a given position.
            /// </summary>
            /// <param name="start">X = line; Y = ch</param>
            public override void Insert(RichStringMembers text, Vector2I start) =>
                Insert(new RichStringMembers[] { text }, start);
        }
    }
}