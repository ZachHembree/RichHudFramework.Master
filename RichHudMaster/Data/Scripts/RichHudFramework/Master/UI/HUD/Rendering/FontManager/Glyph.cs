using System.Collections.Generic;
using VRage;
using VRageMath;
using AtlasMembers = VRage.MyTuple<string, VRageMath.Vector2>;
using GlyphMembers = VRage.MyTuple<int, VRageMath.Vector2, VRageMath.Vector2, float, float>;

namespace RichHudFramework
{
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
                private readonly BoundingBox2 texBounds;

                public Glyph(Material atlas, Vector2 size, Vector2 origin, float aw, float lsb)
                {
                    advanceWidth = aw;
                    leftSideBearing = lsb;

                    matFrame = new MaterialFrame()
                    {
                        Material = new Material(atlas.TextureID, atlas.size, origin, size),
                        Alignment = MaterialAlignment.FitHorizontal,
                    };

                    Vector2 bbSize = matFrame.Material.size;
                    texBounds = matFrame.GetMaterialAlignment(bbSize.X / bbSize.Y);
                    MatFrame = matFrame;
                }

                public QuadBoard GetQuadBoard(GlyphFormat format, Vector4 bbColor)
                {
                    return new QuadBoard
                    {
                        materialData = new BoundedQuadMaterial
                        {
                            textureID = matFrame.Material.TextureID,
                            texBounds = texBounds,
                            bbColor = bbColor,
                        },
                        skewRatio = ((FontStyles)format.Data.Item3.Y & FontStyles.Italic) > 0 ? -.4f : 0f
                    };
                }
            }
        }
    }
}