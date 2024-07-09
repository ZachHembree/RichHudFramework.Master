using RichHudFramework.UI.Rendering;
using VRageMath;

namespace RichHudFramework.UI.Server
{
    /// <summary>
    /// Clickable button. Mimics the appearance of the terminal button in the SE terminal.
    /// </summary>
    public class TerminalButton : TerminalControlBase
    {
        /// <summary>
        /// Invoked when the button is clicked.
        /// </summary>
        public override event EventHandler ControlChanged;

        /// <summary>
        /// The name of the control as it appears in the terminal.
        /// </summary>
        public override string Name { get { return button.TextBoard.ToString(); } set { button.TextBoard.SetText(value); } }

        /// <summary>
        /// Text formatting applied to button text.
        /// </summary>
        public GlyphFormat Format { get { return button.Format; } set { button.Format = value; } }

        /// <summary>
        /// If true, the the button will highlight when moused over.
        /// </summary>
        public bool HighlightEnabled { get { return button.HighlightEnabled; } set { button.HighlightEnabled = value; } }

        public IMouseInput MouseInput => button.MouseInput;

        private readonly BorderedButton button;
        
        public TerminalButton()
        {
            button = new BorderedButton() 
            {
                DimAlignment = DimAlignments.UnpaddedWidth,
                Padding = Vector2.Zero
            };

            SetElement(button);

            MouseInput.LeftClicked += (sender, args) => ControlChanged?.Invoke(sender, args);          
        }

        public override void Update()
        {
            if (ToolTip != null && !HudMain.Cursor.IsToolTipRegistered && button.MouseInput.IsMousedOver)
                HudMain.Cursor.RegisterToolTip(ToolTip);
        }
    }
}