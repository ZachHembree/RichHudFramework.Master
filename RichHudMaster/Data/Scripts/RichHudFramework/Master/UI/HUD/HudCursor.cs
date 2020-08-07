using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage;
using VRage.Utils;
using VRageMath;
using ApiMemberAccessor = System.Func<object, int, object>;
using FloatProp = VRage.MyTuple<System.Func<float>, System.Action<float>>;
using RichStringMembers = VRage.MyTuple<System.Text.StringBuilder, VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>>;
using Vec2Prop = VRage.MyTuple<System.Func<VRageMath.Vector2>, System.Action<VRageMath.Vector2>>;
using HudSpaceDelegate = System.Func<VRage.MyTuple<float, VRageMath.MatrixD>>;

namespace RichHudFramework
{
    using CursorMembers = MyTuple<
        Func<bool>, // Visible
        Func<bool>, // IsCaptured
        Func<Vector2>, // Position
        Func<Vector3D>, // WorldPos
        Action<object, float, HudSpaceDelegate>, // Capture
        MyTuple<
            Func<object, bool>, // IsCapturing
            Func<object, float, HudSpaceDelegate, bool>, // TryCapture
            Func<object, bool>, // TryRelease
            ApiMemberAccessor // GetOrSetMember
        >
    >;

    namespace UI.Server
    {
        using Rendering;

        public sealed partial class HudMain
        {
            /// <summary>
            /// Draws cursor shared by elements in the framework
            /// </summary>
            private sealed class HudCursor : TexturedBox, ICursor
            {
                /// <summary>
                /// The position of the cursor in pixels in screen space
                /// </summary>
                public Vector2 ScreenPos { get; private set; }

                /// <summary>
                /// Position of the cursor in world space.
                /// </summary>
                public Vector3D WorldPos { get; private set; }

                /// <summary>
                /// Indicates whether the cursor is currently visible
                /// </summary>
                public override bool Visible { get { return base.Visible && MyAPIGateway.Gui.ChatEntryVisible; } }

                /// <summary>
                /// Returns true if the cursor has been captured by a UI element
                /// </summary>
                public bool IsCaptured => CapturedElement != null;

                /// <summary>
                /// Returns the object capturing the cursor
                /// </summary>
                public object CapturedElement { get; private set; }

                private float captureDepth;
                private HudSpaceDelegate GetHudSpaceFunc;

                public HudCursor(HudParentBase parent = null) : base(parent)
                {
                    Material = new Material(MyStringId.GetOrCompute("MouseCursor"), new Vector2(64f));
                    Size = new Vector2(64f);
                    ZOffset = int.MaxValue;

                    var shadow = new TexturedBox(this)
                    {
                        Material = new Material(MyStringId.GetOrCompute("RadialShadow"), new Vector2(32f, 32f)),
                        Color = new Color(0, 0, 0, 96),
                        Size = new Vector2(64f),
                        Offset = new Vector2(12f, -12f),
                        ZOffset = -1
                    };
                }

                /// <summary>
                /// Indicates whether the cursor is being captured by the given element.
                /// </summary>
                public bool IsCapturing(object capturedElement) =>
                    Visible && capturedElement == CapturedElement;

                /// <summary>
                /// Attempts to capture the cursor with the given object
                /// </summary>
                public void Capture(object capturedElement, float depth = 0f, HudSpaceDelegate GetHudSpaceFunc = null)
                {
                    if (this.CapturedElement == null && depth <= captureDepth)
                    {
                        CapturedElement = capturedElement;
                        captureDepth = depth;
                        this.GetHudSpaceFunc = GetHudSpaceFunc;
                    }
                }

                /// <summary>
                /// Attempts to capture the cursor using the given object. Returns true on success.
                /// </summary>
                public bool TryCapture(object capturedElement, float depth = 0f, HudSpaceDelegate GetHudSpaceFunc = null)
                {
                    if (this.CapturedElement == null && depth <= captureDepth)
                    {
                        CapturedElement = capturedElement;
                        captureDepth = depth;
                        this.GetHudSpaceFunc = GetHudSpaceFunc;

                        return true;
                    }
                    else
                        return false;
                }

                /// <summary>
                /// Attempts to release the cursor from the given element. Returns false if
                /// not capture or if not captured by the object given.
                /// </summary>
                public bool TryRelease(object capturedElement)
                {
                    if (this.CapturedElement == capturedElement && capturedElement != null)
                    {
                        Release();
                        return true;
                    }
                    else
                        return false;
                }

                /// <summary>
                /// Releases the cursor
                /// </summary>
                public void Release()
                {
                    CapturedElement = null;
                    captureDepth = 0f;
                    GetHudSpaceFunc = null;
                }

                protected override void Layout()
                {
                    // Reverse scaling due to differences between rendering resolution and
                    // desktop resolution when running the game in windowed mode
                    Vector2 desktopSize = MyAPIGateway.Input.GetMouseAreaSize();
                    Vector2 invMousePosScale = new Vector2
                    {
                        X = ScreenWidth / desktopSize.X,
                        Y = ScreenHeight / desktopSize.Y,
                    };

                    Vector2 pos = MyAPIGateway.Input.GetMousePosition() * invMousePosScale;

                    // Reverse Y-axis direction and offset the cursor position s.t. it's 
                    // centered in the middle of the screen rather than the upper left
                    // corner.
                    pos.Y *= -1f;
                    pos += new Vector2(-ScreenWidth / 2f, ScreenHeight / 2f);

                    // Calculate position of the cursor in world space
                    MatrixD ptw = HudMain.PixelToWorld;
                    Vector3D worldPos = new Vector3D(pos.X, pos.Y, 0d);
                    Vector3D.TransformNoProjection(ref worldPos, ref ptw, out worldPos);

                    WorldPos = worldPos;
                    ScreenPos = pos;
                    Offset = pos;

                    base.Layout();
                }

                protected override object BeginDraw(object matrix)
                {
                    if (Visible)
                    {
                        if (GetHudSpaceFunc != null)
                        {
                            MyTuple<float, MatrixD> spaceDef = GetHudSpaceFunc();
                            Scale = spaceDef.Item1;
                            matrix = spaceDef.Item2;
                        }
                        else
                            Scale = ResScale;
                    }

                    return base.BeginDraw(matrix);
                }

                private object GetOrSetMember(object data, int memberEnum)
                {
                    return null;
                }

                /// <summary>
                /// Returns cursor API interface members
                /// </summary>
                public CursorMembers GetApiData()
                {
                    return new CursorMembers()
                    {
                        Item1 = () => Visible,
                        Item2 = () => IsCaptured,
                        Item3 = () => ScreenPos,
                        Item4 = () => WorldPos,
                        Item5 = Capture,
                        Item6 = new MyTuple<Func<object, bool>, Func<object, float, HudSpaceDelegate, bool>, Func<object, bool>, ApiMemberAccessor>()
                        {
                            Item1 = IsCapturing,
                            Item2 = TryCapture,
                            Item3 = TryRelease,
                            Item4 = GetOrSetMember
                        }
                    };
                }
            }
        }
    }
}
