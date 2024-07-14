using System;

namespace RichHudFramework.UI.Server
{
    /// <summary>
    /// Labeled checkbox designed to mimic the appearance of checkboxes in the SE terminal.
    /// </summary>
    public class TerminalCheckbox : TerminalValue<bool>
    {
        /// <summary>
        /// Name of the checkbox as it appears on its label.
        /// </summary>
        public override string Name { get { return checkBox.NameBuilder.ToString(); } set { checkBox.NameBuilder.SetText(value); } }

        public override bool Value { get { return checkBox.IsBoxChecked; } set { checkBox.IsBoxChecked = value; } }

        /// <summary>
        /// Used to periodically update the value associated with the control. Optional.
        /// </summary>
        public override Func<bool> CustomValueGetter { get; set; }

        private readonly NamedCheckBox checkBox;
        
        public TerminalCheckbox()
        {
            checkBox = new NamedCheckBox();
            SetElement(checkBox);
        }

        public override void Update()
        {
            base.Update();

            if (ToolTip != null && !HudMain.Cursor.IsToolTipRegistered && checkBox.MouseInput.IsMousedOver)
                HudMain.Cursor.RegisterToolTip(ToolTip);
        }
    }
}