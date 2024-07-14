using System;
using System.Collections.Generic;
using VRage;
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
        namespace Rendering.Server
        {
            /// <summary>
            /// Expanded font interface. Used publicly by the HUD API.
            /// </summary>
            public interface IFont : IFontMin
            {
                /// <summary>
                /// Gets the style for the given font; returns null if the style isn't defined.
                /// </summary>
                IFontStyle this[FontStyles type] { get; }

                /// <summary>
                /// Gets the style for the given font; returns null if the style isn't defined.
                /// </summary>
                IFontStyle this[int index] { get; }

                IReadOnlyList<IFontStyle> AtlasStyles { get; }

                /// <summary>
                /// Attempts to add a style to the font using a FontStyleDefinition
                /// </summary>
                bool TryAddStyle(FontStyleDefinition styleData);

                /// <summary>
                /// Attempts to add a style to the font
                /// </summary>
                bool TryAddStyle(int style, float height, float baseLine, AtlasMembers[] atlasData, KeyValuePair<char, GlyphMembers>[] glyphData, KeyValuePair<uint, float>[] kernData);

                /// <summary>
                /// Retrieves data needed to interact with IFont types via the Framework API.
                /// </summary>
                FontMembers GetApiData();
            }
        }
    }
}