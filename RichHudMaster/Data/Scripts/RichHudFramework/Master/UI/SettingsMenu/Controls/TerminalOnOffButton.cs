using RichHudFramework.UI.Rendering;
using System;
using System.Collections.Generic;
using System.Text;
using VRage;
using VRageMath;
using ApiMemberAccessor = System.Func<object, int, object>;
using GlyphFormatMembers = VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>;

namespace RichHudFramework.UI.Server
{
    using RichStringMembers = MyTuple<StringBuilder, GlyphFormatMembers>;

    internal enum OnOffButtonAccessors : int
    {
        OnText = 16,
        OffText = 17,
    }

    /// <summary>
    /// On/Off toggle designed to mimic the appearance of the On/Off button in the SE Terminal.
    /// </summary>
    public class TerminalOnOffButton : TerminalValue<bool, TerminalOnOffButton>
    {
        /// <summary>
        /// The name of the control as it appears in the terminal.
        /// </summary>
        public override string Name { get { return NameBuilder.ToString(); } set { NameBuilder.SetText(value); } }
        public ITextBuilder NameBuilder => name.TextBoard;

        /// <summary>
        /// Value associated with the control.
        /// </summary>
        public override bool Value { get { return base.Value; } set { base.Value = value; } }

        /// <summary>
        /// Used to periodically update the value associated with the control. Optional.
        /// </summary>
        public override Func<bool> CustomValueGetter { get; set; }

        public string OnText { get { return on.Name; } set { on.Name = value; } }
        public string OffText { get { return off.Name; } set { off.Name = value; } }

        private readonly Label name;
        private readonly TerminalButton on, off;
        private readonly BorderBox selectionHighlight;
        private readonly HudChain<HudElementBase> buttonChain;

        public TerminalOnOffButton(IHudParent parent = null) : base(parent)
        {
            name = new Label()
            {
                Format = RichHudTerminal.ControlFormat,
                Text = "NewOnOffButton",
                AutoResize = false,
                Height = 22f,
            };

            on = new TerminalButton()
            {
                Name = "On",
                Padding = Vector2.Zero,
                Size = new Vector2(71f, 49f),
                HighlightEnabled = false,
            };

            on.border.Thickness = 2f;

            off = new TerminalButton()
            {
                Name = "Off",
                Padding = Vector2.Zero,
                Size = new Vector2(71f, 49f),
                HighlightEnabled = false,
            };

            off.border.Thickness = 2f;

            buttonChain = new HudChain<HudElementBase>()
            {
                AlignVertical = false,
                Spacing = 9f,
                ChildContainer = { on, off }
            };

            on.MouseInput.OnLeftClick += ToggleValue;
            off.MouseInput.OnLeftClick += ToggleValue;

            selectionHighlight = new BorderBox(buttonChain) 
            { Color = Color.White };

            var layout = new HudChain<HudElementBase>(this) 
            { 
                DimAlignment = DimAlignments.Width | DimAlignments.IgnorePadding,
                AutoResize = true,
                AlignVertical = true,
                Spacing = 8f,
                ChildContainer = { name, buttonChain }
            };

            Padding = new Vector2(20f, 0f);
            Size = new Vector2(250f, 72f);
        }

        private void ToggleValue()
        {
            Value = !Value;
        }

        protected override void Draw()
        {
            if (Value)
            {
                selectionHighlight.Size = on.Size;
                selectionHighlight.Offset = on.Offset;
            }
            else
            {
                selectionHighlight.Size = off.Size;
                selectionHighlight.Offset = off.Offset;
            }

            base.Draw();
        }

        protected override object GetOrSetMember(object data, int memberEnum)
        {
            if (memberEnum < 16)
                return base.GetOrSetMember(data, memberEnum);
            else
            {
                var member = (OnOffButtonAccessors)memberEnum;

                switch (member)
                {
                    case OnOffButtonAccessors.OffText:
                        {
                            if (data == null)
                                return OffText;
                            else
                                OffText = data as string;

                            break;
                        }
                    case OnOffButtonAccessors.OnText:
                        {
                            if (data == null)
                                return OnText;
                            else
                                OnText = data as string;

                            break;
                        }
                }

                return null;
            }
        }
    }
}