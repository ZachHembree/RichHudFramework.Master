using RichHudFramework.Internal;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage;
using VRageMath;
using ApiMemberAccessor = System.Func<object, int, object>;
using FloatProp = VRage.MyTuple<System.Func<float>, System.Action<float>>;
using HudSpaceDelegate = System.Func<VRage.MyTuple<bool, float, VRageMath.MatrixD>>;
using RichStringMembers = VRage.MyTuple<System.Text.StringBuilder, VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>>;
using Vec2Prop = VRage.MyTuple<System.Func<VRageMath.Vector2>, System.Action<VRageMath.Vector2>>;

namespace RichHudFramework
{
    using TextBoardMembers = MyTuple<
        // TextBuilderMembers
        MyTuple<
            MyTuple<Func<int, int, object>, Func<int>>, // GetLineMember, GetLineCount
            Func<Vector2I, int, object>, // GetCharMember
            ApiMemberAccessor, // GetOrSetMember
            Action<IList<RichStringMembers>, Vector2I>, // Insert
            Action<IList<RichStringMembers>>, // SetText
            Action // Clear
        >,
        FloatProp, // Scale
        Func<Vector2>, // Size
        Func<Vector2>, // TextSize
        Vec2Prop, // FixedSize
        Action<Vector2, MatrixD> // Draw 
    >;

    namespace UI.Server
    {
        using Rendering.Server;
        using HudUpdateAccessors = MyTuple<
            ApiMemberAccessor,
            MyTuple<Func<ushort>, Func<Vector3D>>, // ZOffset + GetOrigin
            Action, // DepthTest
            Action, // HandleInput
            Action<bool>, // BeforeLayout
            Action // BeforeDraw
        >;

        public sealed partial class HudMain : RichHudComponentBase
        {
            public const byte WindowBaseOffset = 1, WindowMaxOffset = 250;
            public const int treeRefreshRate = 10;

            /// <summary>
            /// Root parent for all HUD elements.
            /// </summary>
            public static HudParentBase Root
            {
                get
                {
                    if (mainInstance == null)
                        Init();

                    return mainInstance._root;
                }
            }

            /// <summary>
            /// Cursor shared between mods.
            /// </summary>
            public static ICursor Cursor
            {
                get
                {
                    if (mainInstance == null)
                        Init();

                    return mainInstance._cursor;
                }
            }

            /// <summary>
            /// Shared clipboard.
            /// </summary>
            public static RichText ClipBoard
            {
                get 
                {
                    if (mainInstance == null)
                        Init();

                    if (mainInstance._clipBoard == null)
                        mainInstance._clipBoard = new RichText();

                    return mainInstance._clipBoard;
                }
                set 
                {
                    if (mainInstance == null)
                        Init();

                    mainInstance._clipBoard = value;
                }
            }

            /// <summary>
            /// Resolution scale normalized to 1080p for resolutions over 1080p. Returns a scale of 1f
            /// for lower resolutions.
            /// </summary>
            public static float ResScale
            {
                get
                {
                    if (mainInstance == null)
                        Init();

                    return mainInstance._resScale;
                }
            }

            /// <summary>
            /// Matrix used to convert from 2D pixel-value screen space coordinates to worldspace.
            /// </summary>
            public static MatrixD PixelToWorld
            {
                get
                {
                    if (mainInstance == null)
                        Init();

                    return mainInstance._pixelToWorld;
                }
            }

            /// <summary>
            /// The current horizontal screen resolution in pixels.
            /// </summary>
            public static float ScreenWidth
            {
                get
                {
                    if (mainInstance == null)
                        Init();

                    return mainInstance._screenWidth;
                }
            }

            /// <summary>
            /// The current vertical resolution in pixels.
            /// </summary>
            public static float ScreenHeight
            {
                get
                {
                    if (mainInstance == null)
                        Init();

                    return mainInstance._screenHeight;
                }
            }

            /// <summary>
            /// The current field of view
            /// </summary>
            public static float Fov
            {
                get
                {
                    if (mainInstance == null)
                        Init();

                    return mainInstance._fov;
                }
            }

            /// <summary>
            /// The current aspect ratio (ScreenWidth/ScreenHeight).
            /// </summary>
            public static float AspectRatio
            {
                get
                {
                    if (mainInstance == null)
                        Init();

                    return mainInstance._aspectRatio;
                }
            }

            /// <summary>
            /// Scaling used by MatBoards to compensate for changes in apparent size and position as a result
            /// of changes to Fov.
            /// </summary>
            public static float FovScale
            {
                get
                {
                    if (mainInstance == null)
                        Init();

                    return mainInstance._fovScale;
                }
            }

            /// <summary>
            /// The current opacity for the in-game menus as configured.
            /// </summary>
            public static float UiBkOpacity
            {
                get
                {
                    if (mainInstance == null)
                        Init();

                    return mainInstance._uiBkOpacity;
                }
            }

            /// <summary>
            /// Used to indicate when the draw list should be refreshed. Resets every frame.
            /// </summary>
            public static bool RefreshDrawList;

            /// <summary>
            /// If true then the cursor will be visible while chat is open
            /// </summary>
            public static bool EnableCursor;

            private static HudMain mainInstance;

            private readonly HudCursor _cursor;
            private readonly HudRoot _root;
            private readonly TreeClient mainClient;

            private RichText _clipBoard;
            private float _resScale;
            private float _uiBkOpacity;

            private MatrixD _pixelToWorld;
            private float _screenWidth;
            private float _screenHeight;
            private float _aspectRatio;
            private float _fov;
            private float _fovScale;

            private Action<byte> LoseFocusCallback;
            private byte unfocusedOffset;
            private int drawTick;

            private HudMain() : base(false, true)
            {
                if (mainInstance == null)
                    mainInstance = this;
                else
                    throw new Exception("Only one instance of HudMain can exist at any given time.");

                _root = new HudRoot();
                _cursor = new HudCursor(_root);
                mainClient = new TreeClient() { GetUpdateAccessors = _root.GetUpdateAccessors };

                UpdateScreenScaling();
                TreeManager.Init();
            }

            public static void Init()
            {
                if (mainInstance == null)
                    new HudMain();
            }

            public override void Close()
            {
                mainInstance = null;
                TreeManager.Close();
            }

            /// <summary>
            /// Draw UI elements
            /// </summary>
            public override void Draw()
            {
                UpdateCache();
                _cursor.Visible = EnableCursor;
                TreeManager.Instance.Draw();

                drawTick++;

                if (drawTick == 60)
                    drawTick = 0;
            }

            public override void HandleInput()
            {
                // Reset cursor
                _cursor.Release();
                TreeManager.Instance.HandleInput();
            }

            /// <summary>
            /// Updates cached values for screen scaling and fov.
            /// </summary>
            private void UpdateCache()
            {
                if (drawTick == 0)
                {
                    UpdateScreenScaling();
                    _uiBkOpacity = MyAPIGateway.Session.Config.UIBkOpacity;
                }

                // Update screen to world matrix transform
                _pixelToWorld = new MatrixD
                {
                    M11 = (FovScale / ScreenHeight),
                    M22 = (FovScale / ScreenHeight),
                    M33 = 1d,
                    M43 = -MyAPIGateway.Session.Camera.NearPlaneDistance,
                    M44 = 1d
                };

                _pixelToWorld *= MyAPIGateway.Session.Camera.WorldMatrix;
            }

            /// <summary>
            /// Updates scaling values used to compensate for resolution, aspect ratio and FOV.
            /// </summary>
            private void UpdateScreenScaling()
            {
                _screenWidth = MyAPIGateway.Session.Camera.ViewportSize.X;
                _screenHeight = MyAPIGateway.Session.Camera.ViewportSize.Y;
                _aspectRatio = (_screenWidth / _screenHeight);
                _resScale = (_screenHeight > 1080f) ? _screenHeight / 1080f : 1f;

                _fov = MyAPIGateway.Session.Camera.FovWithZoom;
                _fovScale = (float)(0.1f * Math.Tan(_fov / 2d));
            }

            /// <summary>
            /// Returns API accessors for a new TextBoard instance.
            /// </summary>
            public static TextBoardMembers GetTextBoardData() =>
                new TextBoard().GetApiData();

            /// <summary>
            /// Returns the ZOffset for focusing a window and registers a callback
            /// for when another object takes focus.
            /// </summary>
            public static byte GetFocusOffset(Action<byte> LoseFocusCallback)
            {
                if (mainInstance == null)
                    Init();

                return mainInstance.GetFocusOffsetInternal(LoseFocusCallback);
            }

            /// <summary>
            /// Returns the ZOffset for focusing a window and registers a callback
            /// for when another object takes focus.
            /// </summary>
            private byte GetFocusOffsetInternal(Action<byte> LoseFocusCallback)
            {
                if (LoseFocusCallback != null)
                {
                    this.LoseFocusCallback?.Invoke(unfocusedOffset);
                    unfocusedOffset++;

                    if (unfocusedOffset >= WindowMaxOffset)
                        unfocusedOffset = WindowBaseOffset;

                    this.LoseFocusCallback = LoseFocusCallback;

                    return WindowMaxOffset;
                }
                else
                    return 0;
            }

            /// <summary>
            /// Converts from a position in absolute screen space coordinates to a position in pixels.
            /// </summary>
            public static Vector2 GetPixelVector(Vector2 scaledVec)
            {
                if (mainInstance == null)
                    Init();

                return new Vector2
                (
                    (int)(scaledVec.X * mainInstance._screenWidth),
                    (int)(scaledVec.Y * mainInstance._screenHeight)
                );
            }

            /// <summary>
            /// Converts from a coordinate given in pixels to a position in absolute units.
            /// </summary>
            public static Vector2 GetAbsoluteVector(Vector2 pixelVec)
            {
                if (mainInstance == null)
                    Init();

                return new Vector2
                (
                    pixelVec.X / mainInstance._screenWidth,
                    pixelVec.Y / mainInstance._screenHeight
                );
            }

            /// <summary>
            /// Root parent for all hud elements.
            /// </summary>
            private sealed class HudRoot : HudParentBase, IReadOnlyHudSpaceNode
            {
                public override bool Visible => true;

                public bool DrawCursorInHudSpace { get; }

                public Vector3 CursorPos { get; private set; }

                public HudSpaceDelegate GetHudSpaceFunc { get; }

                public MatrixD PlaneToWorld { get; private set; }

                public Func<MatrixD> UpdateMatrixFunc { get; }

                public Func<Vector3D> GetNodeOriginFunc { get; }

                public bool IsInFront { get; }

                public bool IsFacingCamera { get; }

                public HudRoot()
                {
                    DrawCursorInHudSpace = true;
                    HudSpace = this;
                    IsInFront = true;
                    IsFacingCamera = true;

                    GetHudSpaceFunc = () => new MyTuple<bool, float, MatrixD>(true, 1f, PixelToWorld);
                    GetNodeOriginFunc = () => PixelToWorld.Translation;
                }

                protected override void Layout()
                {
                    PlaneToWorld = PixelToWorld;
                    CursorPos = new Vector3(Cursor.ScreenPos.X, Cursor.ScreenPos.Y, 0f);
                }
            }
        }
    }

    namespace UI.Client
    { }
}
