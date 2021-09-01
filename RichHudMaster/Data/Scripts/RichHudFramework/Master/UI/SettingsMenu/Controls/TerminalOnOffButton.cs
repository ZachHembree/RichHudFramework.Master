using RichHudFramework.UI.Rendering;
using System;
using System.Collections.Generic;
using System.Text;
using VRage;
using ApiMemberAccessor = System.Func<object, int, object>;
using GlyphFormatMembers = VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>;

namespace RichHudFramework.UI.Server
{
    using RichStringMembers = MyTuple<StringBuilder, GlyphFormatMembers>;

    public enum OnOffButtonAccessors : int
    {
        OnText = 16,
        OffText = 17,
    }

    /// <summary>
    /// On/Off toggle designed to mimic the appearance of the On/Off button in the SE Terminal.
    /// </summary>
    public class TerminalOnOffButton : TerminalValue<bool>
    {
        /// <summary>
        /// The name of the control as it appears in the terminal.
        /// </summary>
        public override string Name { get { return onOffButton.Name.ToString(); } set { onOffButton.Name = value; } }

        /// <summary>
        /// On button text
        /// </summary>
        public string OnText { get { return onOffButton.OnText.ToString(); } set { onOffButton.OnText = value; } }

        /// <summary>
        /// Off button text
        /// </summary>
        public string OffText { get { return onOffButton.OnText.ToString(); } set { onOffButton.OnText = value; } }

        /// <summary>
        /// Value associated with the control.
        /// </summary>
        public override bool Value { get { return onOffButton.Value; } set { onOffButton.Value = value; } }

        /// <summary>
        /// Used to periodically update the value associated with the control. Optional.
        /// </summary>
        public override Func<bool> CustomValueGetter { get; set; }

        protected readonly NamedOnOffButton onOffButton;

        public TerminalOnOffButton()
        {
            onOffButton = new NamedOnOffButton();
            SetElement(onOffButton);
        }

        public override void Update()
        {
            if (ToolTip != null && !HudMain.Cursor.IsToolTipRegistered && onOffButton.MouseInput.IsMousedOver)
                HudMain.Cursor.RegisterToolTip(ToolTip);
        }

        protected override object GetOrSetMember(object data, int memberEnum)
        {
            if (memberEnum < 16)
                return base.GetOrSetMember(data, memberEnum);
            else
            {
                var member = (OnOffButtonAccessors)memberEnum;

                switch (member)
                {
                    case OnOffButtonAccessors.OffText:
                        {
                            if (data == null)
                                return OffText;
                            else
                                OffText = data as string;

                            break;
                        }
                    case OnOffButtonAccessors.OnText:
                        {
                            if (data == null)
                                return OnText;
                            else
                                OnText = data as string;

                            break;
                        }
                }

                return null;
            }
        }
    }
}