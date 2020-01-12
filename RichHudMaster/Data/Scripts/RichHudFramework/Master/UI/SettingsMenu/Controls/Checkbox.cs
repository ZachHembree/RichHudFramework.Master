using System;
using RichHudFramework.UI.Rendering;
using VRageMath;

namespace RichHudFramework.UI.Server
{
    /// <summary>
    /// Creates a named checkbox designed to mimic the appearance of checkboxes in the SE terminal.
    /// </summary>
    public class Checkbox : TerminalValue<bool, Checkbox>
    {
        public override event Action OnControlChanged;

        public override float Width
        {
            get { return chain.Width; }
            set { name.Width = value - box.Width - chain.Spacing; }
        }
        public override float Height { get { return chain.Height; } set { chain.Height = value; } }
        public override Vector2 Padding { get { return chain.Padding; } set { chain.Padding = value; } }

        public override RichText Name { get { return name.TextBoard.GetText(); } set { name.TextBoard.SetText(value); } }
        public override bool Value { get { return box.Visible; } set { box.Visible = value; } }
        public override Func<bool> CustomValueGetter { get; set; }
        public override Action<bool> CustomValueSetter { get; set; }

        private readonly HudChain<HudElementBase> chain;
        private readonly Label name;
        private readonly Button button;
        private readonly TexturedBox box;
        private bool lastValue;

        public Checkbox(IHudParent parent = null) : base(parent)
        {
            name = new Label()
            {
                Format = RichHudTerminal.ControlText.WithAlignment(TextAlignment.Right),
                Text = "NewCheckbox",
            };

            button = new Button()
            {
                Size = new Vector2(37f, 36f),
                Color = new Color(39, 52, 60),
                highlightColor = new Color(50, 60, 70),
            };

            var buttonBorder = new BorderBox(button)
            {
                Color = new Color(53, 66, 75),
                Thickness = 2f,
                DimAlignment = DimAlignments.Both,
            };

            box = new TexturedBox(button)
            {
                DimAlignment = DimAlignments.Both,
                Padding = new Vector2(16f, 16f),
                Color = new Color(114, 121, 139)
            };

            chain = new HudChain<HudElementBase>(this)
            {
                AutoResize = true,
                Spacing = 17f,
                ChildContainer =
                {
                    name,
                    button,
                }
            };

            button.MouseInput.OnLeftClick += ToggleValue;
            Height = 36f;
        }

        protected override void Draw()
        {
            if (Value != lastValue)
            {
                lastValue = Value;
                OnControlChanged?.Invoke();
                CustomValueSetter?.Invoke(Value);
            }

            if (CustomValueGetter != null && Value != CustomValueGetter())
                Value = CustomValueGetter();
        }

        private void ToggleValue()
        {
            Value = !Value;
        }
    }
}