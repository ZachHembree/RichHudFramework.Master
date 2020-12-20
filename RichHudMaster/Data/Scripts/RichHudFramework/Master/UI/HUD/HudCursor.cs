using Sandbox.ModAPI;
using System;
using VRage;
using VRage.Utils;
using VRageMath;
using ApiMemberAccessor = System.Func<object, int, object>;
using HudSpaceDelegate = System.Func<VRage.MyTuple<bool, float, VRageMath.MatrixD>>;

namespace RichHudFramework
{
    using CursorMembers = MyTuple<
        Func<bool>, // Visible
        Func<bool>, // IsCaptured
        Func<Vector2>, // Position
        Func<Vector3D>, // WorldPos
        Func<HudSpaceDelegate, bool>, // IsCapturingSpace
        MyTuple<
            Func<float, HudSpaceDelegate, bool>, // TryCaptureHudSpace
            Func<object, bool>, // IsCapturing
            Func<object, bool>, // TryCapture
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
            private sealed class HudCursor : HudSpaceNode, ICursor
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
                private readonly TexturedBox cursorBox;

                public HudCursor(HudParentBase parent = null) : base(parent)
                {
                    ZOffset = sbyte.MaxValue;
                    zOffsetInner = byte.MaxValue;
                    //GetNodeOriginFunc = () => Vector3D.Zero;

                    cursorBox = new TexturedBox(this)
                    {
                        Material = new Material(MyStringId.GetOrCompute("MouseCursor"), new Vector2(64f)),
                        Size = new Vector2(64f),
                    };

                    var shadow = new TexturedBox(cursorBox)
                    {
                        Material = new Material(MyStringId.GetOrCompute("RadialShadow"), new Vector2(32f, 32f)),
                        Color = new Color(0, 0, 0, 96),
                        Size = new Vector2(64f),
                        Offset = new Vector2(12f, -12f),
                        ZOffset = -1
                    };
                }

                /// <summary>
                /// Returns true if the given HUD space is being captured by the cursor
                /// </summary>
                public bool IsCapturingSpace(HudSpaceDelegate GetHudSpaceFunc) =>
                    Visible && this.GetHudSpaceFunc == GetHudSpaceFunc;

                /// <summary>
                /// Attempts to capture the cursor at the given depth with the given HUD space. If drawInHudSpace
                /// is true, then the cursor will be drawn in the given space.
                /// </summary>
                public void CaptureHudSpace(float depth, HudSpaceDelegate GetHudSpaceFunc) =>
                    TryCaptureHudSpace(depth, GetHudSpaceFunc);

                /// <summary>
                /// Attempts to capture the cursor at the given depth with the given HUD space. If drawInHudSpace
                /// is true, then the cursor will be drawn in the given space.
                /// </summary>
                public bool TryCaptureHudSpace(float depth, HudSpaceDelegate GetHudSpaceFunc)
                {
                    if (this.GetHudSpaceFunc == null || depth <= captureDepth)
                    {
                        captureDepth = depth;
                        this.GetHudSpaceFunc = GetHudSpaceFunc;

                        return true;
                    }
                    else
                        return false;
                }

                /// <summary>
                /// Indicates whether the cursor is being captured by the given element.
                /// </summary>
                public bool IsCapturing(object capturedElement) =>
                    Visible && capturedElement == CapturedElement;

                /// <summary>
                /// Attempts to capture the cursor with the given object
                /// </summary>
                public void Capture(object capturedElement) =>
                    TryCapture(capturedElement);

                /// <summary>
                /// Attempts to capture the cursor using the given object. Returns true on success.
                /// </summary>
                public bool TryCapture(object capturedElement)
                {
                    if (capturedElement != null && CapturedElement == null)
                    {
                        CapturedElement = capturedElement;
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
                    if (CapturedElement == capturedElement && capturedElement != null)
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

                protected override void BeginLayout(bool refresh)
                {
                    base.BeginLayout(refresh);

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
                    cursorBox.Offset = pos;

                    if (Visible)
                    {
                        Scale = ResScale;

                        if (GetHudSpaceFunc != null)
                        {
                            MyTuple<bool, float, MatrixD> spaceDef = GetHudSpaceFunc();

                            if (spaceDef.Item1)
                            {
                                Scale = spaceDef.Item2;
                                PlaneToWorld = spaceDef.Item3;
                            }
                        }
                    }
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
                        Item5 = IsCapturingSpace,
                        Item6 = new MyTuple<Func<float, HudSpaceDelegate, bool>, Func<object, bool>, Func<object, bool>, Func<object, bool>, ApiMemberAccessor>()
                        {
                            Item1 = TryCaptureHudSpace,
                            Item2 = IsCapturing,
                            Item3 = TryCapture,
                            Item4 = TryRelease,
                            Item5 = GetOrSetMember
                        }
                    };
                }
            }
        }
    }
}
