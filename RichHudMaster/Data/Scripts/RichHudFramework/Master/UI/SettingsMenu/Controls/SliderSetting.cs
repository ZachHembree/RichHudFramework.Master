using RichHudFramework.UI.Rendering;
using System;
using System.Text;
using VRage;
using VRageMath;
using ApiMemberAccessor = System.Func<object, int, object>;
using EventAccessor = VRage.MyTuple<bool, System.Action>;
using GlyphFormatMembers = VRage.MyTuple<VRageMath.Vector2I, int, VRageMath.Color, float>;

namespace RichHudFramework.UI.Server
{
    using RichStringMembers = MyTuple<StringBuilder, GlyphFormatMembers>;

    internal enum SliderSettingsAccessors : int
    {
        /// <summary>
        /// Float
        /// </summary>
        Min = 16,

        /// <summary>
        /// Float
        /// </summary>
        Max = 17,

        /// <summary>
        /// Float
        /// </summary>
        Percent = 18,

        /// <summary>
        /// RichStringMembers[]
        /// </summary>
        ValueText = 19,
    }

    public class SliderSetting : TerminalValue<float, SliderSetting>
    {
        public override event Action OnControlChanged;

        public override float Width
        {
            get { return slide.Width + Padding.X; }
            set
            {
                if (value > Padding.X)
                    value -= Padding.X;

                slide.Width = value;
            }
        }

        public override float Height
        {
            get { return slide.Height + Math.Max(name.Height, current.Height) + Padding.Y; }
            set
            {
                if (value > Padding.Y)
                    value -= Padding.Y;

                slide.Height = value - Math.Max(name.Height, current.Height);
            }
        }

        public float Min { get { return slide.Min; } set { slide.Min = value; } }
        public float Max { get { return slide.Max; } set { slide.Max = value; } }
        public override float Value { get { return slide.Current; } set { slide.Current = value; } }
        public float Percent { get { return slide.Percent; } set { slide.Percent = value; } }

        public override Func<float> CustomValueGetter { get; set; }
        public override Action<float> CustomValueSetter { get; set; }


        public override RichText Name { get { return name.TextBoard.GetText(); } set { name.TextBoard.SetText(value); } }
        public RichText ValueText { get { return current.TextBoard.GetText(); } set { current.TextBoard.SetText(value); } }

        private readonly Label name, current;
        private readonly SliderBox slide;
        private float lastValue;

        public SliderSetting(IHudParent parent = null) : base(parent)
        {
            slide = new SliderBox(this)
            {
                ParentAlignment = ParentAlignments.Bottom | ParentAlignments.InnerV,
            };

            name = new Label(this)
            {
                Format = RichHudTerminal.ControlText,
                Text = "NewSlideBox",
                Offset = new Vector2(0f, -18f),
                ParentAlignment = ParentAlignments.InnerH | ParentAlignments.Top | ParentAlignments.Left | ParentAlignments.UsePadding
            };

            current = new Label(this)
            {
                Format = RichHudTerminal.ControlText,
                Text = "Value",
                Offset = new Vector2(0f, -18f),
                ParentAlignment = ParentAlignments.InnerH | ParentAlignments.Top | ParentAlignments.Right | ParentAlignments.UsePadding
            };

            lastValue = Value;
            Padding = new Vector2(40f, 0f);
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

        protected override object GetOrSetMember(object data, int memberEnum)
        {
            if (memberEnum < 16)
                return base.GetOrSetMember(data, memberEnum);
            else
            {
                var member = (SliderSettingsAccessors)memberEnum;

                switch (member)
                {
                    case SliderSettingsAccessors.Min:
                        {
                            if (data == null)
                                return Min;
                            else
                                Min = (float)data;

                            break;
                        }
                    case SliderSettingsAccessors.Max:
                        {
                            if (data == null)
                                return Max;
                            else
                                Max = (float)data;

                            break;
                        }
                    case SliderSettingsAccessors.Percent:
                        {
                            if (data == null)
                                return Percent;
                            else
                                Percent = (float)data;

                            break;
                        }
                    case SliderSettingsAccessors.ValueText:
                        {
                            if (data == null)
                                return ValueText.GetApiData();
                            else
                                ValueText = new RichText((RichStringMembers[])data);

                            break;
                        }
                }

                return null;
            }
        }
    }
}