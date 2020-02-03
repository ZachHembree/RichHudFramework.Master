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
                protected struct GlyphLocData
                {
                    public readonly Vector2 bbSize, chSize, bbOffset;

                    public GlyphLocData(Vector2 bbSize, Vector2 chSize, Vector2 bbOffset = default(Vector2))
                    {
                        this.bbSize = bbSize;
                        this.chSize = chSize;
                        this.bbOffset = bbOffset;
                    }

                    public GlyphLocData SetOffset(Vector2 offset) =>
                        new GlyphLocData(bbSize, chSize, offset);

                    public GlyphLocData Rescale(float scale) =>
                        new GlyphLocData(bbSize * scale, chSize * scale, bbOffset * scale);
                }

                protected struct FormattedGlyph
                {
                    public readonly Glyph glyph;
                    public readonly GlyphFormat format;

                    public FormattedGlyph(Glyph glyph, GlyphFormat format)
                    {
                        this.glyph = glyph;
                        this.format = format;
                    }
                }

                internal interface IRichCharFull : IRichChar
                {
                    Glyph Glyph { get; }
                    QuadBoard GlyphBoard { get; }
                    Vector2 BbSize { get; }
                    Vector2 Offset { get; set; }
                    bool IsLineBreak { get; }
                    bool IsSeparator { get; }

                    bool IsWordBreak(IRichCharFull right);
                }
            }
        }
    }
}