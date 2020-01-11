using System;
using System.Collections.Generic;
using System.Text;
using VRage;
using VRageMath;
using GlyphFormatMembers = VRage.MyTuple<VRageMath.Vector2I, int, VRageMath.Color, float>;

namespace RichHudFramework
{
    using RichStringMembers = MyTuple<StringBuilder, GlyphFormatMembers>;

    namespace UI
    {
        namespace Rendering.Server
        {
            public abstract partial class TextBuilder
            {
                /// <summary>
                /// Contains the information needed to render an individual <see cref="Glyph"/> with a given
                /// <see cref="GlyphFormat"/>.
                /// </summary>
                protected class RichChar : IRichChar
                {
                    public char Ch { get; }
                    public bool IsSeparator => (Ch == ' ' || Ch == '-' || Ch == '_');
                    public bool IsLineBreak => Ch == '\n';

                    public Glyph Glyph { get; private set; }
                    public GlyphFormat Format { get; private set; }

                    public MatBoard GlyphBoard { get; private set; }
                    public Vector2 Size { get; private set; }
                    public Vector2 Offset => GlyphBoard.offset;

                    public RichChar(char ch, GlyphFormat formatting, float scale)
                    {
                        Ch = ch;
                        GlyphBoard = new MatBoard() { MatAlignment = MaterialAlignment.FitHorizontal };
                        SetFormatting(formatting, scale);
                    }

                    public bool IsWordBreak(RichChar right) =>
                        (IsSeparator && !right.IsSeparator);

                    public void SetFormatting(GlyphFormat format, float scale)
                    {
                        Vector2I index = format.StyleIndex;
                        IFontStyle fontStyle = FontManager.Fonts[index.X][index.Y];

                        scale *= format.TextSize * fontStyle.FontScale;
                        Format = format;

                        if (IsLineBreak)
                        {
                            Glyph = fontStyle[' '];
                            Size = new Vector2(0f, fontStyle.Height) * scale;
                        }
                        else
                        {
                            Glyph = fontStyle[Ch];
                            Size = new Vector2(Glyph.advanceWidth, fontStyle.Height) * scale;
                        }

                        GlyphBoard.Color = format.Color;
                        GlyphBoard.Material = Glyph.material;
                        GlyphBoard.Size = Glyph.material.size * scale;
                    }

                    public void Rescale(float scale)
                    {
                        Size *= scale;
                        GlyphBoard.Size *= scale;
                        GlyphBoard.offset *= scale;
                    }
                }
            }
        }
    }
}