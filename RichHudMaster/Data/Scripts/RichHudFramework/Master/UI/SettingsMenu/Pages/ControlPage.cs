using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using VRage;
using VRageMath;
using RichHudFramework.UI.Rendering;
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

        public class ControlPage : TerminalPageBase, IControlPage
        {
            public IReadOnlyCollection<IControlCategory> Categories { get; }
            public IControlPage CategoryContainer => this;

            private readonly ScrollBox<ControlCategory> catBox;

            public ControlPage(IHudParent parent = null) : base(parent)
            {
                catBox = new ScrollBox<ControlCategory>(this)
                {
                    Spacing = 30f,
                    SizingMode = ScrollBoxSizingModes.None,
                    AlignVertical = true,
                    DimAlignment = DimAlignments.Both | DimAlignments.IgnorePadding
                };

                catBox.background.Visible = false;
                Categories = new ReadOnlyCollectionData<IControlCategory>(x => catBox.Members.List[x], () => catBox.Members.List.Count);              
            }

            protected override void Draw()
            {
                for (int n = 0; n < catBox.List.Count; n++)
                    catBox.List[n].Width = Width - catBox.scrollBar.Width;

                SliderBar slider = catBox.scrollBar.slide;
                slider.BarColor = RichHudTerminal.ScrollBarColor.SetAlphaPct(HudMain.UiBkOpacity);
            }

            public void Add(ControlCategory category)
            {
                catBox.AddToList(category);
            }

            IEnumerator<IControlCategory> IEnumerable<IControlCategory>.GetEnumerator() =>
                Categories.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() =>
                Categories.GetEnumerator();

            protected override object GetOrSetMember(object data, int memberEnum)
            {
                if (memberEnum > 9)
                {
                    switch ((ControlPageAccessors)memberEnum)
                    {
                        case ControlPageAccessors.AddCategory:
                            {
                                Add(data as ControlCategory);
                                break;
                            }
                        case ControlPageAccessors.CategoryData:
                            {
                                return new MyTuple<object, Func<int>>()
                                {
                                    Item1 = (Func<int, ControlContainerMembers>)(x => Categories[x].GetApiData()),
                                    Item2 = () => Categories.Count
                                };
                            }
                    }

                    return null;
                }
                else
                    return base.GetOrSetMember(data, memberEnum);
            }
        }
    }
}