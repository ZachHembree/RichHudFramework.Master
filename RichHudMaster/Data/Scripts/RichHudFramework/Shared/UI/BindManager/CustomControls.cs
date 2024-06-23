using System;
using VRage.Input;
using RichHudFramework.UI.Server;
using RichHudFramework.UI.Client;

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

            /// <summary>
            /// Left trigger
            /// </summary>
            ZLeft = 262,

            /// <summary>
            /// Right trigger
            /// </summary>
            ZRight = 263,

            Slider1 = 264,

            Slider2 = 265,

            ReservedEnd = 383,

            DPadLeft = ReservedEnd + MyJoystickButtonsEnum.JDLeft,

            DPadRight = ReservedEnd + MyJoystickButtonsEnum.JDRight,

            DPadUp = ReservedEnd + MyJoystickButtonsEnum.JDUp,

            DPadDown = ReservedEnd + MyJoystickButtonsEnum.JDDown,

            GpadA = ReservedEnd + MyJoystickButtonsEnum.J01,
        }

        /// <summary>
        /// Universal interop container for <see cref="MyKeys"/>, <see cref="RichHudControls"/> and
        /// <see cref="MyJoystickButtonsEnum"/>
        /// </summary>
        public struct ControlHandle
        {
            /// <summary>
            /// Index of the first gamepad key
            /// </summary>
            public const int GPKeysStart = (int)RichHudControls.ReservedEnd + 1;

            /// <summary>
            /// Returns interface to underlying <see cref="IControl"/>
            /// </summary>
            public IControl Control => BindManager.GetControl(this);

            /// <summary>
            /// Unique RHF control ID
            /// </summary>
            public readonly int id;

            public ControlHandle(string controlName)
            {
                this.id = BindManager.GetControl(controlName).Index;
            }

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

            public static implicit operator ControlHandle(string controlName)
            {
                return new ControlHandle(controlName);
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