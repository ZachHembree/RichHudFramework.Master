﻿using System;

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
        public override string Name { get { return checkBox.TextBoard.ToString(); } set { checkBox.TextBoard.SetText(value); } }

        public override bool Value { get { return checkBox.BoxChecked; } set { checkBox.BoxChecked = value; } }

        /// <summary>
        /// Used to periodically update the value associated with the control. Optional.
        /// </summary>
        public override Func<bool> CustomValueGetter { get; set; }

        private readonly NamedCheckBox checkBox;
        
        public TerminalCheckbox()
        {
            checkBox = new NamedCheckBox() { AutoResize = true };
            Element = checkBox;
        }
    }
}