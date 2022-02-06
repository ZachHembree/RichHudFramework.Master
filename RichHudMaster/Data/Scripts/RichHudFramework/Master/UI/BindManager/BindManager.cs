using RichHudFramework.Internal;
using Sandbox.Game;
using Sandbox.ModAPI;
using System;
using System.Diagnostics;
using System.Collections.Generic;
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

            private static readonly HashSet<MyKeys> controlBlacklist = new HashSet<MyKeys>()
            {
                MyKeys.None,
                MyKeys.LeftAlt,
                MyKeys.RightAlt,
                MyKeys.LeftShift,
                MyKeys.RightShift,
                MyKeys.LeftControl,
                MyKeys.RightControl,
                MyKeys.LeftWindows,
                MyKeys.RightWindows
            };

            private static readonly Dictionary<MyKeys, MyKeys[]> controlAliases = new Dictionary<MyKeys, MyKeys[]>()
            {
                { MyKeys.Alt, new MyKeys[] { MyKeys.LeftAlt, MyKeys.RightAlt } },
                { MyKeys.Shift, new MyKeys[] { MyKeys.LeftShift, MyKeys.RightShift } },
                { MyKeys.Control, new MyKeys[] { MyKeys.LeftControl, MyKeys.RightControl } }
            };

            private readonly Control[] controls;
            private readonly string[] seControlIDs, seMouseControlIDs;
            private readonly Dictionary<string, IControl> controlDict, controlDictFriendly;
            private readonly List<Client> bindClients;

            private Client mainClient;
            private bool areControlsBlacklisted, areMouseControlsBlacklisted;
            private int chatInputTick;

            private BindManager() : base(false, true)
            {
                controlDict = new Dictionary<string, IControl>(300);
                controlDictFriendly = new Dictionary<string, IControl>(300);

                var keys = Enum.GetValues(typeof(MyKeys)) as MyKeys[];
                controls = GenerateControls(keys);
                GetControlStringIDs(keys, out seControlIDs, out seMouseControlIDs);

                bindClients = new List<Client>();
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

                if (SharedBinds.Enter.IsNewPressed)
                {
                    IsChatOpen = !IsChatOpen;
                    chatInputTick = 0;
                }

                // Synchronize with actual value every second or so
                if (chatInputTick == 60)
                {
                    IsChatOpen = MyAPIGateway.Gui.ChatEntryVisible;
                }

                chatInputTick++;
                chatInputTick %= 60;
            }

            private void UpdateControls()
            {
                foreach (Control control in controls)
                {
                    if (control != Control.Default)
                    {
                        control.Update();
                    }
                }
            }

            private void UpdateBlacklist()
            {
                CurrentBlacklistMode = SeBlacklistModes.None;

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
                    IMyControllableEntity conEnt = MyAPIGateway.Session.ControlledObject;

                    if (conEnt != null)
                        conEnt.MoveAndRotate(conEnt.LastMotionIndicator, Vector2.Zero, 0f);
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

            private static void SetBlacklist(IReadOnlyList<string> IDs, bool value)
            {
                if (MyAPIGateway.Session?.Player != null)
                {
                    foreach (string control in IDs)
                        MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(control, MyAPIGateway.Session.Player.IdentityId, !value);
                }
            }

            public override void Close()
            {
                bindClients.Clear();
                mainClient = null;

                if (ExceptionHandler.Unloading)
                    _instance = null;

                SetBlacklist(seControlIDs, false);
                SetBlacklist(seMouseControlIDs, false);
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
                IControl con;

                if (Instance.controlDict.TryGetValue(name.ToLower(), out con))
                    return con;
                else if (Instance.controlDictFriendly.TryGetValue(name.ToLower(), out con))
                    return con;

                return null;
            }

            /// <summary>
            /// Returns the control associated with the given <see cref="MyKeys"/> enum.
            /// </summary>
            public static IControl GetControl(MyKeys seKey) =>
                Controls[(int)seKey];

            /// <summary>
            /// Returns the control associated with the given custom <see cref="RichHudControls"/> enum.
            /// </summary>
            public static IControl GetControl(RichHudControls rhdKey) =>
                Controls[(int)rhdKey];

            /// <summary>
            /// Builds dictionary of controls from the set of MyKeys enums and a couple custom controls for the mouse wheel.
            /// </summary>
            private Control[] GenerateControls(MyKeys[] keys)
            {
                Control[] controls = new Control[258];

                // Initialize control list to default
                for (int i = 0; i < controls.Length; i++)
                    controls[i] = Control.Default;
                    
                // Add controls
                for (int i = 0; i < keys.Length; i++)
                {
                    var index = (int)keys[i];
                    var seKey = keys[i];

                    if (!controlBlacklist.Contains(seKey))
                    {
                        Control con = new Control(seKey, index);
                        string name = con.Name.ToLower(), disp = con.DisplayName.ToLower();

                        if (!controlDict.ContainsKey(name))
                        {
                            controlDict.Add(name, con);
                            controlDictFriendly.Add(name, con);
                            controls[index] = con;
                        }
                    }
                    else
                        controls[index] = Control.Default;
                }

                controls[256] = new Control("MousewheelUp", "MwUp", 256,
                    () => MyAPIGateway.Input.DeltaMouseScrollWheelValue() > 0, true);

                controls[257] = new Control("MousewheelDown", "MwDn", 257,
                    () => MyAPIGateway.Input.DeltaMouseScrollWheelValue() < 0, true);

                // Map control aliases to appropriate controls
                foreach (KeyValuePair<MyKeys, MyKeys[]> controlAliasPair in controlAliases)
                {
                    Control con = controls[(int)controlAliasPair.Key];

                    foreach (MyKeys key in controlAliasPair.Value)
                        controls[(int)key] = con;
                }

                controlDict.Add("mousewheelup", controls[256]);
                controlDict.Add("mousewheeldown", controls[257]);

                controlDictFriendly.Add("mwup", controls[256]);
                controlDictFriendly.Add("mwdn", controls[257]);

                return controls;
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
            public static IControl[] GetCombo(IReadOnlyList<string> names)
            {
                IControl[] combo = new IControl[names.Count];

                for (int n = 0; n < names.Count; n++)
                    combo[n] = GetControl(names[n]);

                return combo;
            }

            /// <summary>
            /// Generates a list of control indices using a list of control names.
            /// </summary>
            public static int[] GetComboIndices(IReadOnlyList<string> names)
            {
                int[] combo = new int[names.Count];

                for (int n = 0; n < names.Count; n++)
                    combo[n] = GetControl(names[n]).Index;

                return combo;
            }

            /// <summary>
            /// Generates a combo array using the corresponding control indices.
            /// </summary>
            public static IControl[] GetCombo(IReadOnlyList<int> indices)
            {
                if (indices != null && indices.Count > 0)
                {
                    IControl[] combo = new IControl[indices.Count];

                    for (int n = 0; n < indices.Count; n++)
                    {
                        int index = indices[n];

                        if (index < Controls.Count)
                            combo[n] = Controls[index];
                    }

                    return combo;
                }

                return null;
            }

            /// <summary>
            /// Generates a combo array using the corresponding control indices.
            /// </summary>
            public static IControl[] GetCombo(IReadOnlyList<ControlData> indices)
            {
                if (indices != null && indices.Count > 0)
                {
                    IControl[] combo = new IControl[indices.Count];

                    for (int n = 0; n < indices.Count; n++)
                    {
                        int index = indices[n];

                        if (index < Controls.Count)
                            combo[n] = Controls[index];
                    }

                    return combo;
                }

                return null;
            }

            /// <summary>
            /// Generates a list of control indices from a list of controls.
            /// </summary>
            public static int[] GetComboIndices(IReadOnlyList<IControl> controls)
            {
                int[] indices = new int[controls.Count];

                for (int n = 0; n < controls.Count; n++)
                    indices[n] = controls[n].Index;

                return indices;
            }

            /// <summary>
            /// Generates a list of control indices from a list of controls.
            /// </summary>
            public static int[] GetComboIndices(IReadOnlyList<ControlData> controls)
            {
                int[] indices = new int[controls.Count];

                for (int n = 0; n < controls.Count; n++)
                    indices[n] = controls[n].index;

                return indices;
            }

            /// <summary>
            /// Tries to generate a combo from a list of control names.
            /// </summary>
            public static bool TryGetCombo(IReadOnlyList<string> controlNames, out IControl[] newCombo)
            {
                IControl con;
                newCombo = new IControl[controlNames.Count];

                for (int n = 0; n < controlNames.Count; n++)
                {
                    con = GetControl(controlNames[n].ToLower());

                    if (con != null)
                        newCombo[n] = con;
                    else
                        return false;
                }

                return true;
            }
        }
    }
}