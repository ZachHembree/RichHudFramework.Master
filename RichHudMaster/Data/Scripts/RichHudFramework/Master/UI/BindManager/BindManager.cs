using RichHudFramework.Internal;
using RichHudFramework.Server;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage;
using VRage.Game.ModAPI;
using VRage.Input;
using VRage.Utils;
using VRageMath;
using IMyControllableEntity = VRage.Game.ModAPI.Interfaces.IMyControllableEntity;

namespace RichHudFramework
{
    namespace UI.Server
    {
        /// <summary>
        /// Manages custom keybinds; singleton
        /// </summary>
        public sealed partial class BindManager : RichHudComponentBase
        {
            /// <summary>
            /// Read-only collection of bind groups registered to the main client
            /// </summary>
            public static IReadOnlyList<IBindGroup> Groups => _instance?.mainClient.Groups;

            /// <summary>
            /// Read-only collection of all available controls for use with key binds
            /// </summary>
            public static IReadOnlyList<ControlHandle> Controls => _instance?.controlHandles;

            /// <summary>
            /// Read-only list of registered bind clients
            /// </summary>
            public static IReadOnlyList<Client> Clients => _instance?.bindClients;

            /// <summary>
            /// Bind client used by RHM
            /// </summary>
            public static Client MainClient => _instance?.mainClient;

            /// <summary>
            /// Specifies blacklist mode for SE controls
            /// </summary>
            public static SeBlacklistModes BlacklistMode
            {
                get { if (_instance == null) Init(); return _instance.mainClient.RequestedBlacklistMode; }
                set
                {
                    if (_instance == null)
                        Init();

                    _instance.mainClient.RequestedBlacklistMode = value;
                }
            }

            public static SeBlacklistModes CurrentBlacklistMode { get; private set; }

            /// <summary>
            /// MyAPIGateway.Gui.ChatEntryVisible, but actually usable for input polling
            /// </summary>
            public static bool IsChatOpen { get; private set; }

            /// <summary>
            /// Read-only list of all controls bound to a key
            /// </summary>
            public static IReadOnlyList<string> SeControlIDs { get { if (_instance == null) Init(); return _instance.seControlIDs; } }

            /// <summary>
            /// Read-only list of all controls bound to a mouse key
            /// </summary>
            public static IReadOnlyList<string> SeMouseControlIDs { get { if (_instance == null) Init(); return _instance.seMouseControlIDs; } }

            private static BindManager Instance
            {
                get { Init(); return _instance; }
                set { _instance = value; }
            }
            private static BindManager _instance;

            private static readonly HashSet<ControlHandle> controlBlacklist = new HashSet<ControlHandle>()
            {
                MyKeys.None,
                MyKeys.LeftAlt,
                MyKeys.RightAlt,
                MyKeys.LeftShift,
                MyKeys.RightShift,
                MyKeys.LeftControl,
                MyKeys.RightControl,
                MyKeys.LeftWindows,
                MyKeys.RightWindows,
                MyJoystickButtonsEnum.None
            };

            private static readonly IReadOnlyDictionary<ControlHandle, ControlHandle[]> controlAliases = new Dictionary<ControlHandle, ControlHandle[]>
            {
                { MyKeys.Alt, new ControlHandle[] { MyKeys.LeftAlt, MyKeys.RightAlt } },
                { MyKeys.Shift, new ControlHandle[] { MyKeys.LeftShift, MyKeys.RightShift } },
                { MyKeys.Control, new ControlHandle[] { MyKeys.LeftControl, MyKeys.RightControl } }
            };

            private static readonly IReadOnlyDictionary<MyJoystickButtonsEnum, string> gamepadBtnCodes = new Dictionary<MyJoystickButtonsEnum, string>
            {
                { MyJoystickButtonsEnum.J01, ((char)0xE001).ToString() }, // A
                { MyJoystickButtonsEnum.J02, ((char)0xE003).ToString() }, // B
                { MyJoystickButtonsEnum.J03, ((char)0xE002).ToString() }, // X
                { MyJoystickButtonsEnum.J04, ((char)0xE004).ToString() }, // Y
                { MyJoystickButtonsEnum.J05, ((char)0xE005).ToString() }, // LB
                { MyJoystickButtonsEnum.J06, ((char)0xE006).ToString() }, // RB
                { MyJoystickButtonsEnum.J07, ((char)0xE00D).ToString() }, // View
                { MyJoystickButtonsEnum.J08, ((char)0xE00E).ToString() }, // Menu
                { MyJoystickButtonsEnum.J09, ((char)0xE00B).ToString() }, // Left Stick Btn
                { MyJoystickButtonsEnum.J10, ((char)0xE00C).ToString() }, // Right Stick Btn

                { MyJoystickButtonsEnum.JDUp, ((char)0xE011).ToString() }, // D-Pad Up
                { MyJoystickButtonsEnum.JDLeft, ((char)0xE010).ToString() }, // D-Pad Left
                { MyJoystickButtonsEnum.JDRight, ((char)0xE012).ToString() }, // D-Pad Right
                { MyJoystickButtonsEnum.JDDown, ((char)0xE013).ToString() }, // D-Pad Down
            };

            public static readonly IReadOnlyDictionary<RichHudControls, string> customConNames = new Dictionary<RichHudControls, string>
            {
                { RichHudControls.MousewheelUp, "MwUp" },
                { RichHudControls.MousewheelDown, "MwDn" },

                { RichHudControls.LeftStickLeft, ((char)0xE015).ToString() },
                { RichHudControls.LeftStickRight, ((char)0xE016).ToString() },
                { RichHudControls.LeftStickUp, ((char)0xE017).ToString() },
                { RichHudControls.LeftStickDown, ((char)0xE014).ToString() },

                { RichHudControls.LeftStickX, ((char)0xE022).ToString() },
                { RichHudControls.LeftStickY, ((char)0xE023).ToString() },

                { RichHudControls.RightStickLeft, ((char)0xE019).ToString() },
                { RichHudControls.RightStickRight, ((char)0xE020).ToString() },
                { RichHudControls.RightStickUp, ((char)0xE021).ToString() },
                { RichHudControls.RightStickDown, ((char)0xE018).ToString() },

                { RichHudControls.RightStickX, ((char)0xE024).ToString() },
                { RichHudControls.RightStickY, ((char)0xE025).ToString() },

                { RichHudControls.RightTrigger, ((char)0xE007).ToString() },
                { RichHudControls.LeftTrigger, ((char)0xE008).ToString() },
            };

            private readonly Control[] controls;
            private readonly List<ControlHandle> controlHandles;
            private readonly string[] seControlIDs, seMouseControlIDs;
            private readonly Dictionary<string, IControl> controlDict, controlDictFriendly;
            private readonly List<Client> bindClients;
            private readonly List<int> conIDbuf;
            private readonly MyTuple<List<int>, List<List<int>>> cHandleBuf;
            private readonly List<BindDefinition> bindDefBuf;

            private Client mainClient;
            private bool areControlsBlacklisted, areMouseControlsBlacklisted;
            private int chatInputTick;
            private MySpectatorCameraMovementEnum? lastSpecMode;
            private Vector2 lastSpecSpeeds;

            private BindManager() : base(false, true)
            {
                var kbmKeys = Enum.GetValues(typeof(MyKeys)) as MyKeys[];
                var gpKeys = Enum.GetValues(typeof(MyJoystickButtonsEnum)) as MyJoystickButtonsEnum[];
                int conCount = ControlHandle.GPKeysStart + (int)MyJoystickButtonsEnum.J16 + 1;

                controls = new Control[conCount];
                controlDict = new Dictionary<string, IControl>(conCount);
                controlDictFriendly = new Dictionary<string, IControl>(conCount);

                GenerateControls(kbmKeys, gpKeys);
                GetControlStringIDs(kbmKeys, out seControlIDs, out seMouseControlIDs);

                controlHandles = new List<ControlHandle>(conCount);

                for (int i = 0; i < controls.Length; i++)
                {
                    if (i == controls[i].Index)
                        controlHandles.Add(new ControlHandle(i));
                }

                bindClients = new List<Client>();
                conIDbuf = new List<int>();
                cHandleBuf = new MyTuple<List<int>, List<List<int>>>(new List<int>(), new List<List<int>>());
                bindDefBuf = new List<BindDefinition>();
            }

            public static void Init()
            {
                if (_instance == null)
                    _instance = new BindManager();
                else if (_instance.Parent == null)
                    _instance.RegisterComponent(RichHudCore.Instance);

                if (_instance.mainClient == null)
                {
                    RichHudCore.LateMessageEntered += _instance.HandleChatBlacklist;
                    _instance.mainClient = new Client();
                }
            }

            public override void HandleInput()
            {
                UpdateControls();
                UpdateBlacklist();

                // Update client input
                if (!RebindDialog.Open)
                {
                    for (int n = 0; n < bindClients.Count; n++)
                        bindClients[n].HandleInput();
                }

                // Synchronize with actual value every second or so
                if (chatInputTick == 0)
                {
                    IsChatOpen = MyAPIGateway.Gui.ChatEntryVisible;
                }

                if (MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.None)
                {
                    if (IsChatOpen && SharedBinds.Enter.IsNewPressed)
                    {
                        IsChatOpen = false;
                        chatInputTick = 0;
                    }
                    else if (MyAPIGateway.Input.IsNewGameControlPressed(MyStringId.Get("CHAT_SCREEN")))
                    {
                        IsChatOpen = true;
                        chatInputTick = 0;
                    }
                }

                chatInputTick++;
                chatInputTick %= 60;
            }

            private void UpdateControls()
            {
                for (int i = 0; i < controls.Length; i++)
                {
                    var con = controls[i];

                    if (con != Control.Default && con.Index == i)
                    {
                        con.Update();
                    }
                }
            }

            private void UpdateBlacklist()
            {
                CurrentBlacklistMode = SeBlacklistModes.None;
                IMyControllableEntity conEnt = MyAPIGateway.Session.ControlledObject;
                var specCon = MyAPIGateway.Session.CameraController as MySpectator;

                for (int n = 0; n < bindClients.Count; n++)
                {
                    CurrentBlacklistMode |= bindClients[n].RequestedBlacklistMode;
                }

                // Block/allow camera rotation due to user input
                if ((CurrentBlacklistMode & SeBlacklistModes.CameraRot) > 0)
                {
                    if (specCon != null)
                    {
                        if (lastSpecMode == null)
                            lastSpecMode = specCon.SpectatorCameraMovement;

                        specCon.SpectatorCameraMovement = MySpectatorCameraMovementEnum.None;
                        specCon.SpeedModeAngular = lastSpecSpeeds.X;
                        specCon.SpeedModeLinear = lastSpecSpeeds.Y;
                    }

                    if (conEnt != null)
                    {
                        if (specCon != null)
                            conEnt.MoveAndRotate(Vector3.Zero, Vector2.Zero, 0f);
                        else
                            conEnt.MoveAndRotate(conEnt.LastMotionIndicator, Vector2.Zero, 0f);
                    }
                }
                else
                {
                    if (lastSpecMode != null)
                    {
                        if (specCon != null)
                        {
                            specCon.SpectatorCameraMovement = lastSpecMode.Value;
                            specCon.SpeedModeAngular = lastSpecSpeeds.X;
                            specCon.SpeedModeLinear = lastSpecSpeeds.Y;
                        }

                        lastSpecMode = null;
                    }
                    else if (specCon != null)
                        lastSpecSpeeds = new Vector2(specCon.SpeedModeAngular, specCon.SpeedModeLinear);
                }

                // Set control blacklist according to flag configuration
                if ((CurrentBlacklistMode & SeBlacklistModes.AllKeys) == SeBlacklistModes.AllKeys)
                {
                    if (!areControlsBlacklisted)
                    {
                        areControlsBlacklisted = true;
                        areMouseControlsBlacklisted = true;
                        SetBlacklist(seControlIDs, true); // Enable full blacklist
                    }
                }
                else
                {
                    if (areControlsBlacklisted)
                    {
                        SetBlacklist(seControlIDs, false); // Disable full blacklist
                        areControlsBlacklisted = false;
                        areMouseControlsBlacklisted = false;
                    }

                    if ((CurrentBlacklistMode & SeBlacklistModes.Mouse) > 0)
                    {
                        if (!areMouseControlsBlacklisted)
                        {
                            areMouseControlsBlacklisted = true;
                            SetBlacklist(seMouseControlIDs, true); // Enable mouse button blacklist
                        }
                    }
                    else if (areMouseControlsBlacklisted)
                    {
                        SetBlacklist(seMouseControlIDs, false); // Disable mouse button blacklist
                        areMouseControlsBlacklisted = false;
                    }
                }
            }

            private void HandleChatBlacklist(string message, ref bool sendToOthers)
            {
                if ((CurrentBlacklistMode & SeBlacklistModes.Chat) > 0)
                    sendToOthers = false;
            }

            private static void SetBlacklist(string[] blacklist, bool value)
            {
                BlacklistMessage message = new BlacklistMessage(blacklist, value);
                byte[] data;

                if (Utils.ProtoBuf.TrySerialize(message, out data) == null)
                    RhServer.SendActionToServer(ServerActions.SetBlacklist, data);
            }

            public override void Close()
            {
                bindClients.Clear();
                RichHudCore.LateMessageEntered -= HandleChatBlacklist;
                CurrentBlacklistMode = SeBlacklistModes.None;
                UpdateBlacklist();

                mainClient = null;

                if (ExceptionHandler.Unloading)
                    _instance = null;
            }

            /// <summary>
            /// Sets a temporary control blacklist cleared after every frame. Blacklists set via
            /// property will persist regardless.
            /// </summary>
            public static void RequestTempBlacklist(SeBlacklistModes mode)
            {
                Instance.mainClient.RequestTempBlacklist(mode);
            }

            /// <summary>
            /// Returns the bind group with the given name and/or creates one with the name given
            /// if one doesn't exist.
            /// </summary>
            public static IBindGroup GetOrCreateGroup(string name) =>
                Instance.mainClient.GetOrCreateGroup(name);

            /// <summary>
            /// Returns the bind group with the name igven.
            /// </summary>
            public static IBindGroup GetBindGroup(string name) =>
                Instance.mainClient.GetBindGroup(name);

            /// <summary>
            /// Returns the control associated with the given name.
            /// </summary>
            public static ControlHandle GetControl(string name)
            {
                Init();
                IControl con;

                if (_instance.controlDict.TryGetValue(name.ToLower(), out con))
                    return new ControlHandle(con.Index);
                else if (_instance.controlDictFriendly.TryGetValue(name.ToLower(), out con))
                    return new ControlHandle(con.Index);

                return new ControlHandle(0);
            }

            /// <summary>
            /// Returns the control associated with the given <see cref="ControlHandle"/>.
            /// </summary>
            public static IControl GetControl(ControlHandle handle) =>
                _instance?.controls[handle.id];

            /// <summary>
            /// Returns control name for the corresponding handle
            /// </summary>
            public static string GetControlName(ControlHandle con)
            {
                return _instance?.controls?[con.id]?.Name;
            }

            /// <summary>
            /// Returns control names for the corresponding handle list
            /// </summary>
            public static string[] GetControlNames(IReadOnlyList<ControlHandle> cons)
            {
                var combo = new string[cons.Count];

                for (int i = 0; i < cons.Count; i++)
                {
                    combo[i] = _instance?.controls?[cons[i].id]?.Name;
                }

                return combo;
            }

            /// <summary>
            /// Returns control name for the corresponding int ID
            /// </summary>
            public static string GetControlName(int conID)
            {
                return _instance?.controls?[conID]?.Name;
            }

            /// <summary>
            /// Returns control names for the corresponding int IDs
            /// </summary>
            public static string[] GetControlNames(IReadOnlyList<int> conIDs)
            {
                var combo = new string[conIDs.Count];

                for (int i = 0; i < conIDs.Count; i++)
                {
                    combo[i] = _instance?.controls?[conIDs[i]]?.Name;
                }

                return combo;
            }

            /// <summary>
            /// Builds dictionary of controls from the set of MyKeys enums and a couple custom controls for the mouse wheel.
            /// </summary>
            private void GenerateControls(MyKeys[] kbmKeys, MyJoystickButtonsEnum[] gpKeys)
            {
                // Initialize control list to default
                for (int i = 0; i < controls.Length; i++)
                    controls[i] = Control.Default;

                // Add kbd+mouse controls
                for (int i = 0; i < kbmKeys.Length; i++)
                {
                    var index = (int)kbmKeys[i];
                    var seKey = kbmKeys[i];

                    if (!controlBlacklist.Contains(seKey))
                    {
                        Control con = new Control(seKey, index);
                        string name = con.Name.ToLower(),
                            friendlyName = con.DisplayName.ToLower();

                        if (!controlDict.ContainsKey(name))
                        {
                            controlDict.Add(name, con);
                            controls[index] = con;
                        }

                        if (!controlDictFriendly.ContainsKey(friendlyName))
                            controlDictFriendly.Add(friendlyName, con);
                    }
                    else
                        controls[index] = Control.Default;
                }

                // Add gamepad keys
                for (int i = 0; i < gpKeys.Length; i++)
                {
                    var index = ControlHandle.GPKeysStart + (int)gpKeys[i] - 1;
                    var seKey = gpKeys[i];

                    if (!controlBlacklist.Contains(seKey))
                    {
                        Control con = new Control(seKey, index);
                        string name = con.Name.ToLower(),
                            friendlyName = con.DisplayName.ToLower();

                        if (!controlDict.ContainsKey(name))
                        {
                            controlDict.Add(name, con);
                            controls[index] = con;
                        }

                        if (!controlDictFriendly.ContainsKey(friendlyName))
                            controlDictFriendly.Add(friendlyName, con);
                    }
                    else
                        controls[index] = Control.Default;
                }

                GenerateCustomControls();

                // Map control aliases to appropriate controls
                foreach (KeyValuePair<ControlHandle, ControlHandle[]> controlAliasPair in controlAliases)
                {
                    Control con = controls[controlAliasPair.Key.id];

                    foreach (ControlHandle key in controlAliasPair.Value)
                        controls[key.id] = con;
                }
            }

            private void AddCustomControl(RichHudControls conEnum, Func<bool> IsPressed, Func<float> GetAnalogValue)
            {
                var con = new Control(conEnum, IsPressed, GetAnalogValue);
                controls[con.Index] = con;
                controlDict.Add(con.Name.ToLower(), con);
                controlDictFriendly.Add(con.DisplayName.ToLower(), con);
            }

            private void GenerateCustomControls()
            {
                AddCustomControl(RichHudControls.MousewheelUp,
                    () => MyAPIGateway.Input.DeltaMouseScrollWheelValue() > 0,
                    () => Math.Abs(MyAPIGateway.Input.DeltaMouseScrollWheelValue())
                );
                AddCustomControl(RichHudControls.MousewheelDown,
                    () => MyAPIGateway.Input.DeltaMouseScrollWheelValue() < 0,
                    () => Math.Abs(MyAPIGateway.Input.DeltaMouseScrollWheelValue())
                );

                // Add gamepad axes
                // Left Stick

                // Left Stick Directions
                AddCustomControl(RichHudControls.LeftStickLeft,
                    () => MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Xneg) > .01f,
                    () => MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Xneg)
                );
                AddCustomControl(RichHudControls.LeftStickRight,
                    () => MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Xpos) > .01f,
                    () => MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Xpos)
                );
                AddCustomControl(RichHudControls.LeftStickUp,
                    () => MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Yneg) > .01f,
                    () => MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Yneg)
                );
                AddCustomControl(RichHudControls.LeftStickDown,
                    () => MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Ypos) > .01f,
                    () => MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Ypos)
                );

                // Left X axis
                AddCustomControl(RichHudControls.LeftStickX,
                    () =>
                    {
                        float xPos = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Xpos),
                            xNeg = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Xneg);

                        return Math.Abs(xPos - xNeg) > .001f;
                    },
                    () =>
                    {
                        float xPos = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Xpos),
                            xNeg = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Xneg);

                        return (xPos - xNeg);
                    }
                );

                // Left Y axis
                AddCustomControl(RichHudControls.LeftStickY,
                    () =>
                    {
                        float pos = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Yneg),
                            neg = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Ypos);

                        return Math.Abs(pos - neg) > .001f;
                    },
                    () =>
                    {
                        float pos = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Yneg),
                            neg = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Ypos);

                        return (pos - neg);
                    }
                );

                // Right Stick Directions
                AddCustomControl(RichHudControls.RightStickLeft,
                    () => MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.RotationXneg) > .01f,
                    () => MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.RotationXneg)
                );
                AddCustomControl(RichHudControls.RightStickRight,
                    () => MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.RotationXpos) > .01f,
                    () => MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.RotationXpos)
                );
                AddCustomControl(RichHudControls.RightStickUp,
                    () => MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.RotationYneg) > .01f,
                    () => MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.RotationYneg)
                );
                AddCustomControl(RichHudControls.RightStickDown,
                    () => MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.RotationYpos) > .01f,
                    () => MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.RotationYpos)
                );

                // Right X axis
                AddCustomControl(RichHudControls.RightStickX,
                    () =>
                    {
                        float xPos = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.RotationXpos),
                            xNeg = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.RotationXneg);

                        return Math.Abs(xPos - xNeg) > .001f;
                    },
                    () =>
                    {
                        float xPos = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.RotationXpos),
                            xNeg = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.RotationXneg);

                        return (xPos - xNeg);
                    }
                );

                // Right Y axis
                AddCustomControl(RichHudControls.RightStickY,
                    () =>
                    {
                        float pos = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.RotationYneg),
                            neg = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.RotationYpos);

                        return Math.Abs(pos - neg) > .001f;
                    },
                    () =>
                    {
                        float pos = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.RotationYneg),
                            neg = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.RotationYpos);

                        return (pos - neg);
                    }
                );

                // Left trigger
                AddCustomControl(RichHudControls.LeftTrigger,
                    () => Math.Abs(MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.ZLeft)) > .001f,
                    () => MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.ZLeft)
                );

                // Right trigger
                AddCustomControl(RichHudControls.RightTrigger,
                    () => Math.Abs(MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.ZRight)) > .001f,
                    () => MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.ZRight)
                );

                // Slider 1
                AddCustomControl(RichHudControls.Slider1,
                    () =>
                    {
                        float pos = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Slider1pos),
                            neg = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Slider1neg);

                        return Math.Abs(pos - neg) > .001f;
                    },
                    () =>
                    {
                        float pos = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Slider1pos),
                            neg = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Slider1neg);

                        return (pos - neg);
                    }
                );

                // Slider 2
                AddCustomControl(RichHudControls.Slider2,
                    () =>
                    {
                        float pos = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Slider2pos),
                            neg = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Slider2neg);

                        return Math.Abs(pos - neg) > .001f;
                    },
                    () =>
                    {
                        float pos = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Slider2pos),
                            neg = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Slider2neg);

                        return (pos - neg);
                    }
                );
            }

            private void GetControlStringIDs(MyKeys[] keys, out string[] allControls, out string[] mouseControls)
            {
                // Use HashSets to automatically handle duplicates and improve lookup speed
                HashSet<string> controlIDSet = new HashSet<string>(BuiltInBinds);
                HashSet<string> mouseControlIDSet = new HashSet<string>();

                // Add any additional controls bound to keys that might not be in BuiltInBinds
                foreach (MyKeys key in keys)
                {
                    var control = MyAPIGateway.Input.GetControl(key);
                    string stringID = control?.GetGameControlEnum().ToString();

                    if (!string.IsNullOrEmpty(stringID) && stringID != "None")
                        controlIDSet.Add(stringID);
                }

                // Identify which controls are assigned to mouse buttons
                var mouseButtons = Enum.GetValues(typeof(MyMouseButtonsEnum)) as MyMouseButtonsEnum[];

                foreach (MyMouseButtonsEnum mouseBtn in mouseButtons)
                {
                    var control = MyAPIGateway.Input.GetControl(mouseBtn);
                    string stringID = control?.GetGameControlEnum().ToString();

                    // "FORWARD" appears in mouse control lists?
                    if (!string.IsNullOrEmpty(stringID) && stringID != "None" && stringID != "FORWARD")
                    {
                        mouseControlIDSet.Add(stringID);

                        // Ensure any control discovered via mouse is also in the master list
                        controlIDSet.Add(stringID);
                    }
                }

                // Convert back to arrays for the out parameters
                allControls = new string[controlIDSet.Count];
                controlIDSet.CopyTo(allControls);

                mouseControls = new string[mouseControlIDSet.Count];
                mouseControlIDSet.CopyTo(mouseControls);
            }

            /// <summary>
            /// Generates a list of control indices using a list of control names.
            /// </summary>
            public static void GetComboIndices(IReadOnlyList<string> names, List<int> indices, bool sanitize = true)
            {
                indices.Clear();

                for (int n = 0; n < (names?.Count ?? 0); n++)
                    indices.Add(GetControl(names[n]));

                if (sanitize)
                    SanitizeCombo(indices);
            }

            /// <summary>
            /// Generates a list of control indices from a list of <see cref="ControlHandle"/>s.
            /// </summary>
            public static void GetComboIndices(IReadOnlyList<ControlHandle> controls, List<int> combo, bool sanitize = true)
            {
                combo.Clear();

                for (int n = 0; n < controls.Count; n++)
                    combo.Add(controls[n].id);

                if (sanitize)
                    SanitizeCombo(combo);
            }

            private static void AddHandlesToConBuf(IReadOnlyList<ControlHandle> combo = null,
                IReadOnlyList<IReadOnlyList<ControlHandle>> aliases = null)
            {
                var hBuf = _instance.cHandleBuf;
                var cBuf = hBuf.Item1;
                var aliasBuf = hBuf.Item2;

                foreach (ControlHandle con in combo)
                    cBuf.Add(con.id);

                for (int i = 0; i < aliases.Count; i++)
                {
                    List<int> aliasCons;
                    var alias = aliases[i];

                    if (i >= aliasBuf.Count)
                    {
                        aliasCons = new List<int>();
                        aliasBuf.Add(aliasCons);
                    }
                    else
                        aliasCons = aliasBuf[i];

                    foreach (ControlHandle con in alias)
                        aliasCons.Add(con.id);
                }
            }

            private static void ResetConHandleBuf()
            {
                var hBuf = _instance.cHandleBuf;
                hBuf.Item1.Clear();

                foreach (var combo in hBuf.Item2)
                    combo.Clear();
            }

            private static IReadOnlyList<int> GetSanitizedComboTemp(IEnumerable<int> combo)
            {
                var buf = _instance.conIDbuf;

                if (buf != combo)
                {
                    buf.Clear();
                    buf.AddRange(combo);
                }

                SanitizeCombo(buf);
                return buf;
            }

            /// <summary>
            /// Sorts ControlID buffer and removes duplicates and invalid indices
            /// </summary>
            private static void SanitizeCombo(List<int> combo)
            {
                combo.Sort();

                for (int i = combo.Count - 1; i > 0; i--)
                {
                    if (combo[i] == combo[i - 1] || combo[i] <= 0)
                        combo.RemoveAt(i);
                }

                if (combo.Count > 0 && combo[0] == 0)
                    combo.RemoveAt(0);
            }
        }
    }
}