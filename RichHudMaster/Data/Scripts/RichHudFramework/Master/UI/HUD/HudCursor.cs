﻿using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage;
using VRage.Utils;
using VRageMath;
using ApiMemberAccessor = System.Func<object, int, object>;
using HudSpaceDelegate = System.Func<VRage.MyTuple<bool, float, VRageMath.MatrixD>>;
using RichStringMembers = VRage.MyTuple<System.Text.StringBuilder, VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>>;

namespace RichHudFramework
{
    using ToolTipMembers = MyTuple<
        List<RichStringMembers>, // Text
        Color? // BgColor
    >;
    using CursorMembers = MyTuple<
        Func<HudSpaceDelegate, bool>, // IsCapturingSpace
        Func<float, HudSpaceDelegate, bool>, // TryCaptureHudSpace
        Func<ApiMemberAccessor, bool>, // IsCapturing
        Func<ApiMemberAccessor, bool>, // TryCapture
        Func<ApiMemberAccessor, bool>, // TryRelease
        ApiMemberAccessor // GetOrSetMember
    >;

    namespace UI.Server
    {
        using Rendering;

        public sealed partial class HudMain
        {
            /// <summary>
            /// Draws cursor shared by elements in the framework
            /// </summary>
            public sealed class HudCursor : HudSpaceNodeBase, ICursor
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
                /// Line projected from the cursor into world space on the -Z axis 
                /// correcting for apparent warping due to perspective projection.
                /// </summary>
                public LineD WorldLine { get; private set; }

                /// <summary>
                /// Returns true if the cursor has been captured by a UI element
                /// </summary>
                public bool IsCaptured => CapturedElement != null;

                /// <summary>
                /// Returns true if a tooltip has been registered
                /// </summary>
                public bool IsToolTipRegistered { get; private set; }

                /// <summary>
                /// Returns the object capturing the cursor
                /// </summary>
                public ApiMemberAccessor CapturedElement { get; private set; }

                private float captureDepth;
                private Func<ToolTipMembers> GetToolTipFunc;
                private readonly TexturedBox cursorBox;
                private readonly LabelBox toolTip;

                public HudCursor(HudParentBase parent = null) : base(parent)
                {
                    ZOffset = sbyte.MaxValue;
                    layerData.zOffsetInner = byte.MaxValue;

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

                    toolTip = new LabelBox(cursorBox)
                    {
                        Visible = false,
                        ParentAlignment = ParentAlignments.Bottom | ParentAlignments.Right,
                        ZOffset = -2,
                        TextPadding = new Vector2(10f, 8f),
                        BuilderMode = TextBuilderModes.Lined,
                        AutoResize = true
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
                public bool TryCaptureHudSpace(float depthSquared, HudSpaceDelegate GetHudSpaceFunc)
                {
                    if (this.GetHudSpaceFunc == null || depthSquared <= captureDepth)
                    {
                        captureDepth = depthSquared;
                        this.GetHudSpaceFunc = GetHudSpaceFunc;

                        return true;
                    }
                    else
                        return false;
                }

                /// <summary>
                /// Indicates whether the cursor is being captured by the given element.
                /// </summary>
                public bool IsCapturing(ApiMemberAccessor capturedElement) =>
                    Visible && capturedElement == CapturedElement;

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
                        return true;
                    }
                    else
                        return false;
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
                    captureDepth = 0f;
                    GetHudSpaceFunc = null;
                }

                protected override void Layout()
                {
                    LocalScale = HudMain.ResScale;

                    // Reverse scaling due to differences between rendering resolution and
                    // desktop resolution when running the game in windowed mode
                    Vector2 desktopSize = MyAPIGateway.Input.GetMouseAreaSize();
                    Vector2 invMousePosScale = new Vector2
                    {
                        X = ScreenWidth / desktopSize.X,
                        Y = ScreenHeight / desktopSize.Y,
                    };

                    Vector2 screenPos = MyAPIGateway.Input.GetMousePosition() * invMousePosScale;

                    // Update world line
                    WorldLine = MyAPIGateway.Session.Camera.WorldLineFromScreen(screenPos);

                    // Reverse Y-axis direction and offset the cursor position s.t. it's 
                    // centered in the middle of the screen rather than the upper left
                    // corner.
                    screenPos.Y *= -1f;
                    screenPos += new Vector2(-ScreenWidth * .5f, ScreenHeight * .5f);

                    // Calculate position of the cursor in world space
                    PlaneToWorldRef[0] = HudMain.PixelToWorld;

                    Vector3D worldPos = new Vector3D(screenPos.X, screenPos.Y, 0d);
                    Vector3D.TransformNoProjection(ref worldPos, ref PlaneToWorldRef[0], out worldPos);

                    WorldPos = worldPos;
                    ScreenPos = screenPos;
                    cursorBox.Offset = screenPos;

                    layerData.fullZOffset = ParentUtils.GetFullZOffset(layerData, _parent);
                    cursorBox.Visible = !MyAPIGateway.Gui.IsCursorVisible;

                    UpdateToolTip();
                    base.Layout();
                }

                protected override void HandleInput(Vector2 cursorPos)
                {
                    IsToolTipRegistered = false;
                    GetToolTipFunc = null;
                }

                private void UpdateToolTip()
                {
                    toolTip.Visible = IsToolTipRegistered;

                    if (GetToolTipFunc != null)
                    {
                        ToolTipMembers data = GetToolTipFunc();

                        if (data.Item1 != null)
                        {
                            toolTip.Visible = true;
                            toolTip.Format = ToolTip.DefaultText;
                            toolTip.TextBoard.SetText(data.Item1);

                            if (data.Item2 != null)
                                toolTip.Color = data.Item2.Value;
                            else
                                toolTip.Color = ToolTip.DefaultBG;
                        }

                        GetToolTipFunc = null;
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
                            return Visible;
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
