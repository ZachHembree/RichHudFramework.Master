using RichHudFramework.UI.Rendering;
using System;
using System.Collections.Generic;
using System.Text;
using VRage;
using ApiMemberAccessor = System.Func<object, int, object>;
using EventAccessor = VRage.MyTuple<bool, System.Action>;
using GlyphFormatMembers = VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>;

namespace RichHudFramework.UI.Server
{
    using RichStringMembers = MyTuple<StringBuilder, GlyphFormatMembers>;

    public enum SliderSettingsAccessors : int
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
    public class TerminalSlider : TerminalValue<float>
    {
        /// <summary>
        /// The name of the control as rendred in the terminal.
        /// </summary>
        public override string Name { get { return sliderBox.Name.ToString(); } set { sliderBox.Name =value; } }

        /// <summary>
        /// Text indicating the current value of the slider. Does not automatically reflect changes to the slider value.
        /// </summary>
        public string ValueText { get { return sliderBox.ValueText.ToString(); } set { sliderBox.ValueText = value; } }

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

        private readonly NamedSliderBox sliderBox;

        public TerminalSlider()
        {
            sliderBox = new NamedSliderBox();
            SetElement(sliderBox);
        }

        public override void Update()
        {
            base.Update();

            if (ToolTip != null && !HudMain.Cursor.IsToolTipRegistered && sliderBox.IsMousedOver)
                HudMain.Cursor.RegisterToolTip(ToolTip);
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