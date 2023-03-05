using System.Text;
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
                        /// Defines the formatting applied to the character.
                        /// </summary>
                        public GlyphFormat Format => line.formattedGlyphs[index].format;

                        /// <summary>
                        /// The size of the character as it appears.
                        /// </summary>
                        public Vector2 Size => line.formattedGlyphs[index].chSize * builder.Scale;

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