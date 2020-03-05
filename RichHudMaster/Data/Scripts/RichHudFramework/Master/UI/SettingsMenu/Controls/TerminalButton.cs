using RichHudFramework.UI.Rendering;
using System;
using VRageMath;

namespace RichHudFramework.UI.Server
{
    /// <summary>
    /// Clickable button. Mimics the appearance of the terminal button in the SE terminal.
    /// </summary>
    public class TerminalButton : TerminalControlBase<TerminalButton>
    {
        /// <summary>
        /// Invoked when the button is clicked.
        /// </summary>
        public override event Action OnControlChanged;

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

        public override float Width
        {
            get { return button.Width; }
            set
            {
                if (value > Padding.X)
                    value -= Padding.X;

                button.Width = value;
            }
        }

        public override float Height
        {
            get { return button.Height; }
            set
            {
                if (value > Padding.Y)
                    value -= Padding.Y;

                button.Height = value;
            }
        }

        public readonly LabelBoxButton button;
        public readonly BorderBox border;
        private readonly TexturedBox highlight;

        public TerminalButton(IHudParent parent = null) : base(parent)
        {
            button = new LabelBoxButton(this)
            {
                AutoResize = false,
                Color = new Color(42, 55, 63),
                TextPadding = new Vector2(32f, 0f),
                HighlightEnabled = false,
            };

            border = new BorderBox(button)
            {
                Color = RichHudTerminal.BorderColor,
                Thickness = 1f,
                DimAlignment = DimAlignments.Both | DimAlignments.IgnorePadding,
            };

            highlight = new TexturedBox(button)
            {
                Color = RichHudTerminal.HighlightOverlayColor,
                DimAlignment = DimAlignments.Both,
                Visible = false,
            };

            Padding = new Vector2(37f, 0f);
            Size = new Vector2(253f, 50f);
            MouseInput.OnLeftClick += () => OnControlChanged?.Invoke();

            Format = RichHudTerminal.ControlFormat.WithAlignment(TextAlignment.Center);
            Name = "NewTerminalButton";
        }

        protected override void HandleInput()
        {
            if (button.IsMousedOver)
            {
                highlight.Visible = true;
            }
            else
            {
                highlight.Visible = false;
            }
        }
    }
}