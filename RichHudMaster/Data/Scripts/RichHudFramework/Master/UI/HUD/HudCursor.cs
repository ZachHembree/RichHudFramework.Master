using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage;
using VRage.Utils;
using VRageMath;
using ApiMemberAccessor = System.Func<object, int, object>;
using HudSpaceData = VRage.MyTuple<bool, float, VRageMath.MatrixD>;
using RichStringMembers = VRage.MyTuple<System.Text.StringBuilder, VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>>;

namespace RichHudFramework
{
    using HudSpaceDelegate = Func<HudSpaceData>;
    using ToolTipMembers = MyTuple<
        List<RichStringMembers>, // Text
        Color? // BgColor
    >;

    namespace UI.Server
    {
        using Rendering;
        using CursorMembers = MyTuple<
            Func<HudSpaceDelegate, bool>, // IsCapturingSpace
            Func<float, HudSpaceDelegate, bool>, // TryCaptureHudSpace
            Func<ApiMemberAccessor, bool>, // IsCapturing
            Func<ApiMemberAccessor, bool>, // TryCapture
            Func<ApiMemberAccessor, bool>, // TryRelease
            ApiMemberAccessor // GetOrSetMember
        >;

        public sealed partial class HudMain
        {
            /// <summary>
            /// Draws cursor shared by elements in the framework
            /// </summary>
            public sealed class HudCursor : HudParentBase, IReadOnlyHudSpaceNode, ICursor
            {
                /// <summary>
                /// Returns true if the cursor is drawing
                /// </summary>
                public bool DrawCursor { get; set; }

                /// <summary>
                /// Indicates whether the cursor is currently visible
                /// </summary>
                bool ICursor.Visible => DrawCursor;

                /// <summary>
                /// Cursor position on the XY plane defined by the HUD space. Z == dist from screen.
                /// </summary>
                public Vector3 CursorPos { get; private set; }

                /// <summary>
                /// The position of the cursor in pixels in screen space
                /// </summary>
                public Vector2 ScreenPos { get; private set; }

                /// <summary>
                /// Position of the cursor in world space.
                /// </summary>
                public Vector3D WorldPos { get; private set; }

                /// <summary>
                /// Line projected from the cursor into world space on the -Z axis 
                /// correcting for apparent warping due to perspective projection.
                /// </summary>
                public LineD WorldLine { get; private set; }

                /// <summary>
                /// Returns true if the cursor has been captured by a UI element
                /// </summary>
                public bool IsCaptured { get; private set; }

                /// <summary>
                /// Returns true if a tooltip has been registered
                /// </summary>
                public bool IsToolTipRegistered { get; private set; }

                /// <summary>
                /// Returns the object capturing the cursor
                /// </summary>
                public ApiMemberAccessor CapturedElement { get; private set; }

                /// <summary>
                /// If true, then the cursor will be drawn using the PTW matrix of this HUD space when
                /// captured by one of its children.
                /// </summary>
                public bool DrawCursorInHudSpace { get; }

                /// <summary>
                /// Delegate used to retrieve current hud space. Used for cursor depth testing.
                /// </summary>
                public HudSpaceDelegate GetHudSpaceFunc { get; }

                /// <summary>
                /// Returns the current draw matrix
                /// </summary>
                public MatrixD PlaneToWorld => PlaneToWorldRef[0];

                /// <summary>
                /// Returns the current draw matrix by reference as an array of length 1
                /// </summary>
                public MatrixD[] PlaneToWorldRef { get; }

                /// <summary>
                /// Returns the world space position of the node's origin.
                /// </summary>
                public Func<Vector3D> GetNodeOriginFunc { get; }

                /// <summary>
                /// True if the origin of the HUD space is in front of the camera
                /// </summary>
                public bool IsInFront { get; }

                /// <summary>
                /// True if the XY plane of the HUD space is in front and facing toward the camera
                /// </summary>
                public bool IsFacingCamera { get; }

                private float captureDepth;
                private Func<ToolTipMembers> GetToolTipFunc;
                private HudSpaceDelegate GetCapturedHudSpaceFunc;
                private readonly TexturedBox cursorBox;
                private readonly LabelBox toolTip;

                public HudCursor()
                {
                    HudSpace = this;
                    IsInFront = true;
                    IsFacingCamera = true;

                    layerData.zOffset = sbyte.MaxValue;
                    layerData.zOffsetInner = byte.MaxValue;
                    layerData.fullZOffset = ushort.MaxValue;
                    State |= HudElementStates.CanPreload;

                    GetHudSpaceFunc = () => new HudSpaceData(true, 1f, PlaneToWorldRef[0]);
                    GetNodeOriginFunc = () => PlaneToWorldRef[0].Translation;
                    PlaneToWorldRef = new MatrixD[1];

                    cursorBox = new TexturedBox()
                    {
                        Material = new Material(MyStringId.GetOrCompute("MouseCursor"), new Vector2(64f)),
                        Size = new Vector2(64f)
                    };
                    cursorBox.Register(this, true);

                    var shadow = new TexturedBox(cursorBox)
                    {
                        Material = new Material(MyStringId.GetOrCompute("RadialShadow"), new Vector2(32f, 32f)),
                        Color = new Color(0, 0, 0, 96),
                        Size = new Vector2(64f),
                        Offset = new Vector2(12f, -12f),
                        ZOffset = -1
                    };

                    toolTip = new LabelBox(cursorBox)
                    {
                        Visible = false,
                        ZOffset = -2,
                        TextPadding = new Vector2(10f, 6f),
                        BuilderMode = TextBuilderModes.Lined,
                        AutoResize = true
                    };
                }

                /// <summary>
                /// Returns true if the given HUD space is being captured by the cursor
                /// </summary>
                public bool IsCapturingSpace(HudSpaceDelegate GetHudSpaceFunc) =>
                    (HudMain.InputMode != HudInputMode.NoInput) && GetCapturedHudSpaceFunc == GetHudSpaceFunc;

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
                public bool TryCaptureHudSpace(float depthSquared, HudSpaceDelegate GetHudSpaceFunc)
                {
                    if (GetCapturedHudSpaceFunc == null || depthSquared <= captureDepth)
                    {
                        captureDepth = depthSquared;
                        GetCapturedHudSpaceFunc = GetHudSpaceFunc;

                        return true;
                    }
                    else
                        return false;
                }

                /// <summary>
                /// Indicates whether the cursor is being captured by the given element.
                /// </summary>
                public bool IsCapturing(ApiMemberAccessor capturedElement) =>
                    (HudMain.InputMode != HudInputMode.NoInput) && capturedElement == CapturedElement;

                /// <summary>
                /// Attempts to capture the cursor with the given object
                /// </summary>
                public void Capture(ApiMemberAccessor capturedElement) =>
                    TryCapture(capturedElement);

                /// <summary>
                /// Attempts to capture the cursor using the given object. Returns true on success.
                /// </summary>
                public bool TryCapture(ApiMemberAccessor capturedElement)
                {
                    if (capturedElement != null && CapturedElement == null)
                    {
                        CapturedElement = capturedElement;
                        IsCaptured = CapturedElement != null;
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }

                /// <summary>
                /// Attempts to release the cursor from the given element. Returns false if
                /// not capture or if not captured by the object given.
                /// </summary>
                public bool TryRelease(ApiMemberAccessor capturedElement)
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
                /// Registers a callback delegate to set the tooltip for the next frame. Tooltips are reset
                /// every frame and must be reregistered on HandleInput() every tick.
                /// </summary>
                public void RegisterToolTip(ToolTip toolTip)
                {
                    if (GetToolTipFunc == null && toolTip.GetToolTipFunc != null)
                    {
                        GetToolTipFunc = toolTip.GetToolTipFunc;
                        IsToolTipRegistered = true;
                    }
                }

                private void RegisterToolTipInternal(Func<ToolTipMembers> toolTip)
                {
                    if (GetToolTipFunc == null && toolTip != null)
                    {
                        GetToolTipFunc = toolTip;
                        IsToolTipRegistered = true;
                    }
                }

                /// <summary>
                /// Releases the cursor
                /// </summary>
                public void Release()
                {
                    CapturedElement = null;
                    IsCaptured = false;
                    captureDepth = 0f;
                    GetCapturedHudSpaceFunc = null;
                    IsToolTipRegistered = false;
                    GetToolTipFunc = null;
                }

                public void UpdateCursorPos(Vector2 screenPos, ref MatrixD ptw)
                {
                    // Update world line
                    WorldLine = MyAPIGateway.Session.Camera.WorldLineFromScreen(screenPos);

                    // Reverse Y-axis direction and offset the cursor position s.t. it's 
                    // centered in the middle of the screen rather than the upper left
                    // corner.
                    screenPos.Y *= -1f;
                    screenPos += new Vector2(-ScreenWidth * .5f, ScreenHeight * .5f);

                    // Calculate position of the cursor in world space
                    Vector3D worldPos = new Vector3D(screenPos.X, screenPos.Y, 0d);
                    Vector3D.TransformNoProjection(ref worldPos, ref ptw, out worldPos);

                    WorldPos = worldPos;
                    ScreenPos = screenPos;
                }

                protected override void Layout()
                {
                    // Update custom hud space and tooltips
                    HudSpaceData? hudSpaceData = GetCapturedHudSpaceFunc?.Invoke();
                    bool useCapturedHudSpace = hudSpaceData != null && hudSpaceData.Value.Item1;
                    bool boundTooltips = false, useScreenSpace = true;
                    float tooltipScale = 1f;

                    if (useCapturedHudSpace)
                    {
                        PlaneToWorldRef[0] = hudSpaceData.Value.Item3;
                        useScreenSpace = 
                            PlaneToWorldRef[0].EqualsFast(ref HighDpiRoot.HudSpace.PlaneToWorldRef[0]) ||
                            PlaneToWorldRef[0].EqualsFast(ref Root.HudSpace.PlaneToWorldRef[0]);
                    }

                    if (useScreenSpace)
                    {
                        PlaneToWorldRef[0] = Root.HudSpace.PlaneToWorldRef[0];
                        boundTooltips = true;
                        tooltipScale = ResScale;
                    }

                    MatrixD worldToPlane;
                    MatrixD.Invert(ref PlaneToWorldRef[0], out worldToPlane);
                    LineD cursorLine = HudMain.Cursor.WorldLine;

                    PlaneD plane = new PlaneD(PlaneToWorldRef[0].Translation, PlaneToWorldRef[0].Forward);
                    Vector3D worldPos = plane.Intersection(ref cursorLine.From, ref cursorLine.Direction);

                    Vector3D planePos;
                    Vector3D.TransformNoProjection(ref worldPos, ref worldToPlane, out planePos);

                    CursorPos = new Vector3()
                    {
                        X = (float)planePos.X,
                        Y = (float)planePos.Y,
                        Z = (float)Math.Round(Vector3D.DistanceSquared(worldPos, cursorLine.From), 6)
                    };

                    cursorBox.Offset = new Vector2(CursorPos.X, CursorPos.Y);
                    cursorBox.Visible = DrawCursor && !MyAPIGateway.Gui.IsCursorVisible;

                    UpdateToolTip(boundTooltips, tooltipScale);
                }

                private void UpdateToolTip(bool boundTooltip, float scale)
                {
                    toolTip.Visible = IsToolTipRegistered;

                    if (GetToolTipFunc != null)
                    {
                        ToolTipMembers data = GetToolTipFunc();

                        if (data.Item1 != null)
                        {
                            toolTip.Visible = true;
                            toolTip.Format = ToolTip.DefaultText;
                            toolTip.TextBoard.Scale = scale;
                            toolTip.TextBoard.SetText(data.Item1);

                            if (data.Item2 != null)
                                toolTip.Color = data.Item2.Value;
                            else
                                toolTip.Color = ToolTip.DefaultBG;
                        }

                        GetToolTipFunc = null;

                        /* Position tooltip s.t. its placed below and to the right of the cursor while also bounding
                         the position to keep it from going off screen. */
                        Vector2 halfTooltipSize = toolTip.Size * .5f,
                            cursorPos = new Vector2(CursorPos.X, CursorPos.Y), 
                            toolTipPos = cursorPos + new Vector2(24f, -24f);

                        toolTipPos.X += halfTooltipSize.X;
                        toolTipPos.Y -= halfTooltipSize.Y;

                        BoundingBox2 toolBox = new BoundingBox2(toolTipPos - halfTooltipSize, toolTipPos + halfTooltipSize);

                        if (boundTooltip)
                        {
                            Vector2 halfScreenSize = new Vector2(ScreenWidth, ScreenHeight) * .5f;
                            BoundingBox2 screenBox = new BoundingBox2(-halfScreenSize, halfScreenSize),
                                offsetBox = new BoundingBox2(cursorPos - halfScreenSize, cursorPos + halfScreenSize);

                            offsetBox = offsetBox.Intersect(screenBox);
                            toolBox = toolBox.Intersect(offsetBox);
                        }

                        toolTipPos -= cursorPos;

                        Vector2 delta = (toolBox.Center - cursorPos) - toolTipPos;
                        toolTip.Offset = toolTipPos + 2f * delta;
                    }
                }

                private object GetOrSetMember8(object data, int memberEnum)
                {
                    switch ((HudCursorAccessors)memberEnum)
                    {
                        case HudCursorAccessors.Visible:
                            return HudMain.InputMode == HudInputMode.Full;
                        case HudCursorAccessors.IsCaptured:
                            return IsCaptured;
                        case HudCursorAccessors.ScreenPos:
                            return ScreenPos;
                        case HudCursorAccessors.WorldPos:
                            return WorldPos;
                        case HudCursorAccessors.WorldLine:
                            return WorldLine;
                    }

                    return null;
                }

                private object GetOrSetMember(object data, int memberEnum)
                {
                    switch ((HudCursorAccessors)memberEnum)
                    {
                        case HudCursorAccessors.Visible:
                            return DrawCursor;
                        case HudCursorAccessors.IsCaptured:
                            return IsCaptured;
                        case HudCursorAccessors.ScreenPos:
                            return ScreenPos;
                        case HudCursorAccessors.WorldPos:
                            return WorldPos;
                        case HudCursorAccessors.WorldLine:
                            return WorldLine;
                        case HudCursorAccessors.RegisterToolTip:
                            {
                                if (!IsToolTipRegistered)
                                    RegisterToolTipInternal(data as Func<ToolTipMembers>);

                                break;
                            }
                        case HudCursorAccessors.IsToolTipRegistered:
                            return IsToolTipRegistered;
                    }

                    return null;
                }

                /// <summary>
                /// Returns cursor API interface members
                /// </summary>
                public CursorMembers GetApiData8()
                {
                    return new CursorMembers()
                    {
                        Item1 = IsCapturingSpace,
                        Item2 = TryCaptureHudSpace,
                        Item3 = IsCapturing,
                        Item4 = TryCapture,
                        Item5 = TryRelease,
                        Item6 = GetOrSetMember8
                    };
                }

                /// <summary>
                /// Returns cursor API interface members
                /// </summary>
                public CursorMembers GetApiData()
                {
                    return new CursorMembers()
                    {
                        Item1 = IsCapturingSpace,
                        Item2 = TryCaptureHudSpace,
                        Item3 = IsCapturing,
                        Item4 = TryCapture,
                        Item5 = TryRelease,
                        Item6 = GetOrSetMember
                    };
                }
            }
        }
    }
}
