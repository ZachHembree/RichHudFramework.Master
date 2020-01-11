using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage;
using VRage.Input;
using VRage.ModAPI;
using ApiMemberAccessor = System.Func<object, int, object>;

namespace RichHudFramework
{
    using ControlMembers = MyTuple<string, string, int, Func<bool>, bool, ApiMemberAccessor>;

    namespace UI.Server
    {
        public sealed partial class BindManager
        {
            /// <summary>
            /// General purpose button wrapper for MyKeys and anything else associated with a name and an IsPressed method.
            /// </summary>
            private class Control : IControl
            {
                public string Name { get; }
                public string DisplayName { get; }
                public bool IsPressed { get { return IsPressedFunc(); } }
                public bool Analog { get; }
                public int Index { get; }

                private readonly Func<bool> IsPressedFunc;

                public Control(MyKeys seKey, int index, bool analog = false)
                {
                    Name = seKey.ToString();
                    DisplayName = MyAPIGateway.Input.GetKeyName(seKey);

                    if (DisplayName == null || DisplayName.Length == 0 || DisplayName[0] <= ' ')
                        DisplayName = Name;

                    Index = index;
                    IsPressedFunc = () => MyAPIGateway.Input.IsKeyPress(seKey);
                    Analog = analog;
                }

                public Control(string name, string friendlyName, int index, Func<bool> IsPressed, bool analog = false)
                {
                    Name = name;
                    DisplayName = friendlyName;

                    Index = index;
                    IsPressedFunc = IsPressed;
                    Analog = analog;
                }

                public ControlMembers GetApiData()
                {
                    return new ControlMembers()
                    {
                        Item1 = Name,
                        Item2 = DisplayName,
                        Item3 = Index,
                        Item4 = IsPressedFunc,
                        Item5 = Analog
                    };
                }
            }
        }
    }
}