using RichHudFramework.Internal;
using RichHudFramework.UI.Rendering;
using Sandbox.ModAPI;
using System;
using VRage;
using VRageMath;
using HudSpaceDelegate = System.Func<VRage.MyTuple<bool, float, VRageMath.MatrixD>>;

namespace RichHudFramework
{
    namespace UI.Server
    {
        public sealed partial class HudMain : RichHudParallelComponentBase
        {
            public const int tickResetInterval = 240;
            private const byte WindowBaseOffset = 1, WindowMaxOffset = 250;
            private const int treeRefreshRate = 5;

            /// <summary>
            /// Root parent for all HUD elements.
            /// </summary>
            public static HudParentBase Root => instance._root;

            /// <summary>
            /// Root node for high DPI scaling at > 1080p. Draw matrix automatically rescales to comensate
            /// for decrease in apparent size due to high DPI displays.
            /// </summary>
            public static HudParentBase HighDpiRoot => instance._highDpiRoot;

            /// <summary>
            /// Cursor shared between mods.
            /// </summary>
            public static ICursor Cursor => instance._cursor;

            /// <summary>
            /// Shared clipboard.
            /// </summary>
            public static RichText ClipBoard
            {
                get
                {
                    if (instance == null)
                        Init();

                    if (instance._clipBoard == null)
                        instance._clipBoard = new RichText();

                    return instance._clipBoard?.GetCopy();
                }
                set
                {
                    if (instance == null)
                        Init();

                    instance._clipBoard = new RichText(value);
                }
            }

            /// <summary>
            /// Resolution scale normalized to 1080p for resolutions over 1080p. Returns a scale of 1f
            /// for lower resolutions.
            /// </summary>
            public static float ResScale { get; private set; }

            /// <summary>
            /// Matrix used to convert from 2D pixel-value screen space coordinates to worldspace.
            /// </summary>
            public static MatrixD PixelToWorld => PixelToWorldRef[0];

            /// <summary>
            /// Matrix used to convert from 2D pixel-value screen space coordinates to worldspace.
            /// </summary>
            public static MatrixD[] PixelToWorldRef { get; }

            /// <summary>
            /// The current horizontal screen resolution in pixels.
            /// </summary>
            public static float ScreenWidth { get; private set; }

            /// <summary>
            /// The current vertical resolution in pixels.
            /// </summary>
            public static float ScreenHeight { get; private set; }

            /// <summary>
            /// The current field of view
            /// </summary>
            public static float Fov { get; private set; }

            /// <summary>
            /// The current aspect ratio (ScreenWidth/ScreenHeight).
            /// </summary>
            public static float AspectRatio { get; private set; }

            /// <summary>
            /// Scaling used by MatBoards to compensate for changes in apparent size and position as a result
            /// of changes to Fov.
            /// </summary>
            public static float FovScale { get; private set; }

            /// <summary>
            /// The current opacity for the in-game menus as configured.
            /// </summary>
            public static float UiBkOpacity { get; private set; }

            /// <summary>
            /// If true then the cursor will be visible while chat is open
            /// </summary>
            public static bool EnableCursor;

            /// <summary>
            /// Current input mode. Used to indicate whether UI elements should accept cursor or text input.
            /// </summary>
            public static HudInputMode InputMode { get; private set; }

            /// <summary>
            /// Tick interval at which the UI tree updates. Lower is faster, higher is slower.
            /// </summary>
            public static int TreeRefreshRate { get; }

            private static HudMain instance;
            private static TreeManager treeManager;

            private readonly HudCursor _cursor;
            private readonly HudRoot _root;
            private readonly ScaledSpaceNode _highDpiRoot;

            private RichText _clipBoard;

            private Action<byte> LoseFocusCallback;
            private Action LoseInputFocusCallback;
            private byte unfocusedOffset;
            private int drawTick;

            static HudMain()
            {
                TreeRefreshRate = treeRefreshRate;
                PixelToWorldRef = new MatrixD[1];
            }

            private HudMain() : base(false, true)
            {
                if (instance == null)
                    instance = this;
                else
                    throw new Exception("Only one instance of HudMain can exist at any given time.");

                _root = new HudRoot();
                _highDpiRoot = new ScaledSpaceNode(_root) { UpdateScaleFunc = () => ResScale };
                _cursor = new HudCursor();

                UpdateScreenScaling();
                TreeManager.Init();
            }

            public static void Init()
            {
                BillBoardUtils.Init();   

                if (instance == null)
                    new HudMain();
            }

            public override void Close()
            {
                InputMode = HudInputMode.NoInput;
                EnableCursor = false;
                instance = null;
                treeManager = null;
            }

            /// <summary>
            /// Draw UI elements
            /// </summary>
            public override void Draw()
            {
                EnqueueAction(() => 
                {
                    UpdateCache();
                    treeManager.Draw();
                    drawTick++;

                    if (drawTick == tickResetInterval)
                        drawTick = 0;

                    if (SharedBinds.Escape.IsNewPressed)
                        LoseInputFocusCallback?.Invoke();
                });
            }

            public override void HandleInput()
            {
                if (instance._cursor.DrawCursor)
                {
                    if (MyAPIGateway.Gui.ChatEntryVisible || MyAPIGateway.Gui.IsCursorVisible)
                        InputMode = HudInputMode.Full;
                    else
                        InputMode = HudInputMode.CursorOnly;
                }
                else
                    InputMode = HudInputMode.NoInput;

                if (InputMode == HudInputMode.CursorOnly)
                    BindManager.RequestTempBlacklist(SeBlacklistModes.MouseAndCam);

                // Reset cursor
                _cursor.Release();
                treeManager.HandleInput();
            }

            /// <summary>
            /// Updates cached values for screen scaling and fov.
            /// </summary>
            private void UpdateCache()
            {
                if (drawTick % 60 == 0)
                {
                    UpdateScreenScaling();
                    UiBkOpacity = MyAPIGateway.Session.Config.UIBkOpacity;
                }

                // Update screen to world matrix transform
                PixelToWorldRef[0] = new MatrixD
                {
                    M11 = (FovScale / ScreenHeight),
                    M22 = (FovScale / ScreenHeight),
                    M33 = 1d,
                    M43 = -MyAPIGateway.Session.Camera.NearPlaneDistance,
                    M44 = 1d
                };
                
                PixelToWorldRef[0] *= MyAPIGateway.Session.Camera.WorldMatrix;
                Vector2 screenPos;

                if (MyAPIGateway.Input.IsJoystickLastUsed)
                {
                    Vector2 halfScreen = 0.5f * new Vector2(ScreenWidth, ScreenHeight);
                    Vector2 gpDelta = new Vector2
                    { 
                        X = SharedBinds.RightStickX.AnalogValue, 
                        Y = SharedBinds.RightStickY.AnalogValue
                    } * 10f * ResScale;

                    screenPos = _cursor.ScreenPos + gpDelta;
                    screenPos = Vector2.Clamp(screenPos, -halfScreen, halfScreen);
                    screenPos -= new Vector2(-ScreenWidth * .5f, ScreenHeight * .5f);
                    screenPos.Y *= -1f;
                }
                else
                {
                    // Reverse scaling due to differences between rendering resolution and
                    // desktop resolution when running the game in windowed mode
                    Vector2 desktopSize = MyAPIGateway.Input.GetMouseAreaSize(),
                        invMousePosScale = new Vector2
                        {
                            X = ScreenWidth / desktopSize.X,
                            Y = ScreenHeight / desktopSize.Y,
                        };

                    screenPos = MyAPIGateway.Input.GetMousePosition() * invMousePosScale;
                }

                _cursor.UpdateCursorPos(screenPos, ref PixelToWorldRef[0]);
                _cursor.Visible = _cursor.DrawCursor && !MyAPIGateway.Gui.IsCursorVisible;
            }

            /// <summary>
            /// Updates scaling values used to compensate for resolution, aspect ratio and FOV.
            /// </summary>
            private void UpdateScreenScaling()
            {
                ScreenWidth = MyAPIGateway.Session.Camera.ViewportSize.X;
                ScreenHeight = MyAPIGateway.Session.Camera.ViewportSize.Y;
                AspectRatio = (ScreenWidth / ScreenHeight);
                ResScale = (ScreenHeight > 1080f) ? ScreenHeight / 1080f : 1f;

                Fov = MyAPIGateway.Session.Camera.FovWithZoom;
                FovScale = (float)(0.1f * Math.Tan(Fov / 2d));
            }

            /// <summary>
            /// Returns the ZOffset for focusing a window and registers a callback
            /// for when another object takes focus.
            /// </summary>
            public static byte GetFocusOffset(Action<byte> LoseFocusCallback)
            {
                if (instance == null)
                    Init();

                return instance.GetFocusOffsetInternal(LoseFocusCallback);
            }

            /// <summary>
            /// Registers a callback for UI elements taking input focus. Callback
            /// invoked when another element takes focus.
            /// </summary>
            public static void GetInputFocus(Action LoseFocusCallback)
            {
                if (LoseFocusCallback != null && (LoseFocusCallback != instance.LoseInputFocusCallback))
                {
                    instance.LoseInputFocusCallback?.Invoke();
                    instance.LoseInputFocusCallback = LoseFocusCallback;
                }
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
            /// Converts from a position in normalized screen space coordinates to a position in pixels.
            /// </summary>
            public static Vector2 GetPixelVector(Vector2 scaledVec)
            {
                if (instance == null)
                    Init();

                return new Vector2
                (
                    (int)(scaledVec.X * ScreenWidth),
                    (int)(scaledVec.Y * ScreenHeight)
                );
            }

            /// <summary>
            /// Converts from a coordinate given in pixels to a position in normalized units.
            /// </summary>
            public static Vector2 GetAbsoluteVector(Vector2 pixelVec)
            {
                if (instance == null)
                    Init();

                return new Vector2
                (
                    pixelVec.X / ScreenWidth,
                    pixelVec.Y / ScreenHeight
                );
            }

            /// <summary>
            /// Root parent for all hud elements.
            /// </summary>
            private sealed class HudRoot : HudParentBase, IReadOnlyHudSpaceNode
            {
                public bool DrawCursorInHudSpace { get; }

                public Vector3 CursorPos { get; private set; }

                public HudSpaceDelegate GetHudSpaceFunc { get; }

                public MatrixD PlaneToWorld => PlaneToWorldRef[0];

                public MatrixD[] PlaneToWorldRef { get; }

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

                    GetHudSpaceFunc = () => new MyTuple<bool, float, MatrixD>(true, 1f, PixelToWorldRef[0]);
                    GetNodeOriginFunc = () => PixelToWorldRef[0].Translation;
                    PlaneToWorldRef = PixelToWorldRef;
                }

                protected override void Layout()
                {
                    PlaneToWorldRef[0] = PixelToWorld;
                    CursorPos = new Vector3(Cursor.ScreenPos.X, Cursor.ScreenPos.Y, 0f);
                }
            }
        }
    }

    namespace UI.Client
    { }
}
