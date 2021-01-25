using System;
using System.Collections.Generic;
using VRage;
using AtlasMembers = VRage.MyTuple<string, VRageMath.Vector2>;
using GlyphMembers = VRage.MyTuple<int, VRageMath.Vector2, VRageMath.Vector2, float, float>;
using ApiMemberAccessor = System.Func<object, int, object>;
using VRageMath;

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
            public sealed partial class FontManager
            {
                /// <summary>
                /// Defines a collection of font styles.
                /// </summary>
                private partial class Font : IFont
                {
                    /// <summary>
                    /// Retrieves the font style at the given index.
                    /// </summary>
                    public IFontStyle this[int index] => styles[index & 1];

                    // <summary>
                    /// Retrieves the font style at the given index.
                    /// </summary>
                    public IFontStyle this[FontStyles type] => styles[(int)type & 1];

                    /// <summary>
                    /// Font name
                    /// </summary>
                    public string Name { get; }

                    /// <summary>
                    /// Index of the font in the font manager
                    /// </summary>
                    public int Index { get; }

                    /// <summary>
                    /// Font size at which the textures were created.
                    /// </summary>
                    public float PtSize { get; }

                    /// <summary>
                    /// Default scaling applied to font. Used to normalize font size.
                    /// </summary>
                    public float BaseScale { get; }

                    /// <summary>
                    /// Returns the index for this font using regular styling
                    /// </summary>
                    public Vector2I Regular => new Vector2I(Index, 0);

                    /// <summary>
                    /// Returns the index for the bolded version of this font
                    /// </summary>
                    public Vector2I Bold => new Vector2I(Index, 1);

                    /// <summary>
                    /// Returns the index for the italicised version of this font
                    /// </summary>
                    public Vector2I Italic => new Vector2I(Index, 2);

                    /// <summary>
                    /// Returns the index for the bold italic version of this font
                    /// </summary>
                    public Vector2I BoldItalic => new Vector2I(Index, 3);

                    private readonly FontStyle[] styles;

                    /// <summary>
                    /// Initializes a new font with the given name, size and index.
                    /// </summary>
                    public Font(string name, float ptSize, int index)
                    {
                        Name = name;
                        PtSize = ptSize;
                        Index = index;

                        BaseScale = 12f / ptSize;
                        styles = new FontStyle[2];
                    }

                    /// <summary>
                    /// Initializes a new font using the given API data.
                    /// </summary>
                    public Font(FontDefinition fontData, int index) : this(fontData.Item1, fontData.Item2, index)
                    {
                        var styleData = fontData.Item3;

                        for (int n = 0; n < 2; n++)
                        {
                            if (styleData[n].Item4 != null)
                                TryAddStyle(styleData[n]);
                        }
                    }

                    /// <summary>
                    /// Attempts to add a style to the font using a FontStyleDefinition
                    /// </summary>
                    public bool TryAddStyle(FontStyleDefinition styleData) =>
                        TryAddStyle(styleData.Item1, styleData.Item2, styleData.Item3, styleData.Item4, styleData.Item5, styleData.Item6);

                    /// <summary>
                    /// Attempts to add a style to the font
                    /// </summary>
                    public bool TryAddStyle(int style, float height, float baseLine, AtlasMembers[] atlasData, KeyValuePair<char, GlyphMembers>[] glyphData, KeyValuePair<uint, float>[] kernData)
                    {
                        if (styles[style] == null)
                        {
                            Material[] atlases = new Material[atlasData.Length];
                            Dictionary<char, Glyph> glyphs = new Dictionary<char, Glyph>(glyphData.Length);
                            Dictionary<uint, float> kerningPairs = new Dictionary<uint, float>(kernData.Length);
                            GlyphMembers? spaceData = null;

                            for (int n = 0; n < atlasData.Length; n++)
                                atlases[n] = new Material(atlasData[n].Item1, atlasData[n].Item2);

                            for (int n = 0; n < glyphData.Length; n++)
                            {
                                if (glyphData[n].Key >= ' ')
                                {
                                    if (glyphData[n].Key == ' ')
                                        spaceData = glyphData[n].Value;

                                    GlyphMembers v = glyphData[n].Value;
                                    glyphs.Add(glyphData[n].Key, new Glyph(atlases[v.Item1], v.Item2, v.Item3, v.Item4, v.Item5));
                                }
                            }

                            // Add special characters
                            if (spaceData != null)
                            {
                                GlyphMembers v = spaceData.Value;
                                Vector2 size = new Vector2(0f, v.Item2.Y);
                                glyphs.Add('\n', new Glyph(atlases[v.Item1], size, v.Item3, 0f, v.Item5));
                                glyphs.Add('\t', new Glyph(atlases[v.Item1], size, v.Item3, 6f * v.Item4, v.Item5));
                            }

                            for (int n = 0; n < kernData.Length; n++)
                                kerningPairs.Add(kernData[n].Key, kernData[n].Value);

                            styles[style] = new FontStyle(this, height, baseLine, glyphs, kerningPairs);
                            return true;
                        }
                        else
                            return false;
                    }

                    /// <summary>
                    /// Returns true if the font is defined for the given style.
                    /// </summary>
                    public bool IsStyleDefined(FontStyles styleEnum) =>
                        styles[(int)styleEnum & 1] != null;

                    /// <summary>
                    /// Returns true if the font is defined for the given style.
                    /// </summary>
                    public bool IsStyleDefined(int style) =>
                        styles[style & 1] != null;

                    /// <summary>
                    /// Retrieves the full index of the font style
                    /// </summary>
                    public Vector2I GetStyleIndex(int style) =>
                        new Vector2I(Index, style);

                    /// <summary>
                    /// Retrieves the full index of the font style
                    /// </summary>
                    public Vector2I GetStyleIndex(FontStyles style) =>
                        new Vector2I(Index, (int)style);

                    /// <summary>
                    /// Retrieves data needed to interact with IFont types via the Framework API.
                    /// </summary>
                    public FontMembers GetApiData()
                    {
                        return new FontMembers()
                        {
                            Item1 = Name,
                            Item2 = Index,
                            Item3 = PtSize,
                            Item4 = BaseScale,
                            Item5 = IsStyleDefined
                        };
                    }
                }
            }
        }
    }
}