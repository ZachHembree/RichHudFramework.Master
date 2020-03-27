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
        private class LinedText : FormattedTextBase
        {
            public LinedText(LinePool lines) : base(lines)
            { }

            /// <summary>
            /// Inserts text at a given position in the document.
            /// </summary>
            /// <param name="start">X = line; Y = ch</param>
            public override void Insert(IList<RichStringMembers> text, Vector2I start)
            {
                start = ClampIndex(start);
                GlyphFormat previous = GetPreviousFormat(start);
                charBuffer.Clear();

                for (int n = 0; n < text.Count; n++)
                    GetRichChars(text[n], charBuffer, previous, Scale, x => (x >= ' ' || x == '\n'));

                InsertChars(start);
            }

            /// <summary>
            /// Inserts a string starting on a given line at a given position.
            /// </summary>
            /// <param name="start">X = line; Y = ch</param>
            public override void Insert(RichStringMembers text, Vector2I start) =>
                Insert(new RichStringMembers[] { text }, start);

            /// <summary>
            /// Inserts the contents of the character buffer at the index specified.
            /// </summary>
            private void InsertChars(Vector2I splitStart)
            {
                if (lines.Count > 0)
                {
                    for (int y = splitStart.Y; y < lines[splitStart.X].Count; y++)
                        charBuffer.AddCharFromLine(y, lines[splitStart.X]);

                    lines.RemoveAt(splitStart.X);
                }

                List<Line> newLines = GetLines();
                string contents = "";

                for (int n = 0; n < newLines[0].Count; n++)
                    contents += newLines[0][n].Ch;

                InsertLines(newLines, splitStart.X);
            }

            /// <summary>
            /// Generates a list of lines from the contents of the character buffer.
            /// </summary>
            private List<Line> GetLines()
            {
                Line currentLine = null;
                List<Line> newLines = new List<Line>();

                for (int n = 0; n < charBuffer.Count; n++)
                {
                    if (currentLine == null || (charBuffer.Chars[n] == '\n' && currentLine.Count > 0))
                    {
                        currentLine = lines.GetNewLine();
                        newLines.Add(currentLine);
                    }

                    currentLine.AddCharFromLine(n, charBuffer);
                }

                return newLines;
            }

            /// <summary>
            /// Inserts new lines at the given index and calculates the size of each line.
            /// </summary>
            private void InsertLines(List<Line> newLines, int start)
            {
                for (int n = 0; n < newLines.Count; n++)
                    newLines[n].UpdateSize();

                lines.InsertRange(start, newLines);
                charBuffer.Clear();
            }
        }
    }
}