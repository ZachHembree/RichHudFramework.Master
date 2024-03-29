﻿using System.Text;
using VRage;
using VRageMath;
using GlyphFormatMembers = VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>;

namespace RichHudFramework
{
    namespace UI
    {
        namespace Rendering.Server
        {
            public abstract partial class TextBuilder
            {
                protected abstract partial class Line
                {
                    /// <summary>
                    /// Wrapper used to facilitate access to rich character members.
                    /// </summary>
                    public struct RichChar : IRichChar
                    {
                        /// <summary>
                        /// The character associated with the <see cref="RichChar"/>
                        /// </summary>
                        public char Ch => line.chars[index];

                        /// <summary>
                        /// If true, then the char indicates a break between two words.
                        /// </summary>
                        public bool IsSeparator => (Ch == ' ' || Ch == '-' || Ch == '_');

                        /// <summary>
                        /// If true, then the char indicates a break between two lines.
                        /// </summary>
                        public bool IsLineBreak => Ch == '\n';

                        /// <summary>
                        /// The glyph associated with the rich char.
                        /// </summary>
                        public Glyph Glyph => line.formattedGlyphs[index].glyph;

                        /// <summary>
                        /// Billboard object used to render the rich char.
                        /// </summary>
                        public QuadBoard GlyphBoard => line.glyphBoards[index].quadBoard;

                        /// <summary>
                        /// Defines the formatting applied to the character.
                        /// </summary>
                        public GlyphFormat Format => line.formattedGlyphs[index].format;

                        /// <summary>
                        /// The size of the character as it appears.
                        /// </summary>
                        public Vector2 Size => line.formattedGlyphs[index].chSize * builder.Scale;

                        /// <summary>
                        /// The actual size of the character as rendered by the billboard.
                        /// </summary>
                        public Vector2 BbSize => line.glyphBoards[index].bounds.Size * builder.Scale;

                        /// <summary>
                        /// The position of the character's center relative to its TextBoard.
                        /// </summary>
                        public Vector2 Offset => line.glyphBoards[index].bounds.Center * builder.Scale;

                        public readonly int index;
                        public readonly Line line;
                        private readonly TextBuilder builder;

                        public RichChar(TextBuilder builder, Line line, int index)
                        {
                            this.builder = builder;
                            this.line = line;
                            this.index = index;
                        }
                    }
                }
            }
        }
    }
}