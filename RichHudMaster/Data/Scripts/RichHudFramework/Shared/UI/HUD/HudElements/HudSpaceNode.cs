using System;
using VRage;
using VRageMath;

namespace RichHudFramework
{
    namespace UI
    {
        /// <summary>
        /// HUD node used to replace the standard Pixel to World matrix with an arbitrary
        /// world matrix transform. Typically parented to HudMain.Root.
        /// </summary>
        public class HudSpaceNode : HudNodeBase
        {
            /// <summary>
            /// Custom draw matrix
            /// </summary>
            public MatrixD CustomMatrix { get { return _customMatrix; } set { _customMatrix = value; } }

            private MatrixD _customMatrix;

            public HudSpaceNode(IHudParent parent = null) : base(parent)
            { }

            public override void BeforeDraw(HudLayers layer, ref MatrixD oldMatrix)
            {
                base.BeforeDraw(layer, ref _customMatrix);
            }
        }
    }
}

