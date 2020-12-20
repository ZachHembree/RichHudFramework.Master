using System;
using VRage;
using VRageMath;
using ApiMemberAccessor = System.Func<object, int, object>;
using HudSpaceDelegate = System.Func<VRage.MyTuple<bool, float, VRageMath.MatrixD>>;

namespace RichHudFramework
{
    namespace UI
    {
        using HudUpdateAccessors = MyTuple<
            ushort, // ZOffset
            Func<Vector3D>, // GetOrigin
            Action, // DepthTest
            Action, // HandleInput
            Action<bool>, // BeforeLayout
            Action // BeforeDraw
        >;
        using Client;
        using Server;
        using System.Collections.Generic;

        /// <summary>
        /// HUD node used to replace the standard Pixel to World matrix with an arbitrary
        /// world matrix transform. Typically parented to HudMain.Root.
        /// </summary>
        public class HudSpaceNode : HudNodeBase, IReadOnlyHudSpaceNode
        {
            /// <summary>
            /// Node defining the coordinate space used to render the UI element
            /// </summary>
            public override IReadOnlyHudSpaceNode HudSpace => this;

            /// <summary>
            /// Returns the current draw matrix
            /// </summary>
            public MatrixD PlaneToWorld { get; protected set; }

            /// <summary>
            /// Used to update the current draw matrix. If no delegate is set, the node will default
            /// to the matrix supplied by its parent.
            /// </summary>
            public Func<MatrixD> UpdateMatrixFunc { get; set; }

            /// <summary>
            /// Cursor position on the XY plane defined by the HUD space. Z == dist from screen.
            /// </summary>
            public Vector3 CursorPos { get; protected set; }

            /// <summary>
            /// If set to true, then the cursor will be drawn in the node's HUD space when being captured by thsi node.
            /// True by default.
            /// </summary>
            public bool DrawCursorInHudSpace { get; set; }

            /// <summary>
            /// Delegate used to retrieve current hud space. Used with cursor.
            /// </summary>
            public HudSpaceDelegate GetHudSpaceFunc { get; protected set; }

            /// <summary>
            /// Returns the world space position of the node's origin.
            /// </summary>
            public Func<Vector3D> GetNodeOriginFunc { get; protected set; } 

            public HudSpaceNode(HudParentBase parent = null) : base(parent)
            {
                GetHudSpaceFunc = () => new MyTuple<bool, float, MatrixD>(DrawCursorInHudSpace, Scale, PlaneToWorld);
                DrawCursorInHudSpace = true;
                GetNodeOriginFunc = () => PlaneToWorld.Translation;
            }

            protected override void InputDepth()
            {
                if (Visible)
                {
                    MatrixD worldToPlane = MatrixD.Invert(PlaneToWorld),
                    pixelToWorld = HudMain.PixelToWorld;

                    Vector3D worldPos = HudMain.Cursor.WorldPos;
                    Vector3D.TransformNoProjection(ref worldPos, ref worldToPlane, out worldPos);

                    // I'm not interested in the Z coordinate. That only gives me the distance from the 
                    // XY plane of the node's matrix.
                    worldPos.Z = 0d;
                    Vector3 cursorPos = new Vector3(worldPos.X, worldPos.Y, 0f);

                    // Project worldPos back into screen space to get distance from the screen.
                    Vector3D.TransformNoProjection(ref worldPos, ref pixelToWorld, out worldPos);

                    // X & Y == Cursor position on the XY plane of the node's matrix. Z == dist from 
                    // screen to facilitate depth testing.
                    cursorPos.Z = (float)Math.Abs(worldPos.Z);

                    CursorPos = cursorPos;
                }
            }

            protected override void BeginLayout(bool refresh)
            {
                if (UpdateMatrixFunc != null)
                    PlaneToWorld = UpdateMatrixFunc();

                base.BeginLayout(refresh);
            }

            public override void GetUpdateAccessors(List<HudUpdateAccessors> UpdateActions, byte treeDepth)
            {
                _hudSpace = _parent?.HudSpace;
                fullZOffset = GetFullZOffset(this);

                UpdateActions.EnsureCapacity(UpdateActions.Count + children.Count + 1);
                UpdateActions.Add(new HudUpdateAccessors(0, GetNodeOriginFunc, DepthTestAction, InputAction, LayoutAction, DrawAction));

                treeDepth++;

                for (int n = 0; n < children.Count; n++)
                    children[n].GetUpdateAccessors(UpdateActions, treeDepth);
            }
        }
    }
}
