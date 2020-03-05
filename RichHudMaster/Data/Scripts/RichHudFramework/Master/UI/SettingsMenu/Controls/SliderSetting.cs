using RichHudFramework.UI.Rendering;
using System;
using System.Collections.Generic;
using System.Text;
using VRage;
using VRageMath;
using ApiMemberAccessor = System.Func<object, int, object>;
using EventAccessor = VRage.MyTuple<bool, System.Action>;
using GlyphFormatMembers = VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>;

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
        /// string
        /// </summary>
        ValueText = 19,
    }

    /// <summary>
    /// Labeled slider used to set float values in the settings menu. Mimics the appearance of the slider in the
    /// SE terminal.
    /// </summary>
    public class SliderSetting : TerminalValue<float, SliderSetting>
    {
        /// <summary>
        /// The name of the control as rendred in the terminal.
        /// </summary>
        public override string Name { get { return name.TextBoard.ToString(); } set { name.TextBoard.SetText(value); } }

        /// <summary>
        /// Text indicating the current value of the slider. Does not automatically reflect changes to the slider value.
        /// </summary>
        public string ValueText { get { return current.TextBoard.ToString(); } set { current.TextBoard.SetText(value); } }

        /// <summary>
        /// Minimum configurable value for the slider.
        /// </summary>
        public float Min { get { return sliderBox.Min; } set { sliderBox.Min = value; } }

        /// <summary>
        /// Maximum configurable value for the slider.
        /// </summary>
        public float Max { get { return sliderBox.Max; } set { sliderBox.Max = value; } }

        /// <summary>
        /// Value currently set on the slider.
        /// </summary>
        public override float Value { get { return sliderBox.Current; } set { sliderBox.Current = value; } }

        /// <summary>
        /// Current slider value expreseed as a percentage between the min and maximum values.
        /// </summary>
        public float Percent { get { return sliderBox.Percent; } set { sliderBox.Percent = value; } }

        /// <summary>
        /// Used to periodically update the value associated with the control. Optional.
        /// </summary>
        public override Func<float> CustomValueGetter { get; set; }

        public override float Width
        {
            get { return sliderBox.Width + Padding.X; }
            set
            {
                if (value > Padding.X)
                    value -= Padding.X;

                sliderBox.Width = value;
            }
        }

        public override float Height
        {
            get { return sliderBox.Height + Math.Max(name.Height, current.Height) + Padding.Y; }
            set
            {
                if (value > Padding.Y)
                    value -= Padding.Y;

                sliderBox.Height = value - Math.Max(name.Height, current.Height);
            }
        }

        private readonly Label name, current;
        private readonly SliderBox sliderBox;
        private readonly TexturedBox highlight;

        public SliderSetting(IHudParent parent = null) : base(parent)
        {
            sliderBox = new SliderBox(this)
            {
                ParentAlignment = ParentAlignments.Bottom | ParentAlignments.InnerV,
                CaptureCursor = true,
            };

            highlight = new TexturedBox(sliderBox.background)
            {
                Color = RichHudTerminal.HighlightOverlayColor,
                DimAlignment = DimAlignments.Both,
                Visible = false
            };

            name = new Label(this)
            {
                Format = RichHudTerminal.ControlFormat,
                Text = "NewSlideBox",
                Offset = new Vector2(0f, -18f),
                ParentAlignment = ParentAlignments.InnerH | ParentAlignments.Top | ParentAlignments.Left | ParentAlignments.UsePadding
            };

            current = new Label(this)
            {
                Format = RichHudTerminal.ControlFormat,
                Text = "Value",
                Offset = new Vector2(0f, -18f),
                ParentAlignment = ParentAlignments.InnerH | ParentAlignments.Top | ParentAlignments.Right | ParentAlignments.UsePadding
            };

            Padding = new Vector2(40f, 0f);
        }

        protected override void HandleInput()
        {
            base.HandleInput();

            if (sliderBox.IsMousedOver)
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
                                return ValueText;
                            else
                                ValueText = data as string;

                            break;
                        }
                }

                return null;
            }
        }
    }
}