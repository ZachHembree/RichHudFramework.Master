using System;
using System.Collections.Generic;
using System.Text;
using VRage;
using VRageMath;
using GlyphFormatMembers = VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>;

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
                    /// <summary>
                    /// Size of the billboard rendering the character.
                    /// </summary>
                    public Vector2 bbSize;

                    /// <summary>
                    /// Size of the character as it appears.
                    /// </summary>
                    public Vector2 chSize;

                    /// <summary>
                    /// Position of the character starting from the center of its TextBoard.
                    /// </summary>
                    public Vector2 bbOffset;

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
                    /// <summary>
                    /// Font and texture data associated with the glyph.
                    /// </summary>
                    public readonly Glyph glyph;

                    /// <summary>
                    /// Text formatting applied to the glyph.
                    /// </summary>
                    public readonly GlyphFormat format;

                    public FormattedGlyph(Glyph glyph, GlyphFormat format)
                    {
                        this.glyph = glyph;
                        this.format = format;
                    }
                }
            }
        }
    }
}