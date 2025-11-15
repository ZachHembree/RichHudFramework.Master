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

                if (lines.Count > 1)
                {
                    CompactUnbrokenLines(0, lines.Count - 1);
                }
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

			public override void RemoveRange(Vector2I start, Vector2I end)
			{
				base.RemoveRange(start, end);

				if (lines.Count == 0 || lines.Count == 1 && lines.PooledLines[0].Count == 0)
					return;

                // Scan for missing breaks to compact lines on removal
                start = ClampIndex(start);
                end = ClampIndex(end);
                CompactUnbrokenLines(start.X, end.X);
			}

            /// <summary>
            /// Compacts lines that lack line breaks
            /// </summary>
            private void CompactUnbrokenLines(int start, int end)
            {
				for (int i = end; i >= start; i--)
				{
					if (i > 0 && (lines.PooledLines[i].Count == 0 || lines.PooledLines[i].Chars[0] != '\n'))
					{
						lines.PooledLines[i - 1].AddRange(lines.PooledLines[i]);
						lines.RemoveAt(i);

						if (lines.PooledLines[i - 1].Count > 0)
							lines.PooledLines[i - 1].UpdateSize();
					}
				}
			}

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