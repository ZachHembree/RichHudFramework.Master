using RichHudFramework.Internal;
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

        public sealed partial class RichHudTerminal : RichHudComponentBase
        {
            private class ModControlRoot : ScrollBoxEntry<ModControlRootTreeBox>, IModControlRoot
            {
                /// <summary>
                /// Invoked when a new page is selected
                /// </summary>
                public event EventHandler SelectionChanged;

                /// <summary>
                /// Determines whether or not the element will appear in the list.
                /// Disabled by default.
                /// </summary>
                public override bool Enabled
                {
                    get { return _enabled && Element.EntryList.Count > 0; }
                    set { _enabled = value; }
                }

                /// <summary>
                /// Name of the mod as it appears in the <see cref="RichHudTerminal"/> mod list
                /// </summary>
                public string Name { get { return Element.Name.ToString(); } set { Element.Name = value; } }

                /// <summary>
                /// Currently selected <see cref="ITerminalPage"/>.
                /// </summary>
                public ITerminalPage Selection => Element.Selection?.AssocMember;

                /// <summary>
                /// Read only collection of <see cref="ITerminalPage"/>s assigned to this object.
                /// </summary>
                public IReadOnlyList<ITerminalPage> Pages { get; }

                public IReadOnlyList<ListBoxEntry<TerminalPageBase>> ListEntries => treeBox.EntryList;

                /// <summary>
                /// Used to allow the addition of child elements using collection-initializer syntax in
                /// conjunction with normal initializers.
                /// </summary>
                public IModControlRoot PageContainer => this;

                private Action ApiCallbackAction;
                private readonly ModControlRootTreeBox treeBox;
                private bool _enabled;

                public ModControlRoot()
                {
                    treeBox = new ModControlRootTreeBox();
                    SetElement(treeBox);
                    Pages = new ReadOnlyCollectionData<ITerminalPage>
                    (
                        x => Element.EntryList[x].AssocMember,
                        () => Element.EntryList.Count
                    );

                    treeBox.SelectionChanged += InvokeCallback;
                }

                public IEnumerator<ITerminalPage> GetEnumerator() =>
                    Pages.GetEnumerator();

                IEnumerator IEnumerable.GetEnumerator() =>
                    Pages.GetEnumerator();

                /// <summary>
                /// Sets the selection to the given page
                /// </summary>
                public void SetSelection(TerminalPageBase page)
                {
                    treeBox.SetSelection(page);
                    treeBox.OpenList();
                }

                /// <summary>
                /// Adds the given <see cref="TerminalPageBase"/> to the object.
                /// </summary>
                public void Add(TerminalPageBase page)
                {
                    ListBoxEntry<TerminalPageBase> listMember = Element.Add(page.Name, page);
                    page.NameBuilder = listMember.Element.TextBoard;
                }

                /// <summary>
                /// Adds the given ranges of pages to the control root.
                /// </summary>
                public void AddRange(IReadOnlyList<TerminalPageBase> pages)
                {
                    foreach (TerminalPageBase page in pages)
                    {
                        ListBoxEntry<TerminalPageBase> listMember = Element.Add(page.Name, page);
                        page.NameBuilder = listMember.Element.TextBoard;
                    }
                }

                /// <summary>
                /// Adds the given ranges of pages to the control root.
                /// </summary>
                private void AddRangeInternal(IReadOnlyList<object> pages)
                {
                    foreach (TerminalPageBase page in pages)
                    {
                        ListBoxEntry<TerminalPageBase> listMember = Element.Add(page.Name, page);
                        page.NameBuilder = listMember.Element.TextBoard;
                    }
                }

                private void InvokeCallback(object sender, EventArgs args)
                {
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                    ApiCallbackAction?.Invoke();
                }

                private object GetOrSetMember(object data, int memberEnum)
                {
                    var member = (ModControlRootAccessors)memberEnum;

                    switch (member)
                    {
                        case ModControlRootAccessors.GetOrSetCallback:
                            {
                                if (data == null)
                                    return ApiCallbackAction;
                                else
                                    ApiCallbackAction = data as Action;

                                break;
                            }
                        case ModControlRootAccessors.Name:
                            {
                                if (data == null)
                                    return Name;
                                else
                                    Name = data as string;

                                break;
                            }
                        case ModControlRootAccessors.Enabled:
                            {
                                if (data == null)
                                    return Enabled;
                                else
                                    Enabled = (bool)data;

                                break;
                            }
                        case ModControlRootAccessors.Selection:
                            {
                                return Element.Selection?.AssocMember;
                            }
                        case ModControlRootAccessors.AddPage:
                            Add(data as TerminalPageBase); break;
                        case ModControlRootAccessors.AddRange:
                            AddRangeInternal(data as object[]); break;
                    }

                    return null;
                }

                /// <summary>
                /// Retrieves data used by the Framework API
                /// </summary>
                public ControlContainerMembers GetApiData()
                {
                    return new ControlContainerMembers()
                    {
                        Item1 = GetOrSetMember,
                        Item2 = new MyTuple<object, Func<int>>()
                        {
                            Item1 = (Func<int, ControlMembers>)(x => Element.EntryList[x].AssocMember.GetApiData()),
                            Item2 = () => Element.EntryList.Count
                        },
                        Item3 = this
                    };
                }
            }

            /// <summary>
            /// TreeBox modified for use as the ModControlRoot's UI element.
            /// </summary>
            private class ModControlRootTreeBox : TreeList<TerminalPageBase>
            {
                public ModControlRootTreeBox(HudParentBase parent = null) : base(parent)
                {
                    HeaderColor = new Color(40, 48, 55);
                }

                public override bool Unregister(bool fast = false)
                {
                    selectionBox.hudChain.Clear(fast);
                    return base.Unregister(fast);
                }
            }
        }
    }
}