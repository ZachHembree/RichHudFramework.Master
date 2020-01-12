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
        public sealed partial class BindManager : ModBase.ComponentBase
        {
            public static IReadOnlyCollection<IBindGroup> Groups => Instance.mainClient.Groups;
            public static ReadOnlyCollection<IControl> Controls { get; }

            private static BindManager Instance
            {
                get { Init(); return instance; }
                set { instance = value; }
            }
            private static BindManager instance;

            private static readonly Control[] controls;
            private static readonly Dictionary<string, IControl> controlDict, controlDictDisp;
            private static readonly List<MyKeys> controlBlacklist;
            private static int seKeyMax;

            private readonly BindClient mainClient;

            static BindManager()
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

                controlDict = new Dictionary<string, IControl>(250);
                controlDictDisp = new Dictionary<string, IControl>(250);

                controls = GenerateControls();
                Controls = new ReadOnlyCollection<IControl>(controls as IControl[]);
            }

            private BindManager() : base(false, true)
            {
                mainClient = new BindClient();
            }

            public static void Init()
            {
                if (instance == null)
                    instance = new BindManager();
            }

            public override void HandleInput()
            {
                mainClient.HandleInput();
            }

            public override void Close()
            {
                Instance = null;
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

                if (controlDict.TryGetValue(name.ToLower(), out con))
                    return con;
                else if (controlDictDisp.TryGetValue(name.ToLower(), out con))
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
                Controls[seKeyMax + (int)rhdKey];

            /// <summary>
            /// Builds dictionary of controls from the set of MyKeys enums and a couple custom controls for the mouse wheel.
            /// </summary>
            private static Control[] GenerateControls()
            {
                var keys = Enum.GetValues(typeof(MyKeys)) as MyKeys[];
                seKeyMax = 0;

                for (int n = 0; n < keys.Length; n++)
                {
                    var value = (int)keys[n];

                    if (value > seKeyMax)
                        seKeyMax = value;
                }

                Control[] controls = new Control[seKeyMax + 3];

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

                controls[seKeyMax + 1] = new Control("MousewheelUp", "MwUp", seKeyMax + 1,
                    () => MyAPIGateway.Input.DeltaMouseScrollWheelValue() > 0, true);

                controls[seKeyMax + 2] = new Control("MousewheelDown", "MwDn", seKeyMax + 2,
                    () => MyAPIGateway.Input.DeltaMouseScrollWheelValue() < 0, true);

                int last = controls.Length - 1;

                controlDict.Add("mousewheelup", controls[last - 1]);
                controlDict.Add("mousewheeldown", controls[last]);

                controlDictDisp.Add("mwup", controls[last - 1]);
                controlDictDisp.Add("mwdn", controls[last]);

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
                    combo[n] = Controls[indices[n]];

                return combo; ;
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