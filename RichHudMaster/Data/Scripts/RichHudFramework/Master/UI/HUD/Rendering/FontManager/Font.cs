using System;
using System.Collections.Generic;
using VRage;
using VRageMath;
using ApiMemberAccessor = System.Func<object, int, object>;
using AtlasMembers = VRage.MyTuple<string, VRageMath.Vector2>;
using GlyphMembers = VRage.MyTuple<int, VRageMath.Vector2, VRageMath.Vector2, float, float>;

namespace RichHudFramework
{
    using FontGen;
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
                    public Vector2I Regular { get; }

                    /// <summary>
                    /// Returns the index for the bolded version of this font
                    /// </summary>
                    public Vector2I Bold { get; }

                    /// <summary>
                    /// Returns the index for the italicised version of this font
                    /// </summary>
                    public Vector2I Italic { get; }

                    /// <summary>
                    /// Retruns the index for the underlined version of the font
                    /// </summary>
                    public Vector2I Underline { get; }

                    /// <summary>
                    /// Returns the index for the bold italic version of this font
                    /// </summary>
                    public Vector2I BoldItalic { get; }

                    /// <summary>
                    /// Returns the index for the bold underlined version of this font
                    /// </summary>
                    public Vector2I BoldUnderline { get; }

                    /// <summary>
                    /// Returns the index for the bold italic underline version of this font
                    /// </summary>
                    public Vector2I BoldItalicUnderline { get; }

                    public IReadOnlyList<IFontStyle> AtlasStyles { get; }

                    private readonly FontStyle[] styles;

                    public Font(string name, float ptSize, int index)
                    {
                        Name = name;
                        PtSize = ptSize;
                        Index = index;

                        Regular = new Vector2I(Index, 0);
                        Bold = new Vector2I(Index, 1);
                        Italic = new Vector2I(Index, 2);
                        BoldItalic = new Vector2I(Index, 3);
                        Underline = new Vector2I(Index, 4);
                        BoldUnderline = new Vector2I(Index, 5);
                        BoldItalicUnderline = new Vector2I(Index, 7);

                        BaseScale = 12f / ptSize;
                        styles = new FontStyle[2];
                        AtlasStyles = styles;
                    }

                    /// <summary>
                    /// Initializes a new font using the given API data.
                    /// </summary>
                    public Font(FontDefData fontData, int index) : 
                        this(fontData.Item1, fontData.Item2, index)
                    {
                        var styleData = fontData.Item3;

                        for (int n = 0; n < styleData.Length; n++)
                        {
                            // Check if the style tuple contains valid data (Item4 is the Atlas array)
                            if (styleData[n].Item4 != null)
                                TryAddStyle(styleData[n]);
                        }
                    }

                    /// <summary>
                    /// Initializes a new font using data deserialized from XML.
                    /// </summary>
                    public Font(FontDefinition fontData, int index) : 
                        this(fontData.Name, fontData.Size, index)
                    {
                        List<StyleDefinition> definedStyles = fontData.Styles;

                        for (int i = 0; i < definedStyles.Count; i++)
                        {
                            StyleDefinition styleDef = definedStyles[i];
                            StyleData xmlData = styleDef.FontData;

                            // 1. Direct Material Conversion
                            List<AtlasData> xmlBitmaps = xmlData.Bitmaps;
                            int bitmapCount = xmlBitmaps.Count;
                            Material[] atlases = new Material[bitmapCount];

                            for (int j = 0; j < bitmapCount; j++)
                            {
                                var size = new Vector2(xmlBitmaps[j].Width, xmlBitmaps[j].Height);
                                atlases[j] = new Material(xmlBitmaps[j].Name, size);
                            }

                            // 2. Direct Glyph Dictionary Population
                            List<GlyphData> xmlGlyphs = xmlData.Glyphs;
                            int glyphCount = xmlGlyphs.Count;
                            Dictionary<char, Glyph> glyphs = new Dictionary<char, Glyph>(glyphCount + 3);

                            Glyph spaceGlyph = null;

                            for (int j = 0; j < glyphCount; j++)
                            {
                                var g = xmlGlyphs[j];
                                char ch = g.Ch[0];
                                Vector2 size = new Vector2(g.Width, g.Height),
                                    origin = new Vector2(g.OriginX, g.OriginY);

                                var newGlyph = new Glyph(
                                    atlases[g.BitmapID],
                                    size,
                                    origin,
                                    g.AdvanceWidth,
                                    g.LeftSideBearing
                                );

                                glyphs.Add(ch, newGlyph);

                                if (ch == ' ')
                                    spaceGlyph = newGlyph;
                            }

                            // Add special characters (newline, tab) if space was found
                            if (spaceGlyph != null)
                                AddSpecialChars(glyphs, spaceGlyph, atlases[0]);

                            // 3. Direct Kerning Dictionary Population
                            List<KerningPairData> xmlKernings = xmlData.Kernings;
                            int kernCount = xmlKernings.Count;
                            Dictionary<uint, float> kerningPairs = new Dictionary<uint, float>(kernCount);

                            for (int j = 0; j < kernCount; j++)
                            {
                                var k = xmlKernings[j];
                                uint key = k.Left[0] + (uint)(k.Right[0] << 16);
                                kerningPairs.Add(key, k.Adjust);
                            }

                            // 4. Finalize Style
                            AddStyleInternal(xmlData.Style, xmlData.Height, xmlData.Baseline, glyphs, kerningPairs);
                        }
                    }

                    public bool TryAddStyle(FontStyleData styleData) =>
                        TryAddStyle(styleData.Item1, styleData.Item2, styleData.Item3, styleData.Item4, styleData.Item5, styleData.Item6);

                    /// <summary>
                    /// Adds a style from API Tuple data. 
                    /// </summary>
                    public bool TryAddStyle(int style, float height, float baseLine, AtlasMembers[] atlasData,
                        KeyValuePair<char, GlyphMembers>[] glyphData, KeyValuePair<uint, float>[] kernData)
                    {
                        if (styles[style] != null)
                            return false;

                        // 1. Convert Atlases
                        Material[] atlases = new Material[atlasData.Length];

                        for (int n = 0; n < atlasData.Length; n++)
                            atlases[n] = new Material(atlasData[n].Item1, atlasData[n].Item2);

                        // 2. Convert Glyphs
                        Dictionary<char, Glyph> glyphs = new Dictionary<char, Glyph>(glyphData.Length + 3);
                        Glyph spaceGlyph = null;

                        for (int n = 0; n < glyphData.Length; n++)
                        {
                            char charKey = glyphData[n].Key;

                            // Filter invalid chars if necessary (original code checked >= ' ')
                            if (charKey >= ' ')
                            {
                                GlyphMembers v = glyphData[n].Value;

                                var newGlyph = new Glyph(atlases[v.Item1], v.Item2, v.Item3, v.Item4, v.Item5);
                                glyphs.Add(charKey, newGlyph);

                                if (charKey == ' ')
                                    spaceGlyph = newGlyph;
                            }
                        }

                        if (spaceGlyph != null)
                            AddSpecialChars(glyphs, spaceGlyph, spaceGlyph.MatFrame.Material);

                        // 3. Convert Kernings
                        Dictionary<uint, float> kerningPairs = new Dictionary<uint, float>(kernData.Length);

                        for (int n = 0; n < kernData.Length; n++)
                            kerningPairs.Add(kernData[n].Key, kernData[n].Value);

                        // 4. Finalize
                        AddStyleInternal(style, height, baseLine, glyphs, kerningPairs);

                        return true;
                    }

                    /// <summary>
                    /// Helper to add generated special characters based on Space metrics.
                    /// </summary>
                    private void AddSpecialChars(Dictionary<char, Glyph> glyphs, Glyph space, Material mat)
                    {
                        // Calculate special char metrics
                        Vector2 size = new Vector2(0f, space.texBounds.Size.Y);
                        Vector2 origin = space.texBounds.Center;

                        // Newline
                        glyphs.Add('\n', new Glyph(mat, size, origin, 0f, space.leftSideBearing));

                        // Tab (6 spaces)
                        glyphs.Add('\t', new Glyph(mat, size, origin, 6f * space.advanceWidth, space.leftSideBearing));
                    }

                    /// <summary>
                    /// Assigns the constructed collections to the FontStyle array.
                    /// </summary>
                    private void AddStyleInternal(int styleIndex, float height, float baseLine, Dictionary<char, Glyph> glyphs, Dictionary<uint, float> kerningPairs)
                    {
                        if (styles[styleIndex] == null)
                            styles[styleIndex] = new FontStyle(this, height, baseLine, glyphs, kerningPairs);
                    }

                    public bool IsStyleDefined(FontStyles styleEnum) =>
                        styles[(int)styleEnum & 1] != null;

                    public bool IsStyleDefined(int style) =>
                        styles[style & 1] != null;

                    public Vector2I GetStyleIndex(int style) =>
                        new Vector2I(Index, style);

                    public Vector2I GetStyleIndex(FontStyles style) =>
                        new Vector2I(Index, (int)style);

                    public FontAPIMembers GetApiData()
                    {
                        return new FontAPIMembers()
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