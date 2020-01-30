using System;
using RichHudFramework.UI.Rendering;
using VRageMath;

namespace RichHudFramework.UI.Server
{
    public class TerminalButton : TerminalControlBase<TerminalButton>
    {
        public override event Action OnControlChanged;

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

        public override RichText Name { get { return button.TextBoard.GetText(); } set { button.TextBoard.SetText(value); } }
        public GlyphFormat Format { get { return button.Format; } set { button.Format = value; } }
        public bool HighlightEnabled { get { return button.HighlightEnabled; } set { button.HighlightEnabled = value; } }
        public IClickableElement MouseInput => button.MouseInput;

        public readonly TextBoxButton button;
        public readonly BorderBox border;
        private readonly TexturedBox highlight;

        public TerminalButton(IHudParent parent = null) : base(parent)
        {
            button = new TextBoxButton(this)
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