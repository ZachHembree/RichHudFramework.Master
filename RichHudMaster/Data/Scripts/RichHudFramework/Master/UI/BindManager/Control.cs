using RichHudFramework.Internal;
using Sandbox.ModAPI;
using System;
using VRage.Input;

namespace RichHudFramework
{
    namespace UI.Server
    {
        public sealed partial class BindManager
        {
            /// <summary>
            /// General purpose button wrapper for MyKeys and anything else associated with a name and an IsPressed method.
            /// </summary>
            private class Control : IControl
            {
                public static readonly Control Default = new Control("default", "default", 0, () => false);

                /// <summary>
                /// Name of the control
                /// </summary>
                public string Name { get; }

                /// <summary>
                /// Name of the control as displayed in bind menu
                /// </summary>
                public string DisplayName { get; }

                /// <summary>
                /// Returns true if the control is being pressed
                /// </summary>
                public bool IsPressed { get; private set; }

                /// <summary>
                /// Returns true if the control was just pressed
                /// </summary>
                public bool IsNewPressed { get; private set; }

                /// <summary>
                /// Returns true if the control was just released
                /// </summary>
                public bool IsReleased { get; private set; }

                /// <summary>
                /// Returns true if the control doesn't represent a boolean value. For example, MwUp/Dn
                /// represent scroll wheel movement, but don't return an exact position/displacement.
                /// </summary>
                public bool Analog { get; }

                /// <summary>
                /// Returns analog value of the control, if it has one
                /// </summary>
                public float AnalogValue { get; private set; }

                /// <summary>
                /// Index of the control in the bind manager
                /// </summary>
                public int Index { get; }

                private readonly Func<bool> IsPressedFunc;
                private readonly Func<float> GetAnalogValueFunc;
                private bool wasPressed;

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

                public Control(MyJoystickButtonsEnum seKey, int index, bool analog = false)
                {
                    Name = seKey.ToString();
                    DisplayName = MyAPIGateway.Input.GetName(seKey);

                    if (DisplayName == null || DisplayName.Length == 0 || DisplayName[0] <= ' ')
                        DisplayName = Name;

                    Index = index;
                    IsPressedFunc = () => MyAPIGateway.Input.IsJoystickButtonPressed(seKey);
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

                public Control(string name, string friendlyName, int index, Func<bool> IsPressed, Func<float> GetAnalogValue)
                {
                    Name = name;
                    DisplayName = friendlyName;

                    Index = index;
                    IsPressedFunc = IsPressed;
                    GetAnalogValueFunc = GetAnalogValue;
                    Analog = true;
                }

                public void Update()
                {
                    wasPressed = IsPressed;
                    IsPressed = IsPressedFunc();
                    IsNewPressed = IsPressed && (!wasPressed || Analog);
                    AnalogValue = GetAnalogValueFunc?.Invoke() ?? 0f;
                }

                public void Reset()
                {
                    wasPressed = false;
                    IsPressed = false;
                    IsNewPressed = false;
                }

                public override string ToString()
                {
                    return Name;
                }
            }
        }
    }
}