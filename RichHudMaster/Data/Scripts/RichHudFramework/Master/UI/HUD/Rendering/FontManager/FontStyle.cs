﻿using System;
using System.Collections.Generic;
using VRage;
using VRageMath;
using AtlasMembers = VRage.MyTuple<string, VRageMath.Vector2>;
using GlyphMembers = VRage.MyTuple<int, VRageMath.Vector2, VRageMath.Vector2, float, float>;
using ApiMemberAccessor = System.Func<object, int, object>;

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
            public sealed partial class FontManager
            {
                private partial class Font
                {
                    private class FontStyle : IFontStyle
                    {
                        public Glyph this[char ch]
                        {
                            get
                            {
                                Glyph value;

                                if (!Glyphs.TryGetValue(ch, out value))
                                {
                                    Glyphs.TryGetValue((char)0x25a1, out value);
                                };

                                return value;
                            }
                        }

                        public IFont Font { get; }

                        public float PtSize { get; }

                        public float Height { get; }

                        public float BaseLine { get; }

                        public float FontScale { get; }

                        public IReadOnlyDictionary<char, Glyph> Glyphs { get; }

                        private readonly Dictionary<uint, float> kerningPairs;

                        public FontStyle(Font parent, float height, float baseline, Dictionary<char, Glyph> glyphs, Dictionary<uint, float> kerningPairs)
                        {
                            Font = parent;
                            PtSize = Font.PtSize;
                            FontScale = Font.BaseScale;
                            Height = height;
                            BaseLine = baseline;
                            this.kerningPairs = kerningPairs;
                            Glyphs = glyphs;
                        }

                        /// <summary>
                        /// Returns the required adjustment for a given pair of characters.
                        /// </summary>
                        public float GetKerningAdjustment(char left, char right)
                        {
                            float value;

                            if (kerningPairs.TryGetValue(left + (uint)(right << 16), out value))
                                return value;
                            else
                                return 0f;
                        }
                    }
                }
            }
        }
    }
}