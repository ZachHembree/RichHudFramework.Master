using System;
using RichHudFramework.UI.Rendering;
using VRageMath;
using ApiMemberAccessor = System.Func<object, int, object>;

namespace RichHudFramework.UI.Server
{
    internal enum TextFieldAccessors : int
    {
        CharFilterFunc = 16,
    }

    public class TextField : TerminalValue<string, TextField>
    {
        public override event Action OnControlChanged;

        public override float Width
        {
            get { return background.Width + Padding.X; }
            set
            {
                if (value > Padding.X)
                    value -= Padding.X;

                background.Width = value;
                name.Width = value;
            }
        }

        public override float Height
        {
            get { return background.Height + name.Height + Padding.Y; }
            set
            {
                if (value > Padding.Y)
                    value -= Padding.Y;

                background.Height = value - name.Height;
            }
        }

        public override RichText Name { get { return name.TextBoard.GetText(); } set { name.TextBoard.SetText(value); } }
        public override string Value
        {
            get { return textBox.TextBoard.GetText().ToString(); }
            set { textBox.TextBoard.SetText(value); }
        }
        public override Func<string> CustomValueGetter { get; set; }
        public override Action<string> CustomValueSetter { get; set; }

        /// <summary>
        /// Used to restrict the range of characters allowed for input.
        /// </summary>
        public Func<char, bool> CharFilterFunc { get { return textBox.CharFilterFunc; } set { textBox.CharFilterFunc = value; } }

        private readonly Label name;
        private readonly TextBox textBox;
        private readonly TexturedBox background, highlight;
        private readonly BorderBox border;
        private readonly Utils.Stopwatch refreshTimer;

        public TextField(IHudParent parent = null) : base(parent)
        {
            name = new Label(this)
            {
                Format = RichHudTerminal.ControlFormat,
                Text = "NewTextField",
                AutoResize = false,
                Height = 22f,
                Offset = new Vector2(0f, 2f),
                ParentAlignment = ParentAlignments.Top | ParentAlignments.InnerV
            };

            background = new TexturedBox(name)
            {
                Color = new Color(42, 55, 63),
                ParentAlignment = ParentAlignments.Bottom,
            };

            border = new BorderBox(background)
            {
                Color = RichHudTerminal.BorderColor,
                Thickness = 1f,
                DimAlignment = DimAlignments.Both,
            };

            textBox = new TextBox(background)
            {
                Format = RichHudTerminal.ControlFormat,
                AutoResize = false,
                DimAlignment = DimAlignments.Both | DimAlignments.IgnorePadding,
                Padding = new Vector2(24f, 0f),
            };

            highlight = new TexturedBox(background)
            {
                Color = RichHudTerminal.HighlightOverlayColor,
                DimAlignment = DimAlignments.Both,
                Visible = false,
            };

            textBox.TextBoard.SetText("TextBox");
            Padding = new Vector2(40f, 0f);
            Size = new Vector2(319f, 62f);

            textBox.TextBoard.OnTextChanged += TextChanged;
            refreshTimer = new Utils.Stopwatch();
            refreshTimer.Start();
        }

        private void TextChanged()
        {
            OnControlChanged?.Invoke();
            CustomValueSetter?.Invoke(Value);
        }

        protected override void Draw()
        {
            if (refreshTimer.ElapsedMilliseconds > 2000)
            {
                if (CustomValueGetter != null && CustomValueSetter != null && Value != CustomValueGetter())
                    Value = CustomValueGetter();

                refreshTimer.Reset();
            }
        }

        protected override void HandleInput()
        {
            if (textBox.IsMousedOver)
            {
                highlight.Visible = true;
            }
            else
            {
                highlight.Visible = false;
            }
        }

        protected override object GetOrSetMember(object data, int memberEnum)
        {
            if (memberEnum < 16)
                return base.GetOrSetMember(data, memberEnum);
            else
            {
                switch ((TextFieldAccessors)memberEnum)
                {
                    case TextFieldAccessors.CharFilterFunc:
                        {
                            if (data == null)
                                return CharFilterFunc;
                            else
                                CharFilterFunc = data as Func<char, bool>;

                            break;
                        }
                }

                return null;
            }
        }
    }
}