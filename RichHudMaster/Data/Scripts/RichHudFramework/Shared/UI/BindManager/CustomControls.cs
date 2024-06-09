using System;
using System.Collections.Generic;
using VRage;
using VRage.Input.Keyboard;
using VRage.Input;

namespace RichHudFramework
{
    namespace UI
    {
        public enum RichHudControls : int
        {
            MousewheelUp = 256,
            MousewheelDown = 257,

            LeftStickX = 258,
            LeftStickY = 259,

            RightStickX = 260,
            RightStickY = 261,

            ZLeft = 262,
            ZRight = 263,

            Slider1 = 264,
            Slider2 = 265
        }

        /// <summary>
        /// Universal interop container for various control enum types
        /// </summary>
        public struct ControlHandle
        {
            /// <summary>
            /// Index of the first gamepad key
            /// </summary>
            public const int GPKeysStart = (int)RichHudControls.Slider2 + 1;

            /// <summary>
            /// Unique RHF control ID
            /// </summary>
            public readonly int id;

            public ControlHandle(MyKeys id)
            {
                this.id = (int)id;
            }

            public ControlHandle(RichHudControls id)
            {
                this.id = (int)id;
            }

            public ControlHandle(MyJoystickButtonsEnum id)
            {
                this.id = GPKeysStart + (int)id;
            }

            public static implicit operator ControlHandle(MyKeys id)
            {
                return new ControlHandle(id);
            }

            public static implicit operator MyKeys(ControlHandle handle)
            {                    
                var id = (MyKeys)handle.id;
                
                if (Enum.IsDefined(typeof(MyKeys), id))
                    return id;
                else
                {
                    throw new Exception($"ControlHandle index {handle.id} cannot be converted to MyKeys.");
                }
            }

            public static implicit operator ControlHandle(RichHudControls id)
            {
                return new ControlHandle(id);
            }

            public static implicit operator RichHudControls(ControlHandle handle)
            {
                var id = (RichHudControls)handle.id;

                if (Enum.IsDefined(typeof(RichHudControls), id))
                    return id;
                else
                {
                    throw new Exception($"ControlHandle index {handle.id} cannot be converted to RichHudControls.");
                }
            }

            public static implicit operator ControlHandle(MyJoystickButtonsEnum id)
            {
                return new ControlHandle(id);
            }

            public static implicit operator MyJoystickButtonsEnum(ControlHandle handle)
            {
                var id = (MyJoystickButtonsEnum)handle.id;

                if (Enum.IsDefined(typeof(MyJoystickButtonsEnum), id))
                    return id;
                else
                {
                    throw new Exception($"ControlHandle index {handle.id} cannot be converted to MyJoystickButtonsEnum.");
                }
            }

            public static implicit operator int(ControlHandle handle)
            {
                return handle.id;
            }

            public override int GetHashCode()
            {
                return id.GetHashCode();
            }
        }
    }
}