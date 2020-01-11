using RichHudFramework.Game;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage;
using VRage.Game.ModAPI;
using VRageMath;
using FloatProp = VRage.MyTuple<System.Func<float>, System.Action<float>>;
using RichStringMembers = VRage.MyTuple<System.Text.StringBuilder, VRage.MyTuple<VRageMath.Vector2I, int, VRageMath.Color, float>>;
using Vec2Prop = VRage.MyTuple<System.Func<VRageMath.Vector2>, System.Action<VRageMath.Vector2>>;
using ApiMemberAccessor = System.Func<object, int, object>;

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
            ApiMemberAccessor
        >
    >;
    using HudElementMembers = MyTuple<
        Func<bool>, // Visible
        object, // ID
        Action, // BeforeDrawStart
        Action, // DrawStart
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
            Action<RichStringMembers, Vector2I>, // Insert
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
                ApiMemberAccessor
            >
        >;

        public sealed partial class HudMain : ModBase.ComponentBase
        {
            public static IHudParent Root => Instance.root;
            public static ICursor Cursor => Instance.cursor;
            public static RichText ClipBoard { get; set; }
            public static float ResScale { get; private set; }
            public static UiTestPattern TestPattern { get; private set; }
            public static float ScreenWidth => Instance.screenWidth;
            public static float ScreenHeight => Instance.screenHeight;
            public static float Fov => Instance.fov;
            public static float AspectRatio => Instance.aspectRatio;
            public static float InvTextApiScale => Instance.invTextApiScale;
            public static float FovScale => Instance.fovScale;
            public static float UiBkOpacity => Instance.uiBkOpacity;

            private static HudMain Instance
            {
                get { Init(); return instance; }
                set { instance = value; }
            }
            private static HudMain instance;
            private readonly HudRoot root;
            private readonly HudCursor cursor;
            private readonly Utils.Stopwatch cacheTimer;
            private float screenWidth, screenHeight, aspectRatio, invTextApiScale, fov, fovScale, uiBkOpacity;

            private HudMain() : base(false, true)
            {
                UpdateResScaling();
                UpdateFovScaling();

                root = new HudRoot();
                cursor = new HudCursor();

                cacheTimer = new Utils.Stopwatch();
                cacheTimer.Start();
            }

            public static void Init()
            {
                if (instance == null)
                {
                    instance = new HudMain();

                    instance.cursor.Visible = true;
                    TestPattern = new UiTestPattern();
                    TestPattern.Hide();
                }
            }

            public override void Close()
            {
                Instance = null;
            }

            public override void Draw()
            {
                if (cacheTimer.ElapsedMilliseconds > 1000)
                {
                    if (screenHeight != MyAPIGateway.Session.Camera.ViewportSize.Y || screenWidth != MyAPIGateway.Session.Camera.ViewportSize.X)
                        UpdateResScaling();

                    if (fov != MyAPIGateway.Session.Camera.FovWithZoom)
                        UpdateFovScaling();

                    uiBkOpacity = MyAPIGateway.Session.Config.UIBkOpacity;
                    cacheTimer.Reset();
                }

                root.BeforeDrawStart();
                root.DrawStart();

                cursor.Draw();
            }

            public override void HandleInput()
            {
                cursor.Release();
                root.HandleInputStart();
                cursor.HandleInput();
            }

            private void UpdateResScaling()
            {
                screenWidth = MyAPIGateway.Session.Camera.ViewportSize.X;
                screenHeight = MyAPIGateway.Session.Camera.ViewportSize.Y;
                aspectRatio = (screenWidth / screenHeight);

                invTextApiScale = 1080f / screenHeight;
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
                scaledVec /= 2f;

                return new Vector2
                (
                    (int)(scaledVec.X * Instance.screenWidth),
                    (int)(scaledVec.Y * Instance.screenHeight)
                );
            }

            /// <summary>
            /// Converts from a coordinate given in pixels to a scaled system independent of screen resolution.
            /// </summary>
            public static Vector2 GetRelativeVector(Vector2 pixelVec)
            {
                pixelVec *= 2f;

                return new Vector2
                (
                    pixelVec.X / Instance.screenWidth,
                    pixelVec.Y / Instance.screenHeight
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
                    Item1 = instance.root.GetApiData(),
                    Item2 = instance.cursor.GetApiData(),
                    Item3 = () => instance.screenWidth,
                    Item4 = () => instance.screenHeight,
                    Item5 = () => instance.aspectRatio,
                    Item6 = new MyTuple<Func<float>, Func<float>, Func<float>, MyTuple<Func<IList<RichStringMembers>>, Action<IList<RichStringMembers>>>, Func<TextBoardMembers>, ApiMemberAccessor>
                    {
                        Item1 = () => ResScale,
                        Item2 = () => instance.fov,
                        Item3 = () => instance.fovScale,
                        Item4 = new MyTuple<Func<IList<RichStringMembers>>, Action<IList<RichStringMembers>>>(() => ClipBoard?.GetApiData(), x => ClipBoard = new RichText(x)),
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
