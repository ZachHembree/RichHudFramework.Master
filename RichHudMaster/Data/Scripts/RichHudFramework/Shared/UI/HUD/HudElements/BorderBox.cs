using RichHudFramework.UI.Rendering;
using VRageMath;

namespace RichHudFramework.UI
{
    /// <summary>
    /// A textured frame. The default texture is just a plain color.
    /// </summary>
    public class BorderBox : HudElementBase
    {
        /// <summary>
        /// Material applied to the box.
        /// </summary>
        public Material Material { get { return hudBoard.Material; } set { hudBoard.Material = value; } }

        /// <summary>
        /// Determines how the material reacts to changes in element size/aspect ratio.
        /// </summary>
        public MaterialAlignment MatAlignment { get { return hudBoard.MatAlignment; } set { hudBoard.MatAlignment = value; } }

        /// <summary>
        /// Coloring applied to the material.
        /// </summary>
        public Color Color { get { return hudBoard.Color; } set { hudBoard.Color = value; } }

        /// <summary>
        /// Size of the border on all four sides in pixels.
        /// </summary>
        public float Thickness { get { return _thickness * (LocalScale * parentScale); } set { _thickness = value / (LocalScale * parentScale); } }

        private float _thickness;
        protected readonly MatBoard hudBoard;

        public BorderBox(HudParentBase parent) : base(parent)
        {
            hudBoard = new MatBoard();
            Thickness = 1f;
        }

        public BorderBox() : this(null)
        { }

        protected override void Draw()
        {
            if (Color.A > 0)
            {
                var matrix = HudSpace.PlaneToWorld;
                CroppedBox box = default(CroppedBox);
                box.mask = maskingBox;

                float scale = (LocalScale * parentScale),
                    thickness = _thickness * scale, 
                    height = _absoluteHeight * scale, width = _absoluteWidth * scale;

                if (maskingBox != null)
                {
                    if (hudBoard.Material != Material.Default)
                    {
                        // Left
                        box.size = new Vector2(thickness, height);
                        box.pos = cachedPosition + new Vector2((-width + thickness) * .5f, 0f);
                        hudBoard.DrawCroppedTex(ref box, ref matrix);

                        // Top
                        box.size = new Vector2(width, thickness);
                        box.pos = cachedPosition + new Vector2(0f, (height - thickness) * .5f);
                        hudBoard.DrawCroppedTex(ref box, ref matrix);

                        // Right
                        box.size = new Vector2(thickness, height);
                        box.pos = cachedPosition + new Vector2((width - thickness) * .5f, 0f);
                        hudBoard.DrawCroppedTex(ref box, ref matrix);

                        // Bottom
                        box.size = new Vector2(width, thickness);
                        box.pos = cachedPosition + new Vector2(0f, (-height + thickness) * .5f);
                        hudBoard.DrawCroppedTex(ref box, ref matrix);
                    }
                    else
                    {
                        // Left
                        box.size = new Vector2(thickness, height);
                        box.pos = cachedPosition + new Vector2((-width + thickness) * .5f, 0f);
                        hudBoard.DrawCropped(ref box, ref matrix);

                        // Top
                        box.size = new Vector2(width, thickness);
                        box.pos = cachedPosition + new Vector2(0f, (height - thickness) * .5f);
                        hudBoard.DrawCropped(ref box, ref matrix);

                        // Right
                        box.size = new Vector2(thickness, height);
                        box.pos = cachedPosition + new Vector2((width - thickness) * .5f, 0f);
                        hudBoard.DrawCropped(ref box, ref matrix);

                        // Bottom
                        box.size = new Vector2(width, thickness);
                        box.pos = cachedPosition + new Vector2(0f, (-height + thickness) * .5f);
                        hudBoard.DrawCropped(ref box, ref matrix);
                    }
                }
                else
                {
                    // Left
                    box.size = new Vector2(thickness, height);
                    box.pos = cachedPosition + new Vector2((-width + thickness) * .5f, 0f);
                    hudBoard.Draw(ref box, ref matrix);

                    // Top
                    box.size = new Vector2(width, thickness);
                    box.pos = cachedPosition + new Vector2(0f, (height - thickness) * .5f);
                    hudBoard.Draw(ref box, ref matrix);

                    // Right
                    box.size = new Vector2(thickness, height);
                    box.pos = cachedPosition + new Vector2((width - thickness) * .5f, 0f);
                    hudBoard.Draw(ref box, ref matrix);

                    // Bottom
                    box.size = new Vector2(width, thickness);
                    box.pos = cachedPosition + new Vector2(0f, (-height + thickness) * .5f);
                    hudBoard.Draw(ref box, ref matrix);
                }
            }
        }
    }
}
