using System.Collections.Generic;
using System.Xml.Serialization;

namespace RichHudFramework.FontGen
{
    [XmlRoot("FontManifest")]
    public class FontManifest
    {
        [XmlElement("Path")]
        public List<string> Paths;
    }

    [XmlRoot("FontDefinition")]
    public class FontDefinition
    {
        [XmlAttribute]
        public string Name;

        [XmlAttribute]
        public float Size;

        [XmlArray("Styles")]
        [XmlArrayItem("Style")]
        public List<StyleDefinition> Styles;

        public FontDefinition() { }

        public FontDefinition(string name, float size)
        {
            Name = name;
            Size = size;
            Styles = new List<StyleDefinition>();
        }
    }

    public class StyleDefinition
    {
        [XmlAttribute]
        public int StyleID;

        [XmlElement("FontData")]
        public StyleData FontData;
    }

    [XmlType("Font")]
    public class StyleData
    {
        [XmlAttribute("Base")]
        public float Baseline;

        [XmlAttribute]
        public float Height;

        [XmlAttribute("Face")]
        public string FaceName;

        [XmlAttribute("Pt")]
        public float PtSize;

        [XmlAttribute]
        public int Style;

        [XmlArray]
        public List<AtlasData> Bitmaps;

        [XmlArray]
        public List<GlyphData> Glyphs;

        [XmlArray]
        public List<KerningPairData> Kernings;

        public StyleData()
        {
            Bitmaps = new List<AtlasData>();
            Glyphs = new List<GlyphData>();
            Kernings = new List<KerningPairData>();
        }
    }

    [XmlType("Atlas")]
    public class AtlasData
    {
        [XmlAttribute]
        public int ID;

        [XmlAttribute]
        public string Name;

        [XmlAttribute]
        public float Width;

        [XmlAttribute]
        public float Height;
    }

    [XmlType("Glyph")]
    public class GlyphData
    {
        [XmlAttribute]
        public string Ch;

        [XmlAttribute("Bm")]
        public int BitmapID;

        [XmlAttribute("X")]
        public float OriginX;

        [XmlAttribute("Y")]
        public float OriginY;

        [XmlAttribute("W")]
        public float Width;

        [XmlAttribute("H")]
        public float Height;

        [XmlAttribute("Aw")]
        public float AdvanceWidth;

        [XmlAttribute("Lsb")]
        public float LeftSideBearing;
    }

    [XmlType("Kern")]
    public class KerningPairData
    {
        [XmlAttribute("L")]
        public string Left;

        [XmlAttribute("R")]
        public string Right;

        [XmlAttribute]
        public float Adjust;
    }
}