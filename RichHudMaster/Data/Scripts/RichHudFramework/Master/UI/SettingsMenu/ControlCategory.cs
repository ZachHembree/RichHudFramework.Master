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
        using ControlContainerMembers = MyTuple<
            ApiMemberAccessor, // GetOrSetMember,
            MyTuple<object, Func<int>>, // Member List
            object // ID
        >;

        /// <summary>
        /// Horizontally scrolling list of control tiles.
        /// </summary>
        public class ControlCategory : ScrollBoxEntry, IControlCategory
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
            /// Read only collection of <see cref="IControlTile"/>s assigned to this category
            /// </summary>
            public IReadOnlyList<IControlTile> Tiles => categoryElement.Tiles;

            /// <summary>
            /// Used to allow the addition of control tiles to categories using collection-initializer syntax in
            /// conjunction with normal initializers.
            /// </summary>
            public IControlCategory TileContainer => this;

            /// <summary>
            /// Unique identifier.
            /// </summary>
            public object ID => this;

            private readonly CategoryElement categoryElement;

            public ControlCategory()
            {
                categoryElement = new CategoryElement();
                Element = categoryElement;
            }

            /// <summary>
            /// Adds a <see cref="IControlTile"/> to the category
            /// </summary>
            public void Add(ControlTile tile)
            {
                categoryElement.Add(tile);
            }

            IEnumerator<IControlTile> IEnumerable<IControlTile>.GetEnumerator() =>
                Tiles.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() =>
                Tiles.GetEnumerator();

            /// <summary>
            /// Retrieves information used by the Framework API
            /// </summary>
            public ControlContainerMembers GetApiData()
            {
                return new ControlContainerMembers()
                {
                    Item1 = GetOrSetMember,
                    Item2 = new MyTuple<object, Func<int>>()
                    {
                        Item1 = (Func<int, ControlContainerMembers>)(x => categoryElement.Tiles[x].GetApiData()),
                        Item2 = () => categoryElement.Tiles.Count
                    },
                    Item3 = this
                };
            }

            private object GetOrSetMember(object data, int memberEnum)
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
                    case ControlCatAccessors.AddTile:
                        {
                            Add(data as ControlTile);
                            break;
                        }
                }

                return null;
            }

            private class CategoryElement : HudElementBase
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
                /// Read only collection of <see cref="IControlTile"/>s assigned to this category
                /// </summary>
                public IReadOnlyList<IControlTile> Tiles => scrollBox.Collection;

                public override float Width
                {
                    get { return layout.Width + Padding.X; }
                    set 
                    {
                        if (value > Padding.X)
                            value -= Padding.X;

                        layout.Width = value; 
                    }
                }

                public override float Height
                {
                    get { return layout.Height + Padding.Y; }
                    set 
                    {
                        if (value > Padding.Y)
                            value -= Padding.Y;

                        layout.Height = value;
                        scrollBox.Height = value - header.Height - subheader.Height; 
                    }
                }

                private readonly ScrollBox<ControlTile> scrollBox;
                private readonly Label header, subheader;
                private readonly HudChain layout;

                public CategoryElement(HudParentBase parent = null) : base(parent)
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

                    scrollBox = new ScrollBox<ControlTile>(false)
                    {
                        SizingMode = HudChainSizingModes.FitChainOffAxis | HudChainSizingModes.ClampChainAlignAxis,
                        MinVisibleCount = 1,
                        Spacing = 12f,
                        Height = 280f,
                        Color = Color.Red,
                    };

                    scrollBox.background.Visible = false;

                    layout = new HudChain(true, this)
                    {
                        SizingMode = HudChainSizingModes.FitMembersOffAxis | HudChainSizingModes.FitChainBoth,
                        CollectionContainer = { header, subheader, scrollBox }
                    };

                    HeaderText = "NewSettingsCategory";
                    SubheaderText = "Subheading\nLine 1\nLine 2\nLine 3\nLine 4";
                    Height = 334f;
                }

                public void Add(ControlTile tile)
                {
                    scrollBox.Add(tile);
                }

                protected override void Layout()
                {
                    SliderBar slider = scrollBox.scrollBar.slide;
                    slider.BarColor = TerminalFormatting.ScrollBarColor.SetAlphaPct(HudMain.UiBkOpacity);
                }
            }
        }
    }
}