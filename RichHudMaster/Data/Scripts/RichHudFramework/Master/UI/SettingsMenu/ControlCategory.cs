using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using VRage;
using VRageMath;
using ApiMemberAccessor = System.Func<object, int, object>;
using GlyphFormatMembers = VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>;

namespace RichHudFramework
{
    using RichStringMembers = MyTuple<StringBuilder, GlyphFormatMembers>;

    namespace UI.Server
    {
        using ControlMembers = MyTuple<
            ApiMemberAccessor, // GetOrSetMember
            object // ID
        >;
        using ControlContainerMembers = MyTuple<
            ApiMemberAccessor, // GetOrSetMember,
            MyTuple<object, Func<int>>, // Member List
            object // ID
        >;

        public class VertControlCategory : ControlCategory<TerminalControlBase>, IVertControlCategory
        {
            /// <summary>
            /// Read only collection of <see cref="TerminalControlBase"/>s assigned to this category
            /// </summary>
            public IReadOnlyList<TerminalControlBase> Controls => categoryElement.Members;

            /// <summary>
            /// Used to allow the addition of controls to vertical categories using collection-initializer syntax in
            /// conjunction with normal initializers.
            /// </summary>
            public IVertControlCategory ControlContainer => this;

            public VertControlCategory() : base(true)
            {
                var scrollBox = categoryElement.scrollBox;

                scrollBox.Padding = new Vector2(16f);
                scrollBox.Spacing = 30f;
            }

            /// <summary>
            /// Retrieves information used by the Framework API
            /// </summary>
            public override ControlContainerMembers GetApiData()
            {
                return new ControlContainerMembers()
                {
                    Item1 = GetOrSetMember,
                    Item2 = new MyTuple<object, Func<int>>
                    {
                        Item1 = (Func<int, ControlMembers>)(x => categoryElement.Members[x].GetApiData()),
                        Item2 = () => categoryElement.Members.Count
                    },
                    Item3 = this
                };
            }
        }

        public class ControlCategory : ControlCategory<ControlTile>, IControlCategory
        {
            /// <summary>
            /// Read only collection of <see cref="IControlTile"/>s assigned to this category
            /// </summary>
            public IReadOnlyList<ControlTile> Tiles => categoryElement.Members;

            /// <summary>
            /// Used to allow the addition of control tiles to categories using collection-initializer syntax in
            /// conjunction with normal initializers.
            /// </summary>
            public IControlCategory TileContainer => this;

            public ControlCategory() : base(false)
            { }

            IEnumerator<ControlTile> IEnumerable<ControlTile>.GetEnumerator() =>
                Tiles.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() =>
                Tiles.GetEnumerator();

            /// <summary>
            /// Retrieves information used by the Framework API
            /// </summary>
            public override ControlContainerMembers GetApiData()
            {
                return new ControlContainerMembers()
                {
                    Item1 = GetOrSetMember,
                    Item2 = new MyTuple<object, Func<int>>()
                    {
                        Item1 = (Func<int, ControlContainerMembers>)(x => categoryElement.Members[x].GetApiData()),
                        Item2 = () => categoryElement.Members.Count
                    },
                    Item3 = this
                };
            }
        }

        /// <summary>
        /// Horizontally scrolling list of control tiles.
        /// </summary>
        public abstract class ControlCategory<TMember> : ScrollBoxEntry, IControlCategory<TMember>
            where TMember : class, IScrollBoxEntry<HudElementBase>, new()
        {
            /// <summary>
            /// Category name
            /// </summary>
            public string HeaderText { get { return categoryElement.HeaderText; } set { categoryElement.HeaderText = value; } }

            /// <summary>
            /// Category information
            /// </summary>
            public string SubheaderText { get { return categoryElement.SubheaderText; } set { categoryElement.SubheaderText = value; } }

            /// <summary>
            /// Unique identifier.
            /// </summary>
            public object ID => this;

            protected readonly CategoryElement categoryElement;

            public ControlCategory(bool alignVertical)
            {
                categoryElement = new CategoryElement(alignVertical);
                SetElement(categoryElement);
            }

            /// <summary>
            /// Adds a <see cref="TInterface"/> to the category
            /// </summary>
            public void Add(TMember tile)
            {
                categoryElement.Add(tile);
            }

            public abstract ControlContainerMembers GetApiData();

            IEnumerator<TMember> IEnumerable<TMember>.GetEnumerator() =>
                categoryElement.Members.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() =>
                categoryElement.Members.GetEnumerator();

            protected virtual object GetOrSetMember(object data, int memberEnum)
            {
                var member = (ControlCatAccessors)memberEnum;

                switch (member)
                {
                    case ControlCatAccessors.HeaderText:
                        {
                            if (data == null)
                                return HeaderText;
                            else
                                HeaderText = data as string;

                            break;
                        }
                    case ControlCatAccessors.SubheaderText:
                        {
                            if (data == null)
                                return SubheaderText;
                            else
                                SubheaderText = data as string;

                            break;
                        }
                    case ControlCatAccessors.Enabled:
                        {
                            if (data == null)
                                return Enabled;
                            else
                                Enabled = (bool)data;

                            break;
                        }
                    case ControlCatAccessors.AddMember:
                        {
                            Add(data as TMember);
                            break;
                        }
                }

                return null;
            }

            protected class CategoryElement : HudElementBase
            {
                /// <summary>
                /// Category name
                /// </summary>
                public string HeaderText { get { return header.TextBoard.ToString(); } set { header.TextBoard.SetText(value); } }

                /// <summary>
                /// Category information
                /// </summary>
                public string SubheaderText { get { return subheader.TextBoard.ToString(); } set { subheader.TextBoard.SetText(value); } }

                /// <summary>
                /// Read only collection of <see cref="TMember"/>s assigned to this category
                /// </summary>
                public IReadOnlyList<TMember> Members => scrollBox.Collection;

                public readonly ScrollBox<TMember> scrollBox;
                private readonly Label header, subheader;

                public CategoryElement(bool alignVertical, HudParentBase parent = null) : base(parent)
                {
                    header = new Label()
                    {
                        AutoResize = false,
                        Height = 24f,
                        Format = GlyphFormat.White,
                    };

                    subheader = new Label()
                    {
                        AutoResize = false,
                        VertCenterText = false,
                        Height = 20f,
                        Padding = new Vector2(0f, 10f),
                        Format = GlyphFormat.White.WithSize(.8f),
                        BuilderMode = TextBuilderModes.Wrapped,
                    };

                    scrollBox = new ScrollBox<TMember>(alignVertical)
                    {
                        SizingMode = HudChainSizingModes.FitMembersOffAxis,
                        Spacing = 12f,
                        Height = 280f,
                        Color = Color.Red,
                    };

                    scrollBox.Background.Visible = false;

                    var layout = new HudChain(true, this)
                    {
                        DimAlignment = DimAlignments.UnpaddedSize,
                        SizingMode = HudChainSizingModes.FitMembersOffAxis,
                        CollectionContainer = { header, subheader, { scrollBox, 1f } }
                    };

                    HeaderText = "NewSettingsCategory";
                    SubheaderText = "Subheading";

                    if (alignVertical)
                        Width = 334f;
                    else
                        Height = 328f;
                }

                public void Add(TMember tile)
                {
                    scrollBox.Add(tile);
                }

                protected override void Layout()
                {
                    SliderBar slider = scrollBox.ScrollBar.slide;
                    slider.BarColor = TerminalFormatting.OuterSpace.SetAlphaPct(HudMain.UiBkOpacity);
                }
            }
        }
    }
}