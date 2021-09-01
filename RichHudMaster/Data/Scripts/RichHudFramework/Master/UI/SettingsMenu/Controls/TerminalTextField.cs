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
        /// The name of the control as it appears in the terminal.
        /// </summary>
        public override string Name { get { return textElement.Name; } set { textElement.Name = value; } }

        /// <summary>
        /// The contents of the textbox.
        /// </summary>
        public override string Value
        {
            get { return textElement.TextField; }
            set { textElement.TextField = value; }
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

        public TerminalTextField()
        {
            textElement = new NamedTextField();
            SetElement(textElement);
        }

        public override void Update()
        {
            if (ToolTip != null && !HudMain.Cursor.IsToolTipRegistered && textElement.textField.IsMousedOver)
                HudMain.Cursor.RegisterToolTip(ToolTip);

            if (!textElement.FieldOpen)
                base.Update();
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
            public override float Width
            {
                get { return textField.Width + Padding.X; }
                set
                {
                    if (value > Padding.X)
                        value -= Padding.X;

                    textField.Width = value;
                    name.Width = value;
                }
            }

            public override float Height
            {
                get { return textField.Height + name.Height + Padding.Y; }
                set
                {
                    if (value > Padding.Y)
                        value -= Padding.Y;

                    textField.Height = value - name.Height;
                }
            }

            public string Name { get { return name.TextBoard.ToString(); } set { name.Text = value; } }

            public string TextField { get{ return textField.TextBoard.ToString(); } set { textField.Text = value; } }

            public Func<char, bool> CharFilterFunc { get { return textField.CharFilterFunc; } set { textField.CharFilterFunc = value; } }

            public bool FieldOpen { get { return textField.InputOpen; } }

            public readonly Label name;
            public readonly TextField textField;

            public NamedTextField(HudParentBase parent = null) : base(parent)
            {
                name = new Label(this)
                {
                    Format = TerminalFormatting.ControlFormat,
                    Text = "NewTextField",
                    AutoResize = false,
                    Height = 22f,
                    Padding = new Vector2(0f, 2f),
                    ParentAlignment = ParentAlignments.Top | ParentAlignments.InnerV | ParentAlignments.UsePadding
                };

                textField = new TextField(name) 
                {
                    ParentAlignment = ParentAlignments.Bottom,
                };

                Padding = new Vector2(40f, 0f);
            }
        }
    }
}