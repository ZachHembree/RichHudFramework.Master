﻿using System;
using System.Collections.Generic;
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
    public class TerminalDropdown<TValue> : TerminalValue<ListBoxEntry<TValue>>
    {
        /// <summary>
        /// The name of the control as it appears in the terminal.
        /// </summary>
        public override string Name { get { return dropdown.Name.ToString(); } set { dropdown.Name = value; } }

        /// <summary>
        /// Currently selected list member.
        /// </summary>
        public override ListBoxEntry<TValue> Value { get { return dropdown.Selection; } set { dropdown.SetSelection(value); } }

        /// <summary>
        /// Used to periodically update the value associated with the control. Optional.
        /// </summary>
        public override Func<ListBoxEntry<TValue>> CustomValueGetter { get; set; }

        /// <summary>
        /// List of entries in the dropdown.
        /// </summary>
        public IReadOnlyList<ListBoxEntry<TValue>> List => dropdown.ListEntries;

        private readonly NamedDropdown<TValue> dropdown;

        public TerminalDropdown()
        {
            dropdown = new NamedDropdown<TValue>();
            SetElement(dropdown);
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
                    return (ApiMemberAccessor)dropdown.GetOrSetMember;

                return null;
            }
        }

        private class NamedDropdown<T> : HudElementBase
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
            public IReadOnlyList<ListBoxEntry<T>> ListEntries => dropdown.ListEntries;

            /// <summary>
            /// Currently selected list member.
            /// </summary>
            public ListBoxEntry<T> Selection => dropdown.Selection;

            public override float Width
            {
                get { return hudChain.Width; }
                set { hudChain.Width = value; }
            }

            public override float Height
            {
                get { return hudChain.Height; }
            }

            public override Vector2 Padding
            {
                get { return hudChain.Padding; }
                set { hudChain.Padding = value; }
            }

            private readonly Label name;
            private readonly Dropdown<T> dropdown;
            private readonly HudChain hudChain;

            public NamedDropdown(HudParentBase parent = null) : base(parent)
            {
                name = new Label()
                {
                    Format = TerminalFormatting.ControlFormat,
                    Text = "NewDropdown",
                    AutoResize = false,
                    Height = 24f,
                };

                dropdown = new Dropdown<T>()
                {
                    Format = TerminalFormatting.ControlFormat,
                };

                hudChain = new HudChain(true, this)
                {
                    SizingMode = HudChainSizingModes.FitMembersOffAxis | HudChainSizingModes.FitChainBoth,
                    CollectionContainer = { name, dropdown },
                };

                Padding = new Vector2(20f, 0f);
                Size = new Vector2(250f, 66f);
            }

            public void SetSelection(ListBoxEntry<T> entry) =>
                dropdown.SetSelection(entry);

            public object GetOrSetMember(object data, int memberEnum) =>
                dropdown.GetOrSetMember(data, memberEnum);
        }
    }    
}