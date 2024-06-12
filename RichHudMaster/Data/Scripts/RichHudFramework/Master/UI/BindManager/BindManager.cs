using RichHudFramework.Internal;
using RichHudFramework.Server;
using Sandbox.Game;
using Sandbox.ModAPI;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using VRage;
using VRage.Input;
using VRage.Utils;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;
using IMyControllableEntity = VRage.Game.ModAPI.Interfaces.IMyControllableEntity;
using VRage.GameServices;

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
            public static IReadOnlyList<IBindGroup> Groups => Instance.mainClient.Groups;

            /// <summary>
            /// Read-only collection of all available controls for use with key binds
            /// </summary>
            public static IReadOnlyList<IControl> Controls => Instance.controls;

            /// <summary>
            /// Read-only list of registered bind clients
            /// </summary>
            public static IReadOnlyList<Client> Clients => Instance.bindClients;

            /// <summary>
            /// Bind client used by RHM
            /// </summary>
            public static Client MainClient => Instance.mainClient;

            /// <summary>
            /// Specifies blacklist mode for SE controls
            /// </summary>
            public static SeBlacklistModes BlacklistMode
            {
                get { if (_instance == null) Init(); return _instance.mainClient.RequestBlacklistMode; }
                set
                {
                    if (_instance == null)
                        Init();

                    _instance.mainClient.RequestBlacklistMode = value;
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

            private static readonly Dictionary<ControlHandle, ControlHandle[]> controlAliases = new Dictionary<ControlHandle, ControlHandle[]>
            {
                { MyKeys.Alt, new ControlHandle[] { MyKeys.LeftAlt, MyKeys.RightAlt } },
                { MyKeys.Shift, new ControlHandle[] { MyKeys.LeftShift, MyKeys.RightShift } },
                { MyKeys.Control, new ControlHandle[] { MyKeys.LeftControl, MyKeys.RightControl } }
            };

            private readonly Control[] controls;
            private readonly string[] seControlIDs, seMouseControlIDs;
            private readonly Dictionary<string, IControl> controlDict, controlDictFriendly;
            private readonly List<Client> bindClients;
            private readonly List<int> conIDbuf;
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

                GenerateControls(controls, kbmKeys, gpKeys);
                GetControlStringIDs(kbmKeys, out seControlIDs, out seMouseControlIDs);

                bindClients = new List<Client>();
                conIDbuf = new List<int>();
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
                    if ((bindClients[n].RequestBlacklistMode & SeBlacklistModes.AllKeys) == SeBlacklistModes.AllKeys)
                        CurrentBlacklistMode |= SeBlacklistModes.AllKeys;

                    if ((bindClients[n].RequestBlacklistMode & SeBlacklistModes.Mouse) > 0)
                        CurrentBlacklistMode |= SeBlacklistModes.Mouse;

                    if ((bindClients[n].RequestBlacklistMode & SeBlacklistModes.CameraRot) > 0)
                        CurrentBlacklistMode |= SeBlacklistModes.CameraRot;
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
            public static IControl GetControl(string name)
            {
                Init();
                IControl con;

                if (_instance.controlDict.TryGetValue(name.ToLower(), out con))
                    return con;
                else if (_instance.controlDictFriendly.TryGetValue(name.ToLower(), out con))
                    return con;

                return null;
            }

            /// <summary>
            /// Returns the control associated with the given <see cref="ControlHandle"/>.
            /// </summary>
            public static IControl GetControl(ControlHandle handle) =>
                Controls[handle.id];

            /// <summary>
            /// Builds dictionary of controls from the set of MyKeys enums and a couple custom controls for the mouse wheel.
            /// </summary>
            private void GenerateControls(Control[] controls, MyKeys[] kbmKeys, MyJoystickButtonsEnum[] gpKeys)
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
                    var index = ControlHandle.GPKeysStart + (int)gpKeys[i];
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

                GenerateCustomControls(controls);

                // Map control aliases to appropriate controls
                foreach (KeyValuePair<ControlHandle, ControlHandle[]> controlAliasPair in controlAliases)
                {
                    Control con = controls[controlAliasPair.Key.id];

                    foreach (ControlHandle key in controlAliasPair.Value)
                        controls[key.id] = con;
                }                
            }

            private void GenerateCustomControls(Control[] controls)
            {
                controls[256] = new Control("MousewheelUp", "MwUp", 256,
                    () => MyAPIGateway.Input.DeltaMouseScrollWheelValue() > 0, 
                    () => Math.Abs(MyAPIGateway.Input.DeltaMouseScrollWheelValue())
                );
                controls[257] = new Control("MousewheelDown", "MwDn", 257,
                    () => MyAPIGateway.Input.DeltaMouseScrollWheelValue() < 0,
                    () => Math.Abs(MyAPIGateway.Input.DeltaMouseScrollWheelValue())
                );

                controlDict.Add("mousewheelup", controls[256]);
                controlDict.Add("mousewheeldown", controls[257]);
                controlDictFriendly.Add("mwup", controls[256]);
                controlDictFriendly.Add("mwdn", controls[257]);

                // Add gamepad axes
                // Left X axis
                controls[258] = new Control(
                    "LeftStickX", "LeftX", 258,
                    () => {
                        float xPos = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Xpos),
                            xNeg = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Xneg);

                        return Math.Abs(xPos - xNeg) > .001f;
                    },
                    () => {
                        float xPos = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Xpos),
                            xNeg = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Xneg);

                        return (xPos - xNeg);
                    }
                );

                // Left Y axis
                controls[259] = new Control(
                    "LeftStickY", "LeftY", 259,
                    () => {
                        float pos = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Ypos),
                            neg = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Yneg);

                        return Math.Abs(pos - neg) > .001f;
                    },
                    () => {
                        float pos = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Ypos),
                            neg = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Yneg);

                        return (pos - neg);
                    }
                );

                controlDict.Add("LeftStickX".ToLower(), controls[258]);
                controlDict.Add("LeftStickY".ToLower(), controls[259]);
                controlDictFriendly.Add("LeftX".ToLower(), controls[258]);
                controlDictFriendly.Add("LeftY".ToLower(), controls[259]);

                // Right X axis
                controls[260] = new Control(
                    "RightStickX", "RightY", 260,
                    () => {
                        float xPos = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.RotationXpos),
                            xNeg = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.RotationXneg);

                        return Math.Abs(xPos - xNeg) > .001f;
                    },
                    () => {
                        float xPos = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.RotationXpos),
                            xNeg = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.RotationXneg);

                        return (xPos - xNeg);
                    }
                );

                // Right Y axis
                controls[261] = new Control(
                    "RightStickY", "RightY", 261,
                    () => {
                        float pos = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.RotationYpos),
                            neg = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.RotationYneg);

                        return Math.Abs(pos - neg) > .001f;
                    },
                    () => {
                        float pos = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.RotationYpos),
                            neg = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.RotationYneg);

                        return (pos - neg);
                    }
                );

                controlDict.Add("RightStickX".ToLower(), controls[260]);
                controlDict.Add("RightStickY".ToLower(), controls[261]);
                controlDictFriendly.Add("RightX".ToLower(), controls[260]);
                controlDictFriendly.Add("RightY".ToLower(), controls[261]);

                // Left trigger
                controls[262] = new Control(
                    "ZLeft", "LeftTrigger", 262,
                    () => Math.Abs(MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.ZLeft)) > .001f,
                    () => MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.ZLeft)
                );

                // Right trigger
                controls[263] = new Control(
                    "ZRight", "RightTrigger", 263,
                    () => Math.Abs(MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.ZRight)) > .001f,
                    () => MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.ZRight)
                );

                controlDict.Add("ZLeft".ToLower(), controls[262]);
                controlDict.Add("ZRight".ToLower(), controls[263]);
                controlDictFriendly.Add("LeftTrigger".ToLower(), controls[262]);
                controlDictFriendly.Add("RightTrigger".ToLower(), controls[263]);

                // Slider 1
                controls[264] = new Control(
                    "Slider1", "Slider1", 264,
                    () => {
                        float pos = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Slider1pos),
                            neg = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Slider1neg);

                        return Math.Abs(pos - neg) > .001f;
                    },
                    () => {
                        float pos = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Slider1pos),
                            neg = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Slider1neg);

                        return (pos - neg);
                    }
                );

                // Slider 2
                controls[265] = new Control(
                    "Slider2", "Slider2", 265,
                    () => {
                        float pos = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Slider2pos),
                            neg = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Slider2neg);

                        return Math.Abs(pos - neg) > .001f;
                    },
                    () => {
                        float pos = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Slider2pos),
                            neg = MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Slider2neg);

                        return (pos - neg);
                    }
                );

                controlDict.Add("Slider1".ToLower(), controls[264]);
                controlDict.Add("Slider2".ToLower(), controls[265]);
                controlDictFriendly.Add("Slider1".ToLower(), controls[264]);
                controlDictFriendly.Add("Slider2".ToLower(), controls[265]);
            }

            private void GetControlStringIDs(MyKeys[] keys, out string[] allControls, out string[] mouseControls)
            {
                var mouseButtons = Enum.GetValues(typeof(MyMouseButtonsEnum)) as MyMouseButtonsEnum[];
                List<string> controlIDs = new List<string>(keys.Length),
                    mouseControlIDs = new List<string>(mouseButtons.Length);

                foreach (MyKeys key in keys)
                {
                    MyStringId? id = MyAPIGateway.Input.GetControl(key)?.GetGameControlEnum();
                    string stringID = id?.ToString();

                    if (stringID != null && stringID.Length > 0)
                        controlIDs.Add(stringID);
                }

                foreach (MyMouseButtonsEnum key in mouseButtons)
                {
                    MyStringId? id = MyAPIGateway.Input.GetControl(key)?.GetGameControlEnum();
                    string stringID = id?.ToString();

                    if (stringID != null && stringID.Length > 0 && stringID != "FORWARD") // WTH is FORWARD in there?
                        mouseControlIDs.Add(stringID);
                }

                allControls = controlIDs.ToArray();
                mouseControls = mouseControlIDs.ToArray();
            }

            /// <summary>
            /// Generates a list of controls from a list of control names.
            /// </summary>
            public static IControl[] GetCombo(IReadOnlyList<string> names, bool sanitize = true)
            {
                var buf = Instance.conIDbuf;
                buf.Clear();

                for (int n = 0; n < names.Count; n++)
                    buf.Add(GetControl(names[n])?.Index ?? 0);

                if (sanitize)
                    SanitizeCombo(buf);

                IControl[] combo = new IControl[buf.Count];

                for (int i = 0; i < buf.Count; i++)
                    combo[i] = _instance.controls[buf[i]];

                return combo;
            }

            /// <summary>
            /// Generates a list of control indices using a list of control names.
            /// </summary>
            public static void GetComboIndices(IReadOnlyList<string> names, List<int> indices, bool sanitize = true)
            {
                indices.Clear();

                for (int n = 0; n < names.Count; n++)
                    indices.Add(GetControl(names[n])?.Index ?? 0);

                if (sanitize)
                    SanitizeCombo(indices);
            }

            /// <summary>
            /// Generates a combo array using the corresponding control indices.
            /// </summary>
            public static IControl[] GetCombo(IReadOnlyList<int> indices, bool sanitize = true)
            {
                if (indices != null && indices.Count > 0)
                {
                    var buf = Instance.conIDbuf;
                    buf.Clear();

                    for (int i = 0; i < indices.Count; i++)
                    {
                        buf.Add(indices[i]);
                    }

                    if (sanitize)
                        SanitizeCombo(buf);

                    IControl[] combo = new IControl[buf.Count];

                    for (int n = 0; n < buf.Count; n++)
                    {
                        int index = buf[n];
                        combo[n] = _instance.controls[index];
                    }

                    return combo;
                }

                return null;
            }

            /// <summary>
            /// Generates a combo array using the corresponding control indices.
            /// </summary>
            public static IControl[] GetCombo(IReadOnlyList<ControlHandle> indices, bool sanitize = true)
            {
                if (indices != null && indices.Count > 0)
                {
                    var buf = Instance.conIDbuf;
                    buf.Clear();

                    for (int i= 0; i < indices.Count; i++)
                    {
                        buf.Add(indices[i].id);
                    }

                    if (sanitize)
                        SanitizeCombo(buf);

                    IControl[] combo = new IControl[buf.Count];

                    for (int n = 0; n < buf.Count; n++)
                    {
                        int index = buf[n];
                        combo[n] = _instance.controls[index];
                    }

                    return combo;
                }

                return null;
            }

            /// <summary>
            /// Generates a list of control indices from a list of controls.
            /// </summary>
            public static void GetComboIndices(IReadOnlyList<IControl> controls, List<int> combo, bool sanitize = true)
            {
                combo.Clear();

                for (int n = 0; n < controls.Count; n++)
                    combo.Add(controls[n].Index);

                if (sanitize)
                    SanitizeCombo(combo);
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

            /// <summary>
            /// Tries to generate a unique combo from a list of control names.
            /// </summary>
            public static bool TryGetCombo(IReadOnlyList<string> controlNames, out IControl[] newCombo, bool sanitize = true)
            {
                var buf = Instance.conIDbuf;
                buf.Clear();
                newCombo = null;

                for (int n = 0; n < controlNames.Count; n++)
                {
                    IControl con = GetControl(controlNames[n].ToLower());

                    if (con != null)
                        buf.Add(con.Index);
                    else
                        return false;
                }

                if (sanitize)
                    SanitizeCombo(buf);

                newCombo = new IControl[buf.Count];

                for (int i = 0; i < buf.Count; i++)
                {
                    int conID = buf[i];
                    newCombo[i] = _instance.controls[conID];
                }

                return true;
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