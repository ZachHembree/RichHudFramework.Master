using RichHudFramework.Internal;
using Sandbox.ModAPI;
using System;
using System.Text;
using System.Collections.Generic;
using VRage;
using VRage.Game.ModAPI;
using VRageMath;
using VRage.Utils;

using ApiMemberAccessor = System.Func<object, int, object>;
using FloatProp = VRage.MyTuple<System.Func<float>, System.Action<float>>;
using RichStringMembers = VRage.MyTuple<System.Text.StringBuilder, VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>>;
using Vec2Prop = VRage.MyTuple<System.Func<VRageMath.Vector2>, System.Action<VRageMath.Vector2>>;

namespace RichHudFramework
{
    using CursorMembers = MyTuple<
        Func<bool>, // Visible
        Func<bool>, // IsCaptured
        Func<Vector2>, // Origin
        Action<object>, // Capture
        Func<object, bool>, // IsCapturing
        MyTuple<
            Func<object, bool>, // TryCapture
            Func<object, bool>, // TryRelease
            ApiMemberAccessor  // GetOrSetMembers
        >
    >;
    using HudElementMembers = MyTuple<
        Func<bool>, // Visible
        object, // ID
        Action<bool>, // BeforeLayout
        Action<int>, // BeforeDraw
        Action, // HandleInput
        ApiMemberAccessor // GetOrSetMembers
    >;
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
        Action<Vector2> // Draw 
    >;

    namespace UI.Server
    {
        using Rendering.Server;
        using HudMainMembers = MyTuple<
            HudElementMembers,
            CursorMembers,
            Func<float>, // ScreenWidth
            Func<float>, // ScreenHeight
            Func<float>, // AspectRatio
            MyTuple<
                Func<float>, // ResScale
                Func<float>, // Fov
                Func<float>, // FovScale
                MyTuple<Func<IList<RichStringMembers>>, Action<IList<RichStringMembers>>>,
                Func<TextBoardMembers>, // GetNewTextBoard
                ApiMemberAccessor // GetOrSetMembers
            >
        >;

        public sealed partial class HudMain : RichHudComponentBase
        {
            /// <summary>
            /// Root parent for all HUD elements.
            /// </summary>
            public static IHudParent Root
            {
                get
                {
                    if (_instance == null)
                        Init();

                    return _instance.root;
                }
            }

            /// <summary>
            /// Cursor shared between mods.
            /// </summary>
            public static ICursor Cursor
            {
                get
                {
                    if (_instance == null)
                        Init();

                    return _instance.cursor;
                }
            }

            /// <summary>
            /// Shared clipboard.
            /// </summary>
            public static RichText ClipBoard { get { return Instance._clipBoard; } set { Instance._clipBoard = value; } }

            /// <summary>
            /// Resolution scale normalized to 1080p for resolutions over 1080p. Returns a scale of 1f
            /// for lower resolutions.
            /// </summary>
            public static float ResScale { get { return Instance._resScale; } set { Instance._resScale = value; } }

            /// <summary>
            /// Debugging element used to test scaling and positioning of members.
            /// </summary>
            public static UiTestPattern TestPattern { get { return Instance._uiTestPattern; } set { Instance._uiTestPattern = value; } }

            /// <summary>
            /// Matrix used to convert from 2D pixel-value screen space coordinates to worldspace.
            /// </summary>
            public static MatrixD PixelToWorld
            {
                get
                {
                    if (_instance == null)
                        Init();

                    return _instance.pixelToWorld;
                }
            }

            /// <summary>
            /// The current horizontal screen resolution in pixels.
            /// </summary>
            public static float ScreenWidth
            {
                get
                {
                    if (_instance == null)
                        Init();

                    return _instance.screenWidth;
                }
            }

            /// <summary>
            /// The current vertical resolution in pixels.
            /// </summary>
            public static float ScreenHeight
            {
                get
                {
                    if (_instance == null)
                        Init();

                    return _instance.screenHeight;
                }
            }

            /// <summary>
            /// The current field of view
            /// </summary>
            public static float Fov
            {
                get
                {
                    if (_instance == null)
                        Init();

                    return _instance.fov;
                }
            }

            /// <summary>
            /// The current aspect ratio (ScreenWidth/ScreenHeight).
            /// </summary>
            public static float AspectRatio
            {
                get
                {
                    if (_instance == null)
                        Init();

                    return _instance.aspectRatio;
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
                    if (_instance == null)
                        Init();

                    return _instance.fovScale;
                }
            }

            /// <summary>
            /// The current opacity for the in-game menus as configured.
            /// </summary>
            public static float UiBkOpacity
            {
                get
                {
                    if (_instance == null)
                        Init();

                    return _instance.uiBkOpacity;
                }
            }

            private static HudMain Instance
            {
                get { Init(); return _instance; }
                set { _instance = value; }
            }
            private static HudMain _instance;

            private RichText _clipBoard;
            private float _resScale;
            private UiTestPattern _uiTestPattern;

            private readonly HudRoot root;
            private readonly HudCursor cursor;
            private readonly Utils.Stopwatch cacheTimer;
            private MatrixD pixelToWorld;
            private float screenWidth, screenHeight, aspectRatio, fov, fovScale, uiBkOpacity;
            private int tick;

            private HudMain() : base(false, true)
            {
                root = new HudRoot();
                cursor = new HudCursor();

                cacheTimer = new Utils.Stopwatch();
                cacheTimer.Start();

                _uiTestPattern = new UiTestPattern();
                _uiTestPattern.Hide();

                cursor.Visible = true;
            }

            public static void Init()
            {
                if (_instance == null)
                {
                    _instance = new HudMain();
                    Instance.UpdateResScaling();
                    Instance.UpdateFovScaling();
                }
            }

            public override void Close()
            {
                Instance = null;
            }

            public override void Draw()
            {
                if (cacheTimer.ElapsedMilliseconds > 2000)
                {
                    if (screenHeight != MyAPIGateway.Session.Camera.ViewportSize.Y || screenWidth != MyAPIGateway.Session.Camera.ViewportSize.X)
                        UpdateResScaling();

                    if (fov != MyAPIGateway.Session.Camera.FovWithZoom)
                        UpdateFovScaling();

                    uiBkOpacity = MyAPIGateway.Session.Config.UIBkOpacity;
                    cacheTimer.Reset();
                }

                pixelToWorld = new MatrixD
                {
                    M11 = (FovScale / ScreenHeight),    M12 = 0d,                           M13 = 0d,       M14 = 0d,
                    M21 = 0d,                           M22 = (FovScale / ScreenHeight),    M23 = 0d,       M24 = 0d,
                    M31 = 0d,                           M32 = 0d,                           M33 = -0.05,    M34 = 0d,
                    M41 = 0d,                           M42 = 0d,                           M43 = 0d,       M44 = 1d
                };

                pixelToWorld *= MyAPIGateway.Session.Camera.WorldMatrix;

                root.BeforeLayout(tick == 0);
                root.BeforeDraw(HudLayers.Background);
                root.BeforeDraw(HudLayers.Normal);
                root.BeforeDraw(HudLayers.Foreground);
                cursor.Draw();

                tick++;

                if (tick == 30)
                    tick = 0;
            }

            public override void HandleInput()
            {
                cursor.Release();
                root.BeforeInput();
                cursor.HandleInput();
            }

            private void UpdateResScaling()
            {
                screenWidth = MyAPIGateway.Session.Camera.ViewportSize.X;
                screenHeight = MyAPIGateway.Session.Camera.ViewportSize.Y;
                aspectRatio = (screenWidth / screenHeight);

                ResScale = (screenHeight > 1080f) ? screenHeight / 1080f : 1f;
            }

            private void UpdateFovScaling()
            {
                fov = MyAPIGateway.Session.Camera.FovWithZoom;
                fovScale = (float)(0.1f * Math.Tan(fov / 2d));
            }

            public static TextBoardMembers GetTextBoardData() =>
                new TextBoard().GetApiData();

            /// <summary>
            /// Converts from a value in the relative coordinate system to a concrete value in pixels.
            /// </summary>
            public static Vector2 GetPixelVector(Vector2 scaledVec)
            {
                if (_instance == null)
                    Init();

                return new Vector2
                (
                    (int)(scaledVec.X * _instance.screenWidth),
                    (int)(scaledVec.Y * _instance.screenHeight)
                );
            }

            /// <summary>
            /// Converts from a coordinate given in pixels to a scaled system independent of screen resolution.
            /// </summary>
            public static Vector2 GetRelativeVector(Vector2 pixelVec)
            {
                if (_instance == null)
                    Init();

                return new Vector2
                (
                    pixelVec.X / _instance.screenWidth,
                    pixelVec.Y / _instance.screenHeight
                );
            }

            private static object GetOrSetMember(object data, int memberEnum)
            {
                return null;
            }

            public static HudMainMembers GetApiData()
            {
                Init();

                return new HudMainMembers()
                {
                    Item1 = _instance.root.GetApiData(),
                    Item2 = _instance.cursor.GetApiData(),
                    Item3 = () => _instance.screenWidth,
                    Item4 = () => _instance.screenHeight,
                    Item5 = () => _instance.aspectRatio,
                    Item6 = new MyTuple<Func<float>, Func<float>, Func<float>, MyTuple<Func<IList<RichStringMembers>>, Action<IList<RichStringMembers>>>, Func<TextBoardMembers>, ApiMemberAccessor>
                    {
                        Item1 = () => ResScale,
                        Item2 = () => _instance.fov,
                        Item3 = () => _instance.fovScale,
                        Item4 = new MyTuple<Func<IList<RichStringMembers>>, Action<IList<RichStringMembers>>>(() => ClipBoard.ApiData, x => ClipBoard = new RichText(x)),
                        Item5 = GetTextBoardData,
                        Item6 = GetOrSetMember
                    }
                };
            }

            /// <summary>
            /// Root parent for all hud elements.
            /// </summary>
            private sealed class HudRoot : HudParentBase
            {
                public override bool Visible => true;

                public HudRoot() : base()
                { }
            }
        }
    }

    namespace UI.Client
    { }
}
