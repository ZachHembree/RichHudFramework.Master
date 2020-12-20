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
    using Server;
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
            Func<Vector3D>, // GetOrigin
            Action, // DepthTest
            Action, // HandleInput
            Action<bool>, // BeforeLayout
            Action // BeforeDraw
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
            private readonly List<HudUpdateAccessors> sortedUpdateAccessors;
            private readonly List<ulong> indexList;
            
            private readonly List<Action> depthTestActions;
            private readonly List<Action> inputActions;
            private readonly List<Action<bool>> layoutActions;
            private readonly List<Action> drawActions;

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
                sortedUpdateAccessors = new List<HudUpdateAccessors>(200);
                indexList = new List<ulong>(200);

                depthTestActions = new List<Action>(200);
                inputActions = new List<Action>(200);
                layoutActions = new List<Action<bool>>(200);
                drawActions = new List<Action>(200);

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

                depthTestActions.Clear();
                layoutActions.Clear();

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
                    depthTestActions.Add(accessors.Item3);
                }

                // Build layout list (without sorting)
                for (int n = 0; n < updateAccessors.Count; n++)
                {
                    HudUpdateAccessors accessors = updateAccessors[n];
                    layoutActions.Add(accessors.Item5);
                }

                RebuildSortedLists();
            }

            /// <summary>
            /// Sorts draw and input accessors first by distance, then by zOffset, then by index
            /// </summary>
            private void RebuildSortedLists()
            {
                indexList.Clear();
                sortedUpdateAccessors.Clear();
                inputActions.Clear();
                drawActions.Clear();

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

                // Sort in ascending order by zOffset
                indexList.Sort();

                for (int n = 0; n < indexList.Count; n++)
                {
                    int index = (int)(indexList[n] & indexMask);
                    sortedUpdateAccessors.Add(updateAccessors[index]);
                }

                indexList.Clear();
                Vector3D position = _pixelToWorld.Translation;

                // Rebuild index list and sort by distance
                for (int n = 0; n < sortedUpdateAccessors.Count; n++)
                {
                    HudUpdateAccessors accessors = sortedUpdateAccessors[n];
                    Vector3D nodeOrigin = accessors.Item2();
                    ulong index = (ulong)n,
                        distance = (ulong)Math.Min(Vector3D.DistanceSquared(nodeOrigin, position) * 8d, 4294967295d);

                    indexList.Add((distance << 32) | index);
                }

                // Sort in ascending order by distance
                indexList.Sort();

                // Build input list
                for (int n = 0; n < indexList.Count; n++)
                {
                    int index = (int)(indexList[n] & indexMask);
                    HudUpdateAccessors accessors = sortedUpdateAccessors[index];

                    inputActions.Add(accessors.Item4);
                }

                // Build draw list
                for (int n = 0; n < indexList.Count; n++)
                {
                    int index = (int)(indexList[n] & indexMask);
                    HudUpdateAccessors accessors = sortedUpdateAccessors[index];

                    drawActions.Add(accessors.Item6);
                }
            }

            /// <summary>
            /// Draw UI elements
            /// </summary>
            public override void Draw()
            {
                RebuildSortedLists();

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
                    layoutActions[n](refresh);
                }

                // Draw UI elements
                for (int n = 0; n < drawActions.Count; n++)
                {
                    drawActions[n]();
                }

                tick++;

                if (tick == 30)
                    tick = 0;
            }

            /// <summary>
            /// Updates input for UI elements
            /// </summary>
            public override void HandleInput()
            {
                // Reset cursor
                _cursor.Release();

                // Update input for UI elements front to back
                for (int n = 0; n < depthTestActions.Count; n++)
                {
                    depthTestActions[n]();
                }

                for (int n = inputActions.Count - 1; n >= 0; n--)
                {
                    inputActions[n]();
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
            private sealed class HudRoot : HudParentBase, IReadOnlyHudSpaceNode
            {
                public override bool Visible => true;

                public bool DrawCursorInHudSpace => true;

                public override IReadOnlyHudSpaceNode HudSpace => this;

                public Vector3 CursorPos => new Vector3(Cursor.ScreenPos.X, Cursor.ScreenPos.Y, 0f);

                public HudSpaceDelegate GetHudSpaceFunc { get; }

                public MatrixD PlaneToWorld => PixelToWorld;

                public Func<MatrixD> UpdateMatrixFunc => null;

                public Func<Vector3D> GetNodeOriginFunc { get; }

                public HudRoot() : base()
                {
                    GetHudSpaceFunc = () => new MyTuple<bool, float, MatrixD>(true, 1f, PixelToWorld);
                    GetNodeOriginFunc = () => PixelToWorld.Translation;
                }
            }
        }
    }

    namespace UI.Client
    { }
}
