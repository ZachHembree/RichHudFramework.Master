using RichHudFramework.Internal;
using System.Collections.Generic;
using System.Text;
using System;
using VRage;
using VRageMath;
using GlyphFormatMembers = VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>;

namespace RichHudFramework.UI.Rendering.Server
{
    using RichStringMembers = MyTuple<StringBuilder, GlyphFormatMembers>;

    public partial class TextBuilder
    {
        private class LinedText : FormattedTextBase
        {
            private readonly List<Line> lineBuf;

            public LinedText(LinePool lines) : base(lines, true)
            {
                lineBuf = new List<Line>();
            }

            /// <summary>
            /// Inserts text at a given position in the document.
            /// </summary>
            /// <param name="index">X = line; Y = ch</param>
            public override void Insert(IList<RichStringMembers> text, Vector2I index)
            {
                index = ClampIndex(index);
                charBuffer.Clear();

                // Prepend immediately preceeding text
                if (lines.Count > 0)
                    charBuffer.AddRange(lines.PooledLines[index.X], 0, index.Y);

                for (int n = 0; n < text.Count; n++)
                    charBuffer.AppendRichString(text[n], AllowSpecialChars);

                // Append succeeding text
                if (lines.Count > 0)
                {
                    charBuffer.AddRange(lines.PooledLines[index.X], index.Y, lines.PooledLines[index.X].Count - index.Y);
                    lines.RemoveAt(index.X);
                }

                GenerateLines();
                InsertLines(index.X);
            }

            /// <summary>
            /// Inserts a string starting on a given line at a given position.
            /// </summary>
            /// <param name="start">X = line; Y = ch</param>
            public override void Insert(RichStringMembers text, Vector2I start) =>
                Insert(new RichStringMembers[] { text }, start);

            /// <summary>
            /// Splits character buffer into multiple lines at line breaks, if any are present
            /// </summary>
            private void GenerateLines()
            {
                lineBuf.TrimExcess();
                lineBuf.Clear();

                for (int n = 0; n < charBuffer.Count; n++)
                {
                    int k = n + 1;

                    // Find line break
                    while (k < charBuffer.Count && charBuffer.Chars[k] != '\n')
                        k++;

                    Line currentLine = lines.GetNewLine();
                    currentLine.AddRange(charBuffer, n, k - n);
                    lineBuf.Add(currentLine);

                    n = k - 1;
                }
            }

            /// <summary>
            /// Inserts new lines at the given index and calculates the size of each line.
            /// </summary>
            private void InsertLines(int start)
            {
                for (int n = 0; n < lineBuf.Count; n++)
                    lineBuf[n].UpdateSize();

                lines.InsertRange(start, lineBuf);
            }
        }
    }
}