using System;
using System.Text;
using VRage;
using VRageMath;
using GlyphFormatMembers = VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>;

namespace RichHudFramework.UI.Server
{
    /// <summary>
    /// A pair of horizontally aligned on and off bordered buttons used to indicate a boolean value. Made to
    /// resemble on/off button used in the SE terminal, sans name tag.
    /// </summary>
    public class OnOffButton : HudElementBase
    {   
        /// <summary>
        /// Distance between the on and off buttons
        /// </summary>
        public float ButtonSpacing { get { return buttonChain.Spacing; } set { buttonChain.Spacing = value; } }

        /// <summary>
        /// Color of the border surrounding the on and off buttons
        /// </summary>
        public Color BorderColor { get { return on.BorderColor; } set { on.BorderColor = value; off.BorderColor = value; } }

        /// <summary>
        /// Color used for the background for the unselected button
        /// </summary>
        public Color BackgroundColor { get; set; }

        /// <summary>
        /// Background color used to indicate the current selection
        /// </summary>
        public Color SelectionColor { get; set; }

        /// <summary>
        /// On button text
        /// </summary>
        public RichText OnText { get { return on.Text; } set { on.Text = value; } }

        /// <summary>
        /// Off button text
        /// </summary>
        public RichText OffText { get { return off.Text; } set { off.Text = value; } }

        /// <summary>
        /// Default glyph format used by the on and off buttons
        /// </summary>
        public GlyphFormat Format { get { return on.Format; } set { on.Format = value; off.Format = value; } }

        /// <summary>
        /// Current value of the on/off button
        /// </summary>
        public bool Value { get; set; }

        protected readonly BorderedButton on, off;
        protected readonly HudChain buttonChain;

        public OnOffButton(HudParentBase parent) : base(parent)
        {
            on = new BorderedButton()
            {
                Text = "On",
                Padding = Vector2.Zero,
                Size = new Vector2(71f, 49f),
                HighlightEnabled = true,
                UseFocusFormatting = false
            };

            on.BorderThickness = 2f;

            off = new BorderedButton()
            {
                Text = "Off",
                Padding = Vector2.Zero,
                Size = new Vector2(71f, 49f),
                HighlightEnabled = true,
                UseFocusFormatting = false
            };

            off.BorderThickness = 2f;

            buttonChain = new HudChain(false, this)
            {
                SizingMode = HudChainSizingModes.FitMembersBoth | HudChainSizingModes.FitChainBoth,
                Spacing = 9f,
                CollectionContainer = { on, off }
            };

            on.MouseInput.LeftClicked += ToggleValue;
            off.MouseInput.LeftClicked += ToggleValue;

            Size = new Vector2(200f, 50f);

            BackgroundColor = TerminalFormatting.OuterSpace;
            SelectionColor = TerminalFormatting.DullMint;
        }

        public OnOffButton() : this(null)
        { }

        private void ToggleValue(object sender, EventArgs args)
        {
            Value = !Value;
        }

        protected override void Layout()
        {
            Vector2 buttonSize = cachedSize - cachedPadding;
            buttonSize.X = buttonSize.X / 2f - buttonChain.Spacing;
            buttonChain.MemberMaxSize = buttonSize;

            if (Value)
            {
                on.Color = SelectionColor;
                off.Color = BackgroundColor;
            }
            else
            {
                off.Color = SelectionColor;
                on.Color = BackgroundColor;
            }
        }
    }
}