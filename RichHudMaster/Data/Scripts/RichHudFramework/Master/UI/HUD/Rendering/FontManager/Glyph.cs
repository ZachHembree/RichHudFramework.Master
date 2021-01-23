using System;
using System.Collections.Generic;
using VRage;
using VRageMath;
using ApiMemberAccessor = System.Func<object, int, object>;
using AtlasMembers = VRage.MyTuple<string, VRageMath.Vector2>;
using GlyphMembers = VRage.MyTuple<int, VRageMath.Vector2, VRageMath.Vector2, float, float>;

namespace RichHudFramework
{
    using FontStyleDefinition = MyTuple<
        int, // styleID
        float, // height
        float, // baseline
        AtlasMembers[], // atlases
        KeyValuePair<char, GlyphMembers>[], // glyphs
        KeyValuePair<uint, float>[] // kernings
    >;

    namespace UI
    {
        namespace Rendering.Server
        {
            /// <summary>
            /// Stores the texture and font data for a given character.
            /// </summary>
            public class Glyph
            {
                public IReadOnlyMaterialFrame MatFrame { get; }

                public readonly float advanceWidth, leftSideBearing;

                private readonly MaterialFrame matFrame;

                public Glyph(Material atlas, Vector2 size, Vector2 origin, float aw, float lsb)
                {
                    advanceWidth = aw;
                    leftSideBearing = lsb;

                    matFrame = new MaterialFrame()
                    {
                        Material = new Material(atlas.TextureID, atlas.size, origin, size),
                        Alignment = MaterialAlignment.FitHorizontal,
                    };

                    MatFrame = matFrame;
                }

                public Glyph(MaterialFrame matFrame, float aw, float lsb)
                {
                    this.matFrame = matFrame;
                    advanceWidth = aw;
                    leftSideBearing = lsb;
                    MatFrame = matFrame;
                }

                public QuadBoard GetQuadBoard(float scale, GlyphFormat format)
                {
                    return new QuadBoard
                    (
                        matFrame.Material.TextureID,
                        GetMaterialAlignment(matFrame.Material.size * scale),
                        format.Color,
                        (format.FontStyle & FontStyles.Italic) > 0 ? .4f : 0f
                    );
                }

                public FlatQuad GetMaterialAlignment(Vector2 bbSize) =>
                    matFrame.GetMaterialAlignment(bbSize.X / bbSize.Y);
            }
        }
    }
}