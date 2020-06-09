using System;
using System.Text;
using VRage;
using VRageMath;
using ApiMemberAccessor = System.Func<object, int, object>;
using GlyphFormatMembers = VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>;

namespace RichHudFramework.UI.Server
{
    public enum ListControlAccessors : int
    {
        ListAccessors = 16,
    }

    /// <summary>
    /// A fixed size list box with a label. Designed to mimic the appearance of the list box in the SE terminal.
    /// </summary>
    public class TerminalList<T> : TerminalValue<ListBoxEntry<T>, TerminalList<T>>
    {
        /// <summary>
        /// The name of the listbox as it appears in the terminal.
        /// </summary>
        public override string Name { get { return name.TextBoard.ToString(); } set { name.TextBoard.SetText(value); } }

        /// <summary>
        /// Currently selected list member.
        /// </summary>
        public override ListBoxEntry<T> Value { get { return List.Selection; } set { List.SetSelection(value); } }

        /// <summary>
        /// Used to periodically update the value associated with the control. Optional.
        /// </summary>
        public override Func<ListBoxEntry<T>> CustomValueGetter { get; set; }

        /// <summary>
        /// ListBox backing the control.
        /// </summary>
        public ListBox<T> List { get; }

        public override float Width
        {
            get { return hudChain.Width; }
            set { hudChain.Width = value; }
        }

        public override float Height
        {
            get { return hudChain.Height; }
            set { List.Height = value - name.Height - Padding.Y; }
        }

        public override Vector2 Padding
        {
            get { return hudChain.Padding; }
            set { hudChain.Padding = value; }
        }

        private readonly Label name;
        private readonly HudChain<HudElementBase> hudChain;

        public TerminalList(IHudParent parent = null) : base(parent)
        {
            name = new Label()
            {
                Format = RichHudTerminal.ControlFormat,
                Text = "NewListBox",
                AutoResize = false,
                Height = 24f,
            };

            List = new ListBox<T>()
            {
                Format = RichHudTerminal.ControlFormat,
            };

            hudChain = new HudChain<HudElementBase>(this)
            {
                AutoResize = true,
                AlignVertical = true,
                ChildContainer = { name, List },
            };

            Padding = new Vector2(20f, 0f);
            Size = new Vector2(250f, 200f);
        }

        public override void Reset()
        {
            name.TextBoard.Clear();
            List.Clear();
            base.Reset();
        }

        protected override void Layout()
        {
            List.Color = RichHudTerminal.ListBgColor.SetAlphaPct(HudMain.UiBkOpacity);

            SliderBar slider = List.scrollBox.scrollBar.slide;
            slider.BarColor = RichHudTerminal.ScrollBarColor.SetAlphaPct(HudMain.UiBkOpacity);

            base.Layout();
        }

        protected override object GetOrSetMember(object data, int memberEnum)
        {
            if (memberEnum < 16)
            {
                return base.GetOrSetMember(data, memberEnum);
            }
            else
            {
                var member = (ListControlAccessors)memberEnum;

                if (member == ListControlAccessors.ListAccessors)
                    return (ApiMemberAccessor)List.GetOrSetMember;

                return null;
            }
        }
    }
}