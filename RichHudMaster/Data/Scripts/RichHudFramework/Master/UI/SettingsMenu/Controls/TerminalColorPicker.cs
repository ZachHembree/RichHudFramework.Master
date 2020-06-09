using System;
using RichHudFramework.UI.Rendering;
using VRageMath;

namespace RichHudFramework.UI.Server
{
    using UI;

    /// <summary>
    /// An RGB color picker using sliders for each channel. Designed to mimic the appearance of the color picker
    /// in the SE terminal.
    /// </summary>
    public class TerminalColorPicker : TerminalValue<Color, TerminalColorPicker>
    {
        /// <summary>
        /// The name of the color picker as it appears in the menu.
        /// </summary>
        public override string Name { get { return name.TextBoard.ToString(); } set { name.TextBoard.SetText(value); } }

        /// <summary>
        /// Used to periodically update the value associated with the control. Optional.
        /// </summary>
        public override Func<Color> CustomValueGetter { get; set; }

        public override float Width
        {
            get { return mainChain.Width + Padding.X; }

            set
            {
                display.Width = value - name.Width;
                colorSliders.Width = display.Width;
            }
        }

        public override float Height
        {
            get { return mainChain.Height + Padding.Y; }

            set
            {
                rText.Height = (value - displayChain.Height) / 3f;
                gText.Height = rText.Height;
                bText.Height = rText.Height;

                r.Height = rText.Height;
                g.Height = rText.Height;
                b.Height = rText.Height;
            }
        }

        private readonly Label name, rText, gText, bText;
        private readonly TexturedBox display;
        private readonly SliderBox r, g, b;
        private readonly HudChain<HudElementBase> mainChain, displayChain,
            sliderChain, colorText, colorSliders;

        public TerminalColorPicker(IHudParent parent = null) : base(parent)
        {
            name = new Label()
            {
                Format = GlyphFormat.Blueish.WithSize(1.08f),
                Text = "NewColorPicker",
                AutoResize = false,
                Size = new Vector2(88f, 22f)
            };

            display = new TexturedBox()
            {
                Width = 231f,
            };

            var dispBorder = new BorderBox(display)
            {
                Color = Color.White,
                Thickness = 1f,
                DimAlignment = DimAlignments.Both,
            };

            displayChain = new HudChain<HudElementBase>()
            {
                AutoResize = true,
                Height = 22f,
                Spacing = 0f,
                ChildContainer = { name, display }
            };

            rText = new Label() { AutoResize = false, Format = RichHudTerminal.ControlFormat, Height = 47f };
            gText = new Label() { AutoResize = false, Format = RichHudTerminal.ControlFormat, Height = 47f };
            bText = new Label() { AutoResize = false, Format = RichHudTerminal.ControlFormat, Height = 47f };

            colorText = new HudChain<HudElementBase>()
            {
                AlignVertical = true,
                AutoResize = true,
                Width = 87f,
                Spacing = 5f,
                ChildContainer = { rText, gText, bText }
            };

            r = new SliderBox() { Min = 0f, Max = 255f, Height = 47f };
            g = new SliderBox() { Min = 0f, Max = 255f, Height = 47f };
            b = new SliderBox() { Min = 0f, Max = 255f, Height = 47f };

            colorSliders = new HudChain<HudElementBase>()
            {
                AlignVertical = true,
                AutoResize = true,
                Width = 231f,
                Spacing = 5f,
                ChildContainer = { r, g, b }
            };

            sliderChain = new HudChain<HudElementBase>()
            {
                colorText,
                colorSliders,
            };

            mainChain = new HudChain<HudElementBase>(this)
            {
                AlignVertical = true,
                AutoResize = true,
                Spacing = 5f,
                Size = new Vector2(318f, 171f),
                ChildContainer =
                {
                    displayChain,
                    sliderChain,
                }
            };
        }

        public override void Reset()
        {
            name.TextBoard.Clear();
            rText.TextBoard.Clear();
            gText.TextBoard.Clear();
            bText.TextBoard.Clear();
            base.Reset();
        }

        protected override void Layout()
        {
            Color color = new Color()
            {
                R = (byte)r.Current.Round(0),
                G = (byte)g.Current.Round(0),
                B = (byte)b.Current.Round(0),
                A = 255
            };

            rText.TextBoard.SetText($"R: {color.R}");
            gText.TextBoard.SetText($"G: {color.G}");
            bText.TextBoard.SetText($"B: {color.B}");

            display.Color = color;

            base.Layout();
        }
    }
}