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

                controlDict = new Dictionary<string, IControl>(200);
                controlDictDisp = new Dictionary<string, IControl>(200);

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

            public static IBindClient GetNewBindClient()
            {
                return new BindClient();
            }

            public static IBindGroup GetOrCreateGroup(string name) =>
                Instance.mainClient.GetOrCreateGroup(name);

            public static IBindGroup GetBindGroup(string name) =>
                Instance.mainClient.GetBindGroup(name);

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
            /// Builds dictionary of controls from the set of MyKeys enums and a couple custom controls for the mouse wheel.
            /// </summary>
            private static Control[] GenerateControls()
            {
                List<Control> controlList = new List<Control>(200);

                controlList.Add(new Control("MousewheelUp", "MwUp", controlList.Count,
                    () => MyAPIGateway.Input.DeltaMouseScrollWheelValue() > 0, true));
                controlList.Add(new Control("MousewheelDown", "MwDn", controlList.Count,
                    () => MyAPIGateway.Input.DeltaMouseScrollWheelValue() < 0, true));

                controlDict.Add("mousewheelup", controlList[0]);
                controlDict.Add("mousewheeldown", controlList[1]);

                controlDictDisp.Add("mwup", controlList[0]);
                controlDictDisp.Add("mwdn", controlList[1]);

                foreach (MyKeys seKey in Enum.GetValues(typeof(MyKeys)))
                {
                    if (!controlBlacklist.Contains(seKey))
                    {
                        Control con = new Control(seKey, controlList.Count);
                        string name = con.Name.ToLower(), disp = con.DisplayName.ToLower();

                        if (!controlDict.ContainsKey(name))
                        {
                            controlDict.Add(name, con);
                            controlDictDisp.Add(name, con);
                            controlList.Add(con);
                        }
                    }
                }

                return controlList.ToArray();
            }

            public static IControl[] GetCombo(IList<string> names)
            {
                IControl[] combo = new IControl[names.Count];

                for (int n = 0; n < names.Count; n++)
                    combo[n] = GetControl(names[n]);

                return combo;
            }

            public static int[] GetComboIndices(IList<string> names)
            {
                int[] combo = new int[names.Count];

                for (int n = 0; n < names.Count; n++)
                    combo[n] = GetControl(names[n]).Index;

                return combo;
            }

            public static IControl[] GetCombo(IList<int> indices)
            {
                IControl[] combo = new IControl[indices.Count];

                for (int n = 0; n < indices.Count; n++)
                    combo[n] = Controls[indices[n]];

                return combo; ;
            }

            public static int[] GetComboIndices(IList<IControl> controls)
            {
                int[] indices = new int[controls.Count];

                for (int n = 0; n < controls.Count; n++)
                    indices[n] = controls[n].Index;

                return indices;
            }

            /// <summary>
            /// Tries to get a key combo using a list of control names.
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