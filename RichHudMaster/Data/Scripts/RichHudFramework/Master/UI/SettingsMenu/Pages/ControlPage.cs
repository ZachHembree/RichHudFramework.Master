using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using VRage;
using ApiMemberAccessor = System.Func<object, int, object>;
using GlyphFormatMembers = VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>;

namespace RichHudFramework
{
    namespace UI.Server
    {
        using ControlContainerMembers = MyTuple<
            ApiMemberAccessor, // GetOrSetMember,
            MyTuple<object, Func<int>>, // Member List
            object // ID
        >;

        /// <summary>
        /// Interactable collection of horizontally scrolling control categories
        /// </summary>
        public class ControlPage : ControlPage<ControlCategory, ControlTile>, IControlPage
        {
            public ControlPage() : base(true)
            { }
        }

        public abstract class ControlPage<TCategory, TMember> : TerminalPageBase, IControlPage<TCategory, TMember>
            where TMember : IScrollBoxEntry<HudElementBase>, new()
            where TCategory : class, IControlCategory<TMember>, IScrollBoxEntry<HudElementBase>, new()
        {
            /// <summary>
            /// List of control categories registered to the page.
            /// </summary>
            public IReadOnlyList<TCategory> Categories => catBox.Collection;

            /// <summary>
            /// Used to allow the addition of category elements using collection-initializer syntax in
            /// conjunction with normal initializers.
            /// </summary>
            public IControlPage<TCategory, TMember> CategoryContainer => this;

            protected readonly CategoryScrollBox catBox;

            public ControlPage(bool alignVertical) : base(new CategoryScrollBox(alignVertical))
            {
                catBox = AssocMember as CategoryScrollBox;
            }

            /// <summary>
            /// Adds the given control category to the page.
            /// </summary>
            public void Add(TCategory category)
            {
                catBox.Add(category);
            }

            IEnumerator<TCategory> IEnumerable<TCategory>.GetEnumerator() =>
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
                                Add(data as TCategory);
                                break;
                            }
                        case ControlPageAccessors.CategoryData:
                            return new MyTuple<object, Func<int>>()
                            {
                                Item1 = (Func<int, ControlContainerMembers>)(x => catBox.Collection[x].GetApiData()),
                                Item2 = () => catBox.Collection.Count
                            };
                    }

                    return null;
                }
                else
                    return base.GetOrSetMember(data, memberEnum);
            }

            protected class CategoryScrollBox : ScrollBox<TCategory>
            {
                public CategoryScrollBox(bool alignVertical = true, HudParentBase parent = null) : base(alignVertical, parent)
                {
                    Spacing = 30f;
                    SizingMode = HudChainSizingModes.FitMembersOffAxis;
                    Background.Visible = false;
                }

                protected override void Layout()
                {
                    base.Layout();

                    SliderBar slider = ScrollBar.SlideInput;
                    slider.BarColor = TerminalFormatting.OuterSpace.SetAlphaPct(HudMain.UiBkOpacity);
                }
            }
        }
    }
}