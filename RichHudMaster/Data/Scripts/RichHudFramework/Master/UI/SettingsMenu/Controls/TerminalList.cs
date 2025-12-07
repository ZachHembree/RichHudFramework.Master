using System;
using System.Collections.Generic;
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
    public class TerminalList<TValue> : TerminalValue<ListBoxEntry<TValue>>
    {
        /// <summary>
        /// The name of the control as it appears in the terminal.
        /// </summary>
        public override string Name { get { return subtype.Name.ToString(); } set { subtype.Name = value; } }

        /// <summary>
        /// Currently selected list member.
        /// </summary>
        public override ListBoxEntry<TValue> Value { get { return subtype.Selection; } set { subtype.SetSelection(value); } }

        /// <summary>
        /// Used to periodically update the value associated with the control. Optional.
        /// </summary>
        public override Func<ListBoxEntry<TValue>> CustomValueGetter { get; set; }

        /// <summary>
        /// List of entries in the dropdown.
        /// </summary>
        public ListBox<TValue> List => subtype.listBox;

        private readonly NamedListBox<TValue> subtype;

        public TerminalList()
        {
            subtype = new NamedListBox<TValue>();
            SetElement(subtype);
        }

        public override void Update()
        {
            base.Update();

            if (ToolTip != null && !HudMain.Cursor.IsToolTipRegistered && subtype.listBox.IsMousedOver)
                HudMain.Cursor.RegisterToolTip(ToolTip);
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
                    return (ApiMemberAccessor)subtype.GetOrSetMember;

                return null;
            }
        }

        private class NamedListBox<T> : HudElementBase
        {
            /// <summary>
            /// Text rendered by the label.
            /// </summary>
            public RichText Name { get { return name.TextBoard.GetText(); } set { name.TextBoard.SetText(value); } }

            /// <summary>
            /// Default formatting used by the label.
            /// </summary>
            public GlyphFormat Format { get { return name.TextBoard.Format; } set { name.TextBoard.Format = value; } }

            /// <summary>
            /// List of entries in the dropdown.
            /// </summary>
            public IReadOnlyList<ListBoxEntry<T>> ListEntries => listBox.EntryList;

            /// <summary>
            /// Currently selected list member.
            /// </summary>
            public ListBoxEntry<T> Selection => listBox.Value;

            public readonly ListBox<T> listBox;

            private readonly Label name;

            public NamedListBox(HudParentBase parent = null) : base(parent)
            {
                name = new Label()
                {
                    Format = TerminalFormatting.ControlFormat,
                    Text = "NewListBox",
                    AutoResize = false,
                    Height = 24f,
                };

                listBox = new ListBox<T>()
                {
                    Format = TerminalFormatting.ControlFormat,
                    Height = 172f
                };

                var layout = new HudChain(true, this)
                {
                    DimAlignment = DimAlignments.UnpaddedSize,
                    SizingMode = HudChainSizingModes.FitMembersOffAxis,
                    CollectionContainer = { { name , 0f }, { listBox, 1f } },
                    Spacing = 2f
                };

                Padding = new Vector2(0f, 0f);
                Size = new Vector2(250f, 200f);
            }

			protected override void Layout()
            {
                listBox.Color = TerminalFormatting.OuterSpace.SetAlphaPct(HudMain.UiBkOpacity);
                listBox.BarColor = TerminalFormatting.OuterSpace.SetAlphaPct(HudMain.UiBkOpacity);
            }

            public void SetSelection(ListBoxEntry<T> entry) =>
                listBox.SetSelection(entry);

            public object GetOrSetMember(object data, int memberEnum) =>
                listBox.GetOrSetMember(data, memberEnum);
        }
    }
}