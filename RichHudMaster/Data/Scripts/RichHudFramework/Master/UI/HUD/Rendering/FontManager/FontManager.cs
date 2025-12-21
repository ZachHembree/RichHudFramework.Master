using RichHudFramework.UI.FontData;
using System;
using System.Collections.Generic;
using VRage;
using VRage.Game;
using VRageMath;
using ApiMemberAccessor = System.Func<object, int, object>;
using AtlasMembers = VRage.MyTuple<string, VRageMath.Vector2>;
using GlyphMembers = VRage.MyTuple<int, VRageMath.Vector2, VRageMath.Vector2, float, float>;

namespace RichHudFramework
{
    using FontGen;
    using Internal;
    using RichHudFramework.IO;
    using RichHudFramework.Server;
    using Sandbox.ModAPI;
    using FontAPIMembers = MyTuple<
        string, // Name
        int, // Index
        float, // PtSize
        float, // BaseScale
        Func<int, bool>, // IsStyleDefined
        ApiMemberAccessor
    >;
    using FontStyleData = MyTuple<
        int, // styleID
        float, // height
        float, // baseline
        AtlasMembers[], // atlases
        KeyValuePair<char, GlyphMembers>[], // glyphs
        KeyValuePair<uint, float>[] // kernings
    >;

    namespace UI
    {
        using FontDefData = MyTuple<
            string, // Name
            float, // PtSize
            FontStyleData[] // styles
        >;

        namespace Rendering.Server
        {
            using FontManagerAPIMembers = MyTuple<
                MyTuple<Func<int, FontAPIMembers>, Func<int>>, // Font List
                Func<FontDefData, FontAPIMembers?>, // TryAddFont
                Func<string, FontAPIMembers?>, // GetFont
                ApiMemberAccessor
            >;

            /// <summary>
            /// Manages fonts used by the Rich Hud Framework
            /// </summary>
            public sealed partial class FontManager : RichHudComponentBase
            {
                /// <summary>
                /// Retrieves default font for Space Engineers with regular styling.
                /// </summary>
                public static Vector2I Default => Vector2I.Zero;

                /// <summary>
                /// Read-only collection of all registered fonts.
                /// </summary>
                public static IReadOnlyList<IFont> Fonts => Instance._fonts;

                public static FontManager Instance { get; private set; }
                private readonly List<IFont> _fonts;
                private FontManager() : base(false, true)
                {
                    _fonts = new List<IFont>();
                }

                public static void Init()
                {
                    if (Instance == null)
                    {
                        Instance = new FontManager();
                        InitializeFonts();
                    }
                    else if (Instance.Parent == null)
                        Instance.RegisterComponent(RichHudCore.Instance);
                }

                private static void InitializeFonts()
                {
                    TryAddFont(SeFont.GetFontData());
                    TryAddFont(MonoFont.GetFontData());
                    TryAddFont(AbhayaLibreMedium.GetFontData());
                    TryAddFont(BitstreamVeraSans.GetFontData());

                    ExceptionHandler.WriteToLog($"Loading font manifests...", true);
                    List<MyObjectBuilder_Checkpoint.ModItem> modList = RichHudMaster.Instance?.Session?.Mods;

                    foreach (var mod in modList)
                    {
                        const string manifestPath = "Data\\Fonts\\RHFFontManifest.xml";

                        if (MyAPIGateway.Utilities.FileExistsInModLocation(manifestPath, mod))
                        {
                            var manifestIO = new ReadOnlyModFileIO(manifestPath, mod);
                            string xml;
                            KnownException err = manifestIO.TryRead(out xml);
                            FontManifest manifest = null;

                            if (err == null)
                                err = Utils.Xml.TryDeserialize(xml, out manifest);

                            if (err == null && manifest != null)
                            {
                                ExceptionHandler.WriteToLog($"Font Manifest Loaded. Loading {manifest.Paths.Count} fonts from: {mod.Name}");

                                foreach (string path in manifest.Paths)
                                {
                                    var fontFile = new ReadOnlyModFileIO(path, mod);
                                    string fontXml;
                                    KnownException fontErr = fontFile.TryRead(out fontXml);

                                    if (fontErr == null)
                                    {
                                        FontDefinition fontDef;

                                        if (Utils.Xml.TryDeserialize(fontXml, out fontDef) == null)
                                        {
                                            if (TryAddFont(fontDef))
                                                ExceptionHandler.WriteToLog($"Loaded font: {fontDef.Name}");
                                        }
                                    }
                                    else
                                        ExceptionHandler.WriteToLog($"Failed to read font at: {path}. Error: {fontErr.Message}");
                                }
                            }
                        }
                    }
                }

                public override void Close()
                {
                    if (ExceptionHandler.Unloading)
                        Instance = null;
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
                    if (!Instance._fonts.Exists(x => x.Name == name))
                    {
                        font = new Font(name, ptSize, Instance._fonts.Count);
                        Instance._fonts.Add(font);

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
                public static bool TryAddFont(FontDefData fontData)
                {
                    IFont font;
                    return TryAddFont(fontData, out font);
                }

                /// <summary>
                /// Attempts to register a new font using API data. Returns the font created.
                /// </summary>
                public static bool TryAddFont(FontDefData fontData, out IFont font)
                {
                    if (!Instance._fonts.Exists(x => x.Name == fontData.Item1))
                    {
                        font = new Font(fontData, Instance._fonts.Count);
                        Instance._fonts.Add(font);

                        return true;
                    }
                    else
                    {
                        font = null;
                        return false;
                    }
                }

                /// <summary>
                /// Attempts to register a new font using XML data.
                /// </summary>
                public static bool TryAddFont(FontGen.FontDefinition fontData)
                {
                    IFont font;
                    return TryAddFont(fontData, out font);
                }

                /// <summary>
                /// Attempts to register a new font using XML data. Returns the font created.
                /// </summary>
                public static bool TryAddFont(FontDefinition fontData, out IFont font)
                {
                    if (!Instance._fonts.Exists(x => x.Name == fontData.Name))
                    {
                        font = new Font(fontData, Instance._fonts.Count);
                        Instance._fonts.Add(font);

                        return true;
                    }
                    else
                    {
                        font = null;
                        return false;
                    }
                }

                /// <summary>
                /// Used publicly to register a new font via the API. Returns font accessors.
                /// </summary>
                private static FontAPIMembers? TryAddApiFont(FontDefData fontData)
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

                    for (int n = 0; n < Instance._fonts.Count; n++)
                        if (Instance._fonts[n].Name.ToLower() == name)
                            return Instance._fonts[n];

                    return null;
                }

                /// <summary>
                /// Retrieves the font with the given name.
                /// </summary>
                public static IFont GetFont(int index)
                {
                    return Instance._fonts[index];
                }

                /// <summary>
                /// Retrieves the font with the given name.
                /// </summary>
                public static IFontStyle GetFontStyle(Vector2I index)
                {
                    return Instance._fonts[index.X].AtlasStyles[index.Y & 1];
                }

                /// <summary>
                /// Retrieves Glyphs and character sizes for the given range of char and GlyphFormat data, using partially initialized
                /// FormattedGlyphs.
                /// </summary>
                public static void SetFormattedGlyphs(IList<char> chars, IList<FormattedGlyph> formattedGlyphs, int start = 0, int count = -1)
                {
                    if (count == -1)
                        count = formattedGlyphs.Count;

                    for (int i = start; i < count; i++)
                    {
                        if (formattedGlyphs[i].glyph == null)
                        {
                            GlyphFormat format = formattedGlyphs[i].format;
                            Vector2I index = format.Data.Item3;
                            IFontStyle fontStyle = Instance._fonts[index.X].AtlasStyles[index.Y & 1];
                            float fontSize = format.Data.Item2 * fontStyle.FontScale;
                            Glyph glyph = fontStyle[chars[i]];
                            Vector2 glyphSize = new Vector2(glyph.advanceWidth, fontStyle.Height) * fontSize;

                            formattedGlyphs[i] = new FormattedGlyph
                            {
                                chSize = glyphSize,
                                format = format,
                                glyph = glyph
                            };
                        }
                    }
                }

                /// <summary>
                /// Retrieves the font style index of the font with the given name and style.
                /// </summary>
                public static Vector2I GetStyleIndex(string name, FontStyles style = FontStyles.Regular)
                {
                    IFontMin font = GetFont(name);
                    return new Vector2I(font.Index, (int)style);
                }

                /// <summary>
                /// Retrieves the API data for the font with the given name.
                /// </summary>
                private static FontAPIMembers? GetApiFont(string name)
                {
                    IFont font = GetFont(name);
                    return font?.GetApiData();
                }

                /// <summary>
                /// Retrieves members needed to access the font manager via the Framework API.
                /// </summary>
                public static FontManagerAPIMembers GetApiData()
                {
                    return new FontManagerAPIMembers()
                    {
                        Item1 = new MyTuple<Func<int, FontAPIMembers>, Func<int>>(x => Instance._fonts[x].GetApiData(), () => Instance._fonts.Count),
                        Item2 = TryAddApiFont,
                        Item3 = GetApiFont
                    };
                }
            }
        }
    }
}