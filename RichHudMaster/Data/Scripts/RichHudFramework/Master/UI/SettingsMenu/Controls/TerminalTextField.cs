using System;
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
    public class TerminalTextField : TerminalValue<string>
    {
        /// <summary>
        /// Invoked whenver a change occurs to a control that requires a response, like a change
        /// to a value.
        /// </summary>
        public override event EventHandler ControlChanged;

        /// <summary>
        /// The name of the control as it appears in the terminal.
        /// </summary>
        public override string Name { get { return textElement.Name; } set { textElement.Name = value; } }

        /// <summary>
        /// The contents of the textbox.
        /// </summary>
        public override string Value
        {
            get { return inputValue; }
            set { textElement.textField.TextBoard.SetText(value); inputValue = value; }
        }

        /// <summary>
        /// Used to periodically update the value associated with the control. Optional.
        /// </summary>
        public override Func<string> CustomValueGetter { get; set; }

        /// <summary>
        /// Restricts the range of characters allowed for input.
        /// </summary>
        public Func<char, bool> CharFilterFunc { get { return textElement.CharFilterFunc; } set { textElement.CharFilterFunc = value; } }

        private readonly NamedTextField textElement;
        private string inputValue;

        public TerminalTextField()
        {
            textElement = new NamedTextField();
            SetElement(textElement);

            textElement.textField.ValueChanged += OnTextFieldChanged;
        }

        public override void Update()
        {
            if (ToolTip != null && !HudMain.Cursor.IsToolTipRegistered && textElement.textField.IsMousedOver)
                HudMain.Cursor.RegisterToolTip(ToolTip);

            if (!textElement.FieldOpen || !textElement.Visible)
            {
                if (!controlUpdating && inputValue != lastValue)
                {
                    controlUpdating = true;
                    lastValue = Value;
                    ControlChanged?.Invoke(this, EventArgs.Empty);
                    controlUpdating = false;
                }

                if (CustomValueGetter != null)
                {
                    string newValue = CustomValueGetter();

                    if (newValue != inputValue)
                        Value = newValue;
                }
            }
        }

        private void OnTextFieldChanged(object sender, EventArgs args)
        {
            var field = sender as TextField;
            inputValue = field.TextBoard.ToString();
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

        private class NamedTextField : HudElementBase
        {
            public string Name { get { return name.TextBoard.ToString(); } set { name.Text = value; } }

            public Func<char, bool> CharFilterFunc { get { return textField.CharFilterFunc; } set { textField.CharFilterFunc = value; } }

            public bool FieldOpen { get { return textField.InputOpen; } }

            public readonly Label name;
            public readonly TextField textField;

            public NamedTextField(HudParentBase parent = null) : base(parent)
            {
                name = new Label()
                {
                    Format = TerminalFormatting.ControlFormat,
                    Text = "NewTextField",
                    AutoResize = false,
                    Height = 22f,
                    Padding = new Vector2(0f, 2f),
                };

                textField = new TextField();

                var layout = new HudChain(true, this)
                {
                    DimAlignment = DimAlignments.UnpaddedSize,
                    SizingMode = HudChainSizingModes.FitMembersOffAxis,
                    CollectionContainer = { name, { textField, 1f } }
                };

                Height = name.Height + textField.Height;
                Padding = Vector2.Zero;
                DimAlignment = DimAlignments.UnpaddedWidth;
            }
        }
    }
}