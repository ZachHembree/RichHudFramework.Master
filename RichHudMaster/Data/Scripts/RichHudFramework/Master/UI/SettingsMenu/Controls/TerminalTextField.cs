using System;
using RichHudFramework.UI.Rendering;
using VRageMath;
using ApiMemberAccessor = System.Func<object, int, object>;

namespace RichHudFramework.UI.Server
{
    public enum TextFieldAccessors : int
    {
        CharFilterFunc = 16,
    }

    /// <summary>
    /// One-line text field with a configurable input filter delegate. Designed to mimic the appearance of the text field
    /// in the SE terminal.
    /// </summary>
    public class TerminalTextField : TerminalValue<string, TerminalTextField>
    {
        /// <summary>
        /// Invoked whenver a change occurs to a control that requires a response, like a change
        /// to a value.
        /// </summary>
        public override event Action OnControlChanged;

        /// <summary>
        /// The name of the control as it appears in the terminal.
        /// </summary>
        public override string Name { get { return name.TextBoard.ToString(); } set { name.TextBoard.SetText(value); } }

        /// <summary>
        /// The contents of the textbox.
        /// </summary>
        public override string Value
        {
            get { return textBox.TextBoard.ToString(); }
            set { textBox.TextBoard.SetText(value); }
        }

        /// <summary>
        /// Used to periodically update the value associated with the control. Optional.
        /// </summary>
        public override Func<string> CustomValueGetter { get; set; }

        /// <summary>
        /// Restricts the range of characters allowed for input.
        /// </summary>
        public Func<char, bool> CharFilterFunc { get { return textBox.CharFilterFunc; } set { textBox.CharFilterFunc = value; } }

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

        private readonly Label name;
        private readonly TextBox textBox;
        private readonly TexturedBox background, highlight;
        private readonly Utils.Stopwatch refreshTimer;
        private string lastValue;
        private bool controlUpdating, valueChanged;

        public TerminalTextField(IHudParent parent = null) : base(parent)
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

            var border = new BorderBox(background)
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

            Name = "TextBox";
            Padding = new Vector2(40f, 0f);
            Size = new Vector2(319f, 62f);

            textBox.TextBoard.OnTextChanged += TextChanged;
            refreshTimer = new Utils.Stopwatch();
            refreshTimer.Start();
        }

        private void TextChanged()
        {
            valueChanged = !controlUpdating;
        }

        protected override void Layout()
        {
            if (!textBox.InputOpen)
            {
                if (refreshTimer.ElapsedMilliseconds > 1000)
                {
                    if (CustomValueGetter != null && lastValue != CustomValueGetter())
                        Value = CustomValueGetter();

                    refreshTimer.Reset();
                }

                if (valueChanged)
                {
                    controlUpdating = true;
                    lastValue = Value;
                    OnControlChanged?.Invoke();

                    controlUpdating = false;
                    valueChanged = false;
                }
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