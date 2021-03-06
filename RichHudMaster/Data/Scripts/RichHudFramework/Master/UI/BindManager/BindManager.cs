using RichHudFramework.Internal;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Input;

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

            private static BindManager Instance
            {
                get { Init(); return _instance; }
                set { _instance = value; }
            }
            private static BindManager _instance;

            private readonly Control[] controls;
            private readonly Dictionary<string, IControl> controlDict, controlDictFriendly;
            private readonly HashSet<MyKeys> controlBlacklist;
            private readonly List<Client> bindClients;

            private Client mainClient;

            private BindManager() : base(false, true)
            {
                controlBlacklist = new HashSet<MyKeys>()
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

                controlDict = new Dictionary<string, IControl>(300);
                controlDictFriendly = new Dictionary<string, IControl>(300);

                controls = GenerateControls();
                bindClients = new List<Client>();
            }

            public static void Init()
            {
                if (_instance == null)
                    _instance = new BindManager();
                else if (_instance.Parent == null)
                    _instance.RegisterComponent(RichHudCore.Instance);

                if (_instance.mainClient == null)
                    _instance.mainClient = new Client();
            }

            public override void HandleInput()
            {
                for (int n = 0; n < bindClients.Count; n++)
                {
                    bindClients[n].HandleInput();
                }
            }

            public override void Close()
            {
                bindClients.Clear();
                mainClient = null;

                if (ExceptionHandler.Unloading)
                    _instance = null;
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
            private Control[] GenerateControls()
            {
                var keys = Enum.GetValues(typeof(MyKeys)) as MyKeys[];
                Control[] controls = new Control[258];

                for (int n = 0; n < keys.Length; n++)
                {
                    var index = (int)keys[n];
                    var seKey = keys[n];

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

                controlDict.Add("mousewheelup", controls[256]);
                controlDict.Add("mousewheeldown", controls[257]);

                controlDictFriendly.Add("mwup", controls[256]);
                controlDictFriendly.Add("mwdn", controls[257]);

                return controls;
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