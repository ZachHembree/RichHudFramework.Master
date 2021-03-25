using RichHudFramework.UI.Rendering;
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

        public class TerminalPageCategory : TerminalPageCategoryBase
        { }

        public abstract class TerminalPageCategoryBase : SelectionBoxEntryTuple<LabelElementBase, object>, ITerminalPageCategory
        {
            /// <summary>
            /// Name of the category as it appears in the terminal
            /// </summary>
            public string Name 
            { 
                get { return Element.TextBoard.ToString(); } 
                set { Element.TextBoard.SetText(value); } 
            }

            /// <summary>
            /// Determines whether or not the element will appear in the list.
            /// Disabled by default.
            /// </summary>
            public override bool Enabled
            {
                get { return base.Enabled && treeBox.EntryList.Count > 0; }
                set { base.Enabled = value; }
            }

            /// <summary>
            /// Read only collection of <see cref="TerminalPageBase"/>s assigned to this object.
            /// </summary>
            public IReadOnlyList<TerminalPageBase> Pages => pages;

            /// <summary>
            /// Currently selected <see cref="TerminalPageBase"/>.
            /// </summary>
            public virtual TerminalPageBase SelectedPage => treeBox.Selection?.AssocMember as TerminalPageBase;

            /// <summary>
            /// Used to allow the addition of category elements using collection-initializer syntax in
            /// conjunction with normal initializers.
            /// </summary>
            public ITerminalPageCategory PageContainer => this;

            public object ID => this;

            protected readonly List<TerminalPageBase> pages;
            protected readonly PageCategoryNodeBox treeBox;

            public TerminalPageCategoryBase()
            {
                AllowHighlighting = false;

                treeBox = new PageCategoryNodeBox();
                pages = new List<TerminalPageBase>();
                SetElement(treeBox);
            }

            public IEnumerator<TerminalPageBase> GetEnumerator() =>
                Pages.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() =>
                Pages.GetEnumerator();

            /// <summary>
            /// Adds a terminal page to the category
            /// </summary>
            public void Add(TerminalPageBase page)
            {
                var entry = new SelectionBoxEntryTuple<LabelElementBase, object>();
                entry.SetElement(new Label());
                entry.AssocMember = page;
                entry.Enabled = true;
                entry.Element.Padding = new Vector2(20f, 6f);

                ITextBoard textBoard = entry.Element.TextBoard;
                textBoard.SetText(page.Name);
                textBoard.AutoResize = false;
                page.NameBuilder = textBoard;

                treeBox.Add(entry);
                pages.Add(page);
            }


            /// <summary>
            /// Sets the selection to the given page
            /// </summary>
            public void SetSelection(object member)
            {
                int index = treeBox.FindIndex(x => x.AssocMember == member);

                if (index != -1)
                {
                    treeBox.SetSelectionAt(index);
                    treeBox.OpenList();
                }
            }

            /// <summary>
            /// Clears the current selection
            /// </summary>
            public void ClearSelection() =>
                treeBox.ClearSelection();

            /// <summary>
            /// Adds a range of terminal pages to the category
            /// </summary>
            public void AddRange(IReadOnlyList<TerminalPageBase> pages)
            {
                foreach (TerminalPageBase page in pages)
                    Add(page);
            }

            protected virtual void AddRangeInternal(IReadOnlyList<object> members)
            {
                foreach (TerminalPageBase page in members)
                    Add(page);
            }

            protected virtual object GetOrSetMember(object data, int memberEnum)
            {
                switch ((TerminalPageCategoryAccessors)memberEnum)
                {
                    case TerminalPageCategoryAccessors.Name:
                        {
                            if (data == null)
                                return Name;
                            else
                                Name = data as string;

                            break;
                        }
                    case TerminalPageCategoryAccessors.Enabled:
                        {
                            if (data == null)
                                return Enabled;
                            else
                                Enabled = (bool)data;

                            break;
                        }
                    case TerminalPageCategoryAccessors.Selection:
                        {
                            return treeBox.Selection?.AssocMember;
                        }
                    case TerminalPageCategoryAccessors.AddPage:
                        Add(data as TerminalPageBase); break;
                    case TerminalPageCategoryAccessors.AddPageRange:
                        AddRangeInternal(data as object[]); break;
                }

                return null;
            }

            public ControlContainerMembers GetApiData()
            {
                return new ControlContainerMembers()
                {
                    Item1 = GetOrSetMember,
                    Item2 = new MyTuple<object, Func<int>>()
                    {
                        Item1 = new Func<int, ControlMembers>(x => pages[x].GetApiData()),
                        Item2 = () => pages.Count
                    },
                    Item3 = this
                };
            }
        }

        public class PageCategoryNodeBox : TreeBox<SelectionBoxEntryTuple<LabelElementBase, object>, LabelElementBase>
        {
            public PageCategoryNodeBox(HudParentBase parent) : base(parent)
            {
                HeaderColor = new Color(40, 48, 55);
                MemberMinSize = new Vector2(0f, 34f);
            }

            public PageCategoryNodeBox() : this(null)
            { }
        }
    }
}