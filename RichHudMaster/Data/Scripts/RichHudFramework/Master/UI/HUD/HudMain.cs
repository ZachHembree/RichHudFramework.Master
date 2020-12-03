using RichHudFramework.Internal;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage;
using VRageMath;
using ApiMemberAccessor = System.Func<object, int, object>;
using FloatProp = VRage.MyTuple<System.Func<float>, System.Action<float>>;
using HudDrawDelegate = System.Func<object, object>;
using HudLayoutDelegate = System.Func<bool, bool>;
using HudSpaceDelegate = System.Func<VRage.MyTuple<bool, float, VRageMath.MatrixD>>;
using RichStringMembers = VRage.MyTuple<System.Text.StringBuilder, VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>>;
using Vec2Prop = VRage.MyTuple<System.Func<VRageMath.Vector2>, System.Action<VRageMath.Vector2>>;

namespace RichHudFramework
{
    using Server;
    using HudInputDelegate = Func<Vector3, HudSpaceDelegate, MyTuple<Vector3, HudSpaceDelegate>>;
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
            ushort, // ZOffset
            byte, // Depth
            HudInputDelegate, // DepthTest
            HudInputDelegate, // HandleInput
            HudLayoutDelegate, // BeforeLayout
            HudDrawDelegate // BeforeDraw
        >;

        public sealed partial class HudMain : RichHudComponentBase
        {
            public const byte WindowBaseOffset = 1, WindowMaxOffset = 250;

            /// <summary>
            /// Root parent for all HUD elements.
            /// </summary>
            public static HudParentBase Root
            {
                get
                {
                    if (_instance == null)
                        Init();

                    return _instance._root;
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

                    return _instance._cursor;
                }
            }

            /// <summary>
            /// Shared clipboard.
            /// </summary>
            public static RichText ClipBoard
            {
                get { return Instance._clipBoard ?? new RichText(); }
                set { Instance._clipBoard = value; }
            }

            /// <summary>
            /// Resolution scale normalized to 1080p for resolutions over 1080p. Returns a scale of 1f
            /// for lower resolutions.
            /// </summary>
            public static float ResScale
            {
                get
                {
                    if (_instance == null)
                        Init();

                    return _instance._resScale;
                }
            }

            /// <summary>
            /// Matrix used to convert from 2D pixel-value screen space coordinates to worldspace.
            /// </summary>
            public static MatrixD PixelToWorld
            {
                get
                {
                    if (_instance == null)
                        Init();

                    return _instance._pixelToWorld;
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

                    return _instance._screenWidth;
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

                    return _instance._screenHeight;
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

                    return _instance._fov;
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

                    return _instance._aspectRatio;
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

                    return _instance._fovScale;
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

                    return _instance._uiBkOpacity;
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

            private static HudMain Instance
            {
                get { Init(); return _instance; }
            }
            private static HudMain _instance;

            private readonly HudCursor _cursor;
            private readonly HudRoot _root;
            private readonly List<Client> hudClients;

            private readonly List<HudUpdateAccessors> updateAccessors;
            private readonly List<ulong> indexList;
            
            private readonly List<MyTuple<byte, HudInputDelegate>> depthTestActions;
            private readonly List<MyTuple<byte, HudInputDelegate>> inputActions;
            private readonly List<MyTuple<byte, HudLayoutDelegate>> layoutActions;
            private readonly List<MyTuple<byte, HudDrawDelegate>> drawActions;

            private RichText _clipBoard;
            private float _resScale;
            private float _uiBkOpacity;

            private readonly Utils.Stopwatch cacheTimer;
            private MatrixD _pixelToWorld;
            private float _screenWidth;
            private float _screenHeight;
            private float _aspectRatio;
            private float _fov;
            private float _fovScale;
            private int tick;

            private Action<byte> LoseFocusCallback;
            private byte unfocusedOffset;

            private HudMain() : base(false, true)
            {
                _root = new HudRoot();
                _cursor = new HudCursor(_root);
                hudClients = new List<Client>();

                updateAccessors = new List<HudUpdateAccessors>(200);
                indexList = new List<ulong>(200);

                depthTestActions = new List<MyTuple<byte, HudInputDelegate>>(200);
                inputActions = new List<MyTuple<byte, HudInputDelegate>>(200);
                layoutActions = new List<MyTuple<byte, HudLayoutDelegate>>(200);
                drawActions = new List<MyTuple<byte, HudDrawDelegate>>(200);

                cacheTimer = new Utils.Stopwatch();
                cacheTimer.Start();
            }

            public static void Init()
            {
                if (_instance == null)
                {
                    _instance = new HudMain();
                    _instance.UpdateScreenScaling();
                }
            }

            public override void Close()
            {
                _instance = null;
            }

            public override void Draw()
            {
                for (int n = 0; n < hudClients.Count; n++)
                {
                    if (hudClients[n].RefreshDrawList)
                        RefreshDrawList = true;

                    hudClients[n].RefreshDrawList = false;
                }

                _cursor.Visible = EnableCursor;

                for (int n = 0; n < hudClients.Count; n++)
                {
                    if (hudClients[n].EnableCursor)
                        _cursor.Visible = true;
                }

                UpdateCache();

                // Update layout
                bool refresh = tick == 0;

                for (int n = 0; n < layoutActions.Count; n++)
                {
                    uint treeDepth = layoutActions[n].Item1;
                    HudLayoutDelegate LayoutFunc = layoutActions[n].Item2;

                    if (treeDepth == 0)
                        refresh = tick == 0;

                    refresh = LayoutFunc(refresh);
                }

                // Draw UI elements
                object matrix = _pixelToWorld;

                for (int n = 0; n < drawActions.Count; n++)
                {
                    uint treeDepth = drawActions[n].Item1;
                    HudDrawDelegate DrawFunc = drawActions[n].Item2;

                    if (treeDepth == 0)
                        matrix = _pixelToWorld;

                    matrix = DrawFunc(matrix);
                }

                tick++;

                if (tick == 30)
                    tick = 0;
            }

            /// <summary>
            /// Updates cached values for screen scaling, fov and maintains UI update lists.
            /// </summary>
            private void UpdateCache()
            {
                if (cacheTimer.ElapsedMilliseconds > 2000)
                {
                    UpdateScreenScaling();

                    _uiBkOpacity = MyAPIGateway.Session.Config.UIBkOpacity;
                    cacheTimer.Reset();
                }

                // Update screen to world matrix transform
                _pixelToWorld = new MatrixD
                {
                    M11 = (FovScale / ScreenHeight),
                    M12 = 0d,
                    M13 = 0d,
                    M14 = 0d,
                    M21 = 0d,
                    M22 = (FovScale / ScreenHeight),
                    M23 = 0d,
                    M24 = 0d,
                    M31 = 0d,
                    M32 = 0d,
                    M33 = 1d,
                    M34 = 0d,
                    M41 = 0d,
                    M42 = 0d,
                    M43 = -.05d,
                    M44 = 1d
                };

                _pixelToWorld *= MyAPIGateway.Session.Camera.WorldMatrix;

                if (RefreshDrawList)
                {
                    RebuildUpdateLists();
                    RefreshDrawList = false;
                }
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
            /// Rebuilds update accessor lists
            /// </summary>
            private void RebuildUpdateLists()
            {
                // Clear update lists and rebuild accessor lists from HUD tree
                updateAccessors.Clear();
                indexList.Clear();

                depthTestActions.Clear();
                inputActions.Clear();
                layoutActions.Clear();
                drawActions.Clear();

                // Add client UI elements
                for (int n = 0; n < hudClients.Count; n++)
                    hudClients[n].GetUpdateAccessors?.Invoke(updateAccessors, 0);

                // Add master UI elements
                _root.GetUpdateAccessors(updateAccessors, 0);

                indexList.EnsureCapacity(updateAccessors.Count);

                depthTestActions.EnsureCapacity(updateAccessors.Count);
                inputActions.EnsureCapacity(updateAccessors.Count);
                layoutActions.EnsureCapacity(updateAccessors.Count);
                drawActions.EnsureCapacity(updateAccessors.Count);

                // Build depth test list (without sorting)
                for (int n = 0; n < updateAccessors.Count; n++)
                {
                    HudUpdateAccessors accessors = updateAccessors[n];
                    depthTestActions.Add(new MyTuple<byte, HudInputDelegate>(accessors.Item2, accessors.Item3));
                }

                // Build layout list (without sorting)
                for (int n = 0; n < updateAccessors.Count; n++)
                {
                    HudUpdateAccessors accessors = updateAccessors[n];
                    layoutActions.Add(new MyTuple<byte, HudLayoutDelegate>(accessors.Item2, accessors.Item5));
                }

                // Lower 32 bits store the index, upper 32 store draw depth 
                ulong indexMask = 0x00000000FFFFFFFF;

                // Build index list and sort by zOffset
                for (int n = 0; n < updateAccessors.Count; n++)
                {
                    HudUpdateAccessors accessors = updateAccessors[n];
                    ulong index = (ulong)n,
                        zOffset = accessors.Item1;

                    indexList.Add((zOffset << 32) | index);
                }

                // Sort in ascending order
                indexList.Sort();

                // Build input list
                for (int n = 0; n < indexList.Count; n++)
                {
                    int index = (int)(indexList[n] & indexMask);
                    HudUpdateAccessors accessors = updateAccessors[index];

                    inputActions.Add(new MyTuple<byte, HudInputDelegate>(accessors.Item2, accessors.Item4));
                }

                // Build draw list
                for (int n = 0; n < indexList.Count; n++)
                {
                    int index = (int)(indexList[n] & indexMask);
                    HudUpdateAccessors accessors = updateAccessors[index];

                    drawActions.Add(new MyTuple<byte, HudDrawDelegate>(accessors.Item2, accessors.Item6));
                }
            }

            /// <summary>
            /// Updates input for UI elements
            /// </summary>
            public override void HandleInput()
            {
                // Reset cursor
                _cursor.Release();

                // Update input for UI elements front to back
                Vector3 cursorPos = new Vector3(_cursor.ScreenPos.X, _cursor.ScreenPos.Y, 0f);
                HudSpaceDelegate DefaultHudSpaceFunc = () => new MyTuple<bool, float, MatrixD>(true, 1f, _pixelToWorld);
                var inputData = new MyTuple<Vector3, HudSpaceDelegate>(cursorPos, DefaultHudSpaceFunc);

                for (int n = 0; n < depthTestActions.Count; n++)
                {
                    uint treeDepth = depthTestActions[n].Item1;
                    HudInputDelegate InputFunc = depthTestActions[n].Item2;

                    if (treeDepth == 0)
                        inputData = new MyTuple<Vector3, HudSpaceDelegate>(cursorPos, DefaultHudSpaceFunc);

                    inputData = InputFunc(inputData.Item1, inputData.Item2);
                }

                inputData = new MyTuple<Vector3, HudSpaceDelegate>(cursorPos, DefaultHudSpaceFunc);

                for (int n = inputActions.Count - 1; n >= 0; n--)
                {
                    uint treeDepth = inputActions[n].Item1;
                    HudInputDelegate InputFunc = inputActions[n].Item2;

                    if (treeDepth == 0)
                        inputData = new MyTuple<Vector3, HudSpaceDelegate>(cursorPos, DefaultHudSpaceFunc);

                    inputData = InputFunc(inputData.Item1, inputData.Item2);
                }
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
            public static byte GetFocusOffset(Action<byte> LoseFocusCallback) =>
                Instance.GetFocusOffsetInternal(LoseFocusCallback);

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

                    HudMain.RefreshDrawList = true;
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
                if (_instance == null)
                    Init();

                return new Vector2
                (
                    (int)(scaledVec.X * _instance._screenWidth),
                    (int)(scaledVec.Y * _instance._screenHeight)
                );
            }

            /// <summary>
            /// Converts from a coordinate given in pixels to a position in absolute units.
            /// </summary>
            public static Vector2 GetAbsoluteVector(Vector2 pixelVec)
            {
                if (_instance == null)
                    Init();

                return new Vector2
                (
                    pixelVec.X / _instance._screenWidth,
                    pixelVec.Y / _instance._screenHeight
                );
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
