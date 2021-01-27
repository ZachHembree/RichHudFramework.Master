using System;
using VRageMath;

namespace RichHudFramework.UI.Server
{
    /// <summary>
    /// An RGB color picker using sliders for each channel. Designed to mimic the appearance of the color picker
    /// in the SE terminal.
    /// </summary>
    public class TerminalColorPicker : TerminalValue<Color>
    {
        /// <summary>
        /// The name of the color picker as it appears in the menu.
        /// </summary>
        public override string Name { get { return colorPicker.NameBuilder.ToString(); } set { colorPicker.NameBuilder.SetText(value); } }

        public override Color Value { get { return colorPicker.Color; } set { colorPicker.Color = value; } }

        /// <summary>
        /// Used to periodically update the value associated with the control. Optional.
        /// </summary>
        public override Func<Color> CustomValueGetter { get; set; }

        private readonly ColorPickerRGB colorPicker;

        public TerminalColorPicker()
        {
            colorPicker = new ColorPickerRGB();
            SetElement(colorPicker);
        }
    }
}