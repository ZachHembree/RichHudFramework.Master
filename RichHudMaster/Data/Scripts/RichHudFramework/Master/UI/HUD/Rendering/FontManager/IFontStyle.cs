using System.Collections.Generic;

namespace RichHudFramework
{
    namespace UI
    {
        namespace Rendering.Server
        {
            /// <summary>
            /// Style for a given font. Used publicly by the HUD API for rendering text from
            /// the font's sprites.
            /// </summary>
            public interface IFontStyle
            {
                /// <summary>
                /// Gets font the style is registered to.
                /// </summary>
                IFont Font { get; }

                /// <summary>
                /// Gets the <see cref="Glyph"/> associated with the given <see cref="char"/>.
                /// Returns □ (U+25A1) if the requested character is not defined for the style.
                /// </summary>
                Glyph this[char ch] { get; }

                /// <summary>
                /// Char to Glyph dictionary
                /// </summary>
                IReadOnlyDictionary<char, Glyph> Glyphs { get; }

                /// <summary>
                /// Position of the base line starting from the origin
                /// </summary>
                float BaseLine { get; }

                /// <summary>
                /// Glyph scale used to normalize the font size to 12pts (at ~100 DPI)
                /// </summary>
                float FontScale { get; }

                /// <summary>
                /// Line height
                /// </summary>
                float Height { get; }

                /// <summary>
                /// Size of the font as it appears in its textures.
                /// </summary>
                float PtSize { get; }

                float GetKerningAdjustment(char left, char right);
            }
        }
    }
}