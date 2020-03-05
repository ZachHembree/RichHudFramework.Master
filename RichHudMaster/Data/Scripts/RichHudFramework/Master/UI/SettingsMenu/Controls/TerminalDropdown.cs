using System;
using System.Text;
using VRage;
using VRageMath;
using ApiMemberAccessor = System.Func<object, int, object>;
using GlyphFormatMembers = VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>;

namespace RichHudFramework.UI.Server
{
    using CollectionData = MyTuple<Func<int, ApiMemberAccessor>, Func<int>>;
    using RichStringMembers = MyTuple<StringBuilder, GlyphFormatMembers>;

    /// <summary>
    /// A dropdown list with a label. Designed to mimic the appearance of the dropdown in the SE terminal.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class TerminalDropdown<T> : TerminalValue<ListBoxEntry<T>, TerminalDropdown<T>>
    {
        /// <summary>
        /// The name of the control as it appears in the terminal.
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
        /// Dropdown instance backing this control.
        /// </summary>
        public Dropdown<T> List { get; }

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
        private readonly TexturedBox highlight;
        private readonly BorderBox border;

        public TerminalDropdown(IHudParent parent = null) : base(parent)
        {
            name = new Label()
            {
                Format = RichHudTerminal.ControlFormat,
                Text = "NewDropdown",
                AutoResize = false,
                Height = 24f,
            };

            List = new Dropdown<T>()
            {
                Format = RichHudTerminal.ControlFormat,
            };

            List.listBox.scrollBox.MinimumSize = Vector2.Zero;

            border = new BorderBox(List)
            {
                Color = RichHudTerminal.BorderColor,
                Thickness = 1f,
                DimAlignment = DimAlignments.Both | DimAlignments.IgnorePadding,
            };

            highlight = new TexturedBox(List)
            {
                Color = RichHudTerminal.HighlightOverlayColor,
                DimAlignment = DimAlignments.Both,
                Visible = false,
            };

            hudChain = new HudChain<HudElementBase>(this)
            {
                AutoResize = true,
                AlignVertical = true,
                ChildContainer = { name, List },
            };

            Padding = new Vector2(20f, 0f);
            Size = new Vector2(250f, 66f);
        }

        protected override void HandleInput()
        {
            if (List.IsMousedOver || List.Open)
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