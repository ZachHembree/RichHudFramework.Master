using RichHudFramework.Game;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage;
using VRage.Input;
using BindDefinitionData = VRage.MyTuple<string, string[]>;
using ControlMembers = VRage.MyTuple<string, int, System.Func<bool>, bool>;
using ApiMemberAccessor = System.Func<object, int, object>;

namespace RichHudFramework
{
    namespace UI.Server
    {
        /// <summary>
        /// Manages custom keybinds; singleton
        /// </summary>
        public sealed partial class BindManager : RichHudComponentBase
        {
            public static IReadOnlyCollection<IBindGroup> Groups => Instance.mainClient.Groups;
            public static ReadOnlyCollection<IControl> Controls => Instance.extControls;

            private static BindManager Instance
            {
                get { Init(); return _instance; }
                set { _instance = value; }
            }
            private static BindManager _instance;

            private readonly Control[] controls;
            private readonly ReadOnlyCollection<IControl> extControls;
            private readonly Dictionary<string, IControl> controlDict, controlDictDisp;
            private readonly List<MyKeys> controlBlacklist;

            private readonly BindClient mainClient;

            private BindManager() : base(false, true)
            {
                controlBlacklist = new List<MyKeys>()
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
                controlDictDisp = new Dictionary<string, IControl>(300);

                controls = GenerateControls();
                extControls = new ReadOnlyCollection<IControl>(controls as IControl[]);

                mainClient = new BindClient();
            }

            public static void Init()
            {
                if (_instance == null)
                    _instance = new BindManager();
                else if (_instance.Parent == null)
                    _instance.RegisterComponent(RichHudCore.Instance);
            }

            public override void HandleInput()
            {
                mainClient.HandleInput();
            }

            public override void Close()
            {
                mainClient.Unload();

                if (Parent.Unloading)
                    _instance = null;
            }

            /// <summary>
            /// Returns a new bind client for Framework clients.
            /// </summary>
            public static IBindClient GetNewBindClient()
            {
                return new BindClient();
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
                else if (Instance.controlDictDisp.TryGetValue(name.ToLower(), out con))
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
                            controlDictDisp.Add(name, con);
                            controls[index] = con;
                        }
                    }
                }

                controls[256] = new Control("MousewheelUp", "MwUp", 256,
                    () => MyAPIGateway.Input.DeltaMouseScrollWheelValue() > 0, true);

                controls[257] = new Control("MousewheelDown", "MwDn", 257,
                    () => MyAPIGateway.Input.DeltaMouseScrollWheelValue() < 0, true);

                controlDict.Add("mousewheelup", controls[256]);
                controlDict.Add("mousewheeldown", controls[257]);

                controlDictDisp.Add("mwup", controls[256]);
                controlDictDisp.Add("mwdn", controls[257]);

                return controls;
            }

            /// <summary>
            /// Generates a list of controls from a list of control names.
            /// </summary>
            public static IControl[] GetCombo(IList<string> names)
            {
                IControl[] combo = new IControl[names.Count];

                for (int n = 0; n < names.Count; n++)
                    combo[n] = GetControl(names[n]);

                return combo;
            }

            /// <summary>
            /// Generates a list of control indices using a list of control names.
            /// </summary>
            public static int[] GetComboIndices(IList<string> names)
            {
                int[] combo = new int[names.Count];

                for (int n = 0; n < names.Count; n++)
                    combo[n] = GetControl(names[n]).Index;

                return combo;
            }

            /// <summary>
            /// Generates a combo array using the corresponding control indices.
            /// </summary>
            public static IControl[] GetCombo(IList<int> indices)
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

            /// <summary>
            /// Generates a combo array using the corresponding control indices.
            /// </summary>
            public static IControl[] GetCombo(IList<ControlData> indices)
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

            /// <summary>
            /// Generates a list of control indices from a list of controls.
            /// </summary>
            public static int[] GetComboIndices(IList<IControl> controls)
            {
                int[] indices = new int[controls.Count];

                for (int n = 0; n < controls.Count; n++)
                    indices[n] = controls[n].Index;

                return indices;
            }

            /// <summary>
            /// Generates a list of control indices from a list of controls.
            /// </summary>
            public static int[] GetComboIndices(IList<ControlData> controls)
            {
                int[] indices = new int[controls.Count];

                for (int n = 0; n < controls.Count; n++)
                    indices[n] = controls[n].index;

                return indices;
            }

            /// <summary>
            /// Tries to generate a combo from a list of control names.
            /// </summary>
            public static bool TryGetCombo(IList<string> controlNames, out IControl[] newCombo)
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