using RichHudFramework.UI.Rendering;
using System;
using System.Text;
using VRage;
using VRageMath;
using ApiMemberAccessor = System.Func<object, int, object>;
using GlyphFormatMembers = VRage.MyTuple<VRageMath.Vector2I, int, VRageMath.Color, float>;

namespace RichHudFramework.UI.Server
{
    using RichStringMembers = MyTuple<StringBuilder, GlyphFormatMembers>;

    internal enum OnOffButtonAccessors : int
    {
        OnText = 16,
        OffText = 17,
    }

    /// <summary>
    /// Boolean toggle designed to mimic the appearance of the On/Off button in the SE Terminal.
    /// </summary>
    public class OnOffButton : TerminalValue<bool, OnOffButton>
    {
        public override event Action OnControlChanged;

        public override RichText Name { get { return NameBuilder.GetText(); } set { NameBuilder.SetText(value); } }
        public ITextBuilder NameBuilder => name.TextBoard;

        public override bool Value { get { return base.Value; } set { base.Value = value; } }
        public override Func<bool> CustomValueGetter { get; set; }
        public override Action<bool> CustomValueSetter { get; set; }

        public RichText OnText { get { return on.Name; } set { on.Name = value; } }
        public RichText OffText { get { return off.Name; } set { off.Name = value; } }

        private readonly Label name;
        private readonly TerminalButton on, off;
        private readonly BorderBox selectionHighlight;
        private readonly HudChain<HudElementBase> layout, buttonChain;
        private bool lastValue;

        public OnOffButton(IHudParent parent = null) : base(parent)
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

            layout = new HudChain<HudElementBase>(this) 
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

            if (Value != lastValue)
            {
                lastValue = Value;
                OnControlChanged?.Invoke();
                CustomValueSetter?.Invoke(Value);
            }          

            if (CustomValueGetter != null && Value != CustomValueGetter())
                Value = CustomValueGetter();

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
                                return OffText.GetApiData();
                            else
                                OffText = new RichText((RichStringMembers[])data);

                            break;
                        }
                    case OnOffButtonAccessors.OnText:
                        {
                            if (data == null)
                                return OnText.GetApiData();
                            else
                                OnText = new RichText((RichStringMembers[])data);

                            break;
                        }
                }

                return null;
            }
        }
    }
}