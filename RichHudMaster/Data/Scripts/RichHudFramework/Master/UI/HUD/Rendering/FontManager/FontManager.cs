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
            using FontManagerMembers = MyTuple<
                MyTuple<Func<int, FontMembers>, Func<int>>, // Font List
                Func<FontDefinition, FontMembers?>, // TryAddFont
                Func<string, FontMembers?>, // GetFont
                ApiMemberAccessor
            >;

            /// <summary>
            /// Manages fonts used by the Rich Hud Framework
            /// </summary>
            public static partial class FontManager
            {
                /// <summary>
                /// Retrieves default font for Space Engineers with regular styling.
                /// </summary>
                public static IFontStyle Default => fonts[0][0];

                /// <summary>
                /// Read-only collection of all registered fonts.
                /// </summary>
                public static ReadOnlyCollection<IFont> Fonts { get; }

                private static readonly List<IFont> fonts;

                static FontManager()
                {
                    fonts = new List<IFont>();
                    Fonts = new ReadOnlyCollection<IFont>(fonts);
                }

                /// <summary>
                /// Attempts to register a new font with the given name and point size. Names must be unique.
                /// </summary>
                public static bool TryAddFont(string name, float ptSize)
                {
                    IFont font;
                    return TryAddFont(name, ptSize, out font);
                }

                /// <summary>
                /// Attempts to register a new font with the given name and point size. Names must be unique.
                /// Returns the font created.
                /// </summary>
                public static bool TryAddFont(string name, float ptSize, out IFont font)
                {
                    if (!fonts.Exists(x => x.Name == name))
                    {
                        font = new Font(name, ptSize, fonts.Count);
                        fonts.Add(font);

                        return true;
                    }
                    else
                    {
                        font = null;
                        return false;
                    }
                }

                /// <summary>
                /// Attempts to register a new font using API data.
                /// </summary>
                public static bool TryAddFont(FontDefinition fontData)
                {
                    IFont font;
                    return TryAddFont(fontData, out font);
                }

                /// <summary>
                /// Attempts to register a new font using API data. Returns the font created.
                /// </summary>
                public static bool TryAddFont(FontDefinition fontData, out IFont font)
                {
                    if (!fonts.Exists(x => x.Name == fontData.Item1))
                    {
                        font = new Font(fontData, fonts.Count);
                        fonts.Add(font);

                        return true;
                    }
                    else
                    {
                        font = null;
                        return false;
                    }
                }

                /// <summary>
                /// Used internally to register a new font via the API. Returns font accessors.
                /// </summary>
                private static FontMembers? TryAddApiFont(FontDefinition fontData)
                {
                    IFont font;

                    if (TryAddFont(fontData, out font))
                        return font.GetApiData();
                    else
                        return null;
                }

                /// <summary>
                /// Retrieves the font with the given name.
                /// </summary>
                public static IFont GetFont(string name)
                {
                    name = name.ToLower();

                    for (int n = 0; n < fonts.Count; n++)
                        if (fonts[n].Name.ToLower() == name)
                            return fonts[n];

                    return null;
                }

                /// <summary>
                /// Retrieves the font with the given name.
                /// </summary>
                public static IFont GetFont(int index) =>
                    fonts[index];

                /// <summary>
                /// Retrieves the font with the given name.
                /// </summary>
                public static IFontStyle GetFontStyle(Vector2I index) =>
                    fonts[index.X][index.Y];

                /// <summary>
                /// Retrieves the font style index of the font with the given name and style.
                /// </summary>
                public static Vector2I GetStyleIndex(string name, FontStyleEnum style = FontStyleEnum.Regular)
                {
                    IFontMin font = GetFont(name);
                    return new Vector2I(font.Index, (int)style);
                }

                /// <summary>
                /// Retrieves the API data for the font with the given name.
                /// </summary>
                private static FontMembers? GetApiFont(string name)
                {
                    IFont font = GetFont(name);

                    return font?.GetApiData();
                }

                /// <summary>
                /// Retrieves members needed to access the font manager via the Framework API.
                /// </summary>
                public static FontManagerMembers GetApiData()
                {
                    return new FontManagerMembers()
                    {
                        Item1 = new MyTuple<Func<int, FontMembers>, Func<int>>(x => Fonts[x].GetApiData(), () => Fonts.Count),
                        Item2 = TryAddApiFont,
                        Item3 = GetApiFont
                    };
                }
            }
        }
    }
}