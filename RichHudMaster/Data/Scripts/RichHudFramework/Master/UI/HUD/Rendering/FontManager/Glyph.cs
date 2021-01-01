using System;
using System.Collections.Generic;
using VRage;
using VRageMath;
using AtlasMembers = VRage.MyTuple<string, VRageMath.Vector2>;
using GlyphMembers = VRage.MyTuple<int, VRageMath.Vector2, VRageMath.Vector2, float, float>;
using ApiMemberAccessor = System.Func<object, int, object>;

namespace RichHudFramework
{
    using FontMembers = MyTuple<
        string, // Name
        int, // Index
        float, // PtSize
        float, // BaseScale
        Func<int, bool>, // IsStyleDefined
        ApiMemberAccessor
    >;
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
        using FontDefinition = MyTuple<
            string, // Name
            float, // PtSize
            FontStyleDefinition[] // styles
        >;

        namespace Rendering.Server
        {
            /// <summary>
            /// Stores the texture and font data for a given character.
            /// </summary>
            public class Glyph
            {
                public readonly Material material;
                public readonly float advanceWidth, leftSideBearing;

                private readonly MaterialFrame matFrame;

                public Glyph(Material atlas, Vector2 size, Vector2 origin, float aw, float lsb)
                {
                    advanceWidth = aw;
                    leftSideBearing = lsb;
                    material = new Material(atlas.TextureID, atlas.size, origin, size);

                    matFrame = new MaterialFrame()
                    {
                        material = material,
                        alignment = MaterialAlignment.FitHorizontal,
                    };
                }

                public QuadBoard GetQuadBoard(float scale, GlyphFormat format)
                {
                    return new QuadBoard
                    (
                        material.TextureID, 
                        GetMaterialAlignment(material.size * scale), 
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