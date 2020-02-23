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

        public class ControlCategory : HudElementBase, IListBoxEntry, IControlCategory
        {
            public override float Width
            {
                get { return layout.Width; }
                set { layout.Width = value; }
            }

            public override float Height
            {
                get { return layout.Height; }
                set
                {
                    layout.Height = value;
                    scrollBox.Height = value - Padding.Y - header.Height - subheader.Height;
                }
            }

            public override Vector2 Padding { get { return layout.Padding; } set { layout.Padding = value; } }

            public RichText HeaderText { get { return header.TextBoard.GetText(); } set { header.TextBoard.SetText(value); } }
            public RichText SubheaderText { get { return subheader.TextBoard.GetText(); } set { subheader.TextBoard.SetText(value); } }
            public IReadOnlyCollection<IControlTile> Tiles { get; }
            public IControlCategory TileContainer => this;
            public bool Enabled { get; set; }

            private readonly ScrollBox<ControlTile> scrollBox;
            private readonly Label header, subheader;
            private readonly HudChain<HudElementBase> layout;

            public ControlCategory(IHudParent parent = null) : base(parent)
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

                scrollBox = new ScrollBox<ControlTile>()
                {
                    Spacing = 12f,
                    SizingMode = ScrollBoxSizingModes.None,
                    AlignVertical = false,
                    MinimumVisCount = 1,
                    Color = Color.Red,
                };

                scrollBox.background.Visible = false;

                layout = new HudChain<HudElementBase>(this)
                {
                    AlignVertical = true,
                    AutoResize = true,
                    ChildContainer = { header, subheader, scrollBox }
                };

                Tiles = new ReadOnlyCollectionData<IControlTile>(x => scrollBox.Members.List[x], () => scrollBox.Members.List.Count);

                HeaderText = "NewSettingsCategory";
                SubheaderText = "Subheading\nLine 1\nLine 2\nLine 3\nLine 4";

                scrollBox.Members.AutoResize = false;
                Enabled = true;
            }

            protected override void Draw()
            {
                SliderBar slider = scrollBox.scrollBar.slide;
                slider.BarColor = RichHudTerminal.ScrollBarColor.SetAlpha((byte)(HudMain.UiBkOpacity * 255f));

                base.Draw();
            }

            public void Add(ControlTile tile)
            {
                scrollBox.AddToList(tile);
            }

            IEnumerator<IControlTile> IEnumerable<IControlTile>.GetEnumerator() =>
                Tiles.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() =>
                Tiles.GetEnumerator();

            public new ControlContainerMembers GetApiData()
            {
                return new ControlContainerMembers()
                {
                    Item1 = GetOrSetMember,
                    Item2 = new MyTuple<object, Func<int>>()
                    {
                        Item1 = (Func<int, ControlContainerMembers>)(x => scrollBox.Members.List[x].GetApiData()),
                        Item2 = () => scrollBox.Members.List.Count
                    },
                    Item3 = this
                };
            }

            private new object GetOrSetMember(object data, int memberEnum)
            {
                var member = (ControlCatAccessors)memberEnum;

                switch (member)
                {
                    case ControlCatAccessors.HeaderText:
                        {
                            if (data == null)
                                return HeaderText.ApiData;
                            else
                                HeaderText = new RichText((IList<RichStringMembers>)data);

                            break;
                        }
                    case ControlCatAccessors.SubheaderText:
                        {
                            if (data == null)
                                return SubheaderText.ApiData;
                            else
                                SubheaderText = new RichText((IList<RichStringMembers>)data);

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
        }
    }
}