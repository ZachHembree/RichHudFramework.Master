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

        /// <summary>
        /// Vertically scrolling collection of control categories.
        /// </summary>
        public class ControlPage : TerminalPageBase, IControlPage
        {
            /// <summary>
            /// List of control categories registered to the page.
            /// </summary>
            public IReadOnlyList<IControlCategory> Categories => catBox.ChainEntries;

            public IControlPage CategoryContainer => this;

            private readonly CategoryScrollBox catBox;

            public ControlPage()
            {
                catBox = new CategoryScrollBox();
                Element = catBox;
            }

            /// <summary>
            /// Adds the given control category to the page.
            /// </summary>
            public void Add(ControlCategory category)
            {
                catBox.Add(category);
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
                            return new MyTuple<object, Func<int>>()
                            {
                                Item1 = (Func<int, ControlContainerMembers>)(x => catBox.ChainEntries[x].GetApiData()),
                                Item2 = () => catBox.ChainEntries.Count
                            };
                    }

                    return null;
                }
                else
                    return base.GetOrSetMember(data, memberEnum);
            }

            private class CategoryScrollBox : ScrollBox<ControlCategory>
            { 
                public CategoryScrollBox(HudParentBase parent = null) : base(true, parent)
                {
                    Spacing = 30f;
                    SizingMode = HudChainSizingModes.ClampChainBoth | HudChainSizingModes.FitMembersOffAxis;
                    background.Visible = false;
                }

                protected override void Layout()
                {
                    base.Layout();

                    SliderBar slider = scrollBar.slide;
                    slider.BarColor = TerminalFormatting.ScrollBarColor.SetAlphaPct(HudMain.UiBkOpacity);
                }
            }
        }
    }
}