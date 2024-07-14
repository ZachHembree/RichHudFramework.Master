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
            public struct FormattedGlyph
            {
                /// <summary>
                /// Size of the glyph as it appears w/font size applied
                /// </summary>
                public Vector2 chSize;

                /// <summary>
                /// Text formatting applied to the glyph.
                /// </summary>
                public GlyphFormat format;

                /// <summary>
                /// Font and texture data associated with the glyph.
                /// </summary>
                public Glyph glyph;
            }
        }
    }
}