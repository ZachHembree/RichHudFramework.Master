﻿using RichHudFramework.Internal;
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
            /// <summary>
            /// Indented dropdown list of terminal pages. Root UI element for all terminal controls
            /// associated with a given mod.
            /// </summary>
            private class ModControlRoot : HudElementBase, IModControlRoot, IListBoxEntry
            {
                /// <summary>
                /// Invoked when a new page is selected
                /// </summary>
                public event Action OnSelectionChanged
                {
                    add { pageControl.OnSelectionChanged += value; }
                    remove { pageControl.OnSelectionChanged -= value; }
                }

                public event Action<ModControlRoot> OnModUpdate;

                /// <summary>
                /// Name of the mod as it appears in the <see cref="RichHudTerminal"/> mod list
                /// </summary>
                public string Name { get { return pageControl.Name.ToString(); } set { pageControl.Name = value; } }

                /// <summary>
                /// Read only collection of <see cref="ITerminalPage"/>s assigned to this object.
                /// </summary>
                public IReadOnlyCollection<ITerminalPage> Pages { get; }

                public IModControlRoot PageContainer => this;

                /// <summary>
                /// Currently selected <see cref="ITerminalPage"/>.
                /// </summary>
                public ITerminalPage Selection => SelectedElement;

                public TerminalPageBase SelectedElement => pageControl.Selection?.AssocMember;

                public override float Width { get { return pageControl.Width; } set { pageControl.Width = value; } }
                public override float Height { get { return pageControl.Height; } set { pageControl.Height = value; } }
                public override Vector2 Padding { get { return pageControl.Padding; } set { pageControl.Padding = value; } }

                public override bool Visible { get { return base.Visible && Enabled; } }

                /// <summary>
                /// Determines whether or not the element will appear in the list.
                /// Disabled by default.
                /// </summary>
                public bool Enabled { get { return _enabled && pageControl.List.Count > 0; } set { _enabled = value; } }

                private readonly TreeBox<TerminalPageBase> pageControl;
                private readonly TerminalWindow menu;
                private bool _enabled;

                public ModControlRoot(TerminalWindow menu) : base(null)
                {
                    this.menu = menu;

                    pageControl = new TreeBox<TerminalPageBase>(this) 
                    { 
                        HeaderColor = TileColor,
                    };

                    Pages = new ReadOnlyCollectionData<ITerminalPage>(x => pageControl.List[x].AssocMember, () => pageControl.List.Count);
                    pageControl.OnSelectionChanged += () => UpdateSelection();

                    Enabled = false;
                    Visible = true;
                }

                protected override void Layout()
                {
                    pageControl.HeaderColor = pageControl.HeaderColor.SetAlphaPct(HudMain.UiBkOpacity);

                    base.Layout();
                }

                private void UpdateSelection()
                {
                    OnModUpdate?.Invoke(this);
                }

                public void ClearSelection()
                {
                    pageControl.ClearSelection();
                }

                /// <summary>
                /// Adds the given <see cref="TerminalPageBase"/> to the object.
                /// </summary>
                public void Add(TerminalPageBase page)
                {
                    ListBoxEntry<TerminalPageBase> listMember = pageControl.Add(page.Name, page);
                    page.NameBuilder = listMember.TextBoard;
                    menu.AddPage(page);
                }

                IEnumerator<ITerminalPage> IEnumerable<ITerminalPage>.GetEnumerator() =>
                    Pages.GetEnumerator();

                IEnumerator IEnumerable.GetEnumerator() =>
                    Pages.GetEnumerator();

                /// <summary>
                /// Retrieves data used by the Framework API
                /// </summary>
                public new ControlContainerMembers GetApiData()
                {
                    return new ControlContainerMembers()
                    {
                        Item1 = GetOrSetMember,
                        Item2 = new MyTuple<object, Func<int>>()
                        {
                            Item1 = (Func<int, ControlMembers>)(x => pageControl.List[x].AssocMember.GetApiData()),
                            Item2 = () => pageControl.List.Count
                        },
                        Item3 = this
                    };
                }

                private new object GetOrSetMember(object data, int memberEnum)
                {
                    var member = (ModControlRootAccessors)memberEnum;

                    switch (member)
                    {
                        case ModControlRootAccessors.OnSelectionChanged:
                            {
                                var eventData = (MyTuple<bool, Action>)data;

                                if (eventData.Item1)
                                    OnSelectionChanged += eventData.Item2;
                                else
                                    OnSelectionChanged -= eventData.Item2;

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
                                return SelectedElement.GetApiData();
                            }
                        case ModControlRootAccessors.AddPage:
                            {
                                Add(data as TerminalPageBase);
                                break;
                            }
                    }

                    return null;
                }
            }
        }
    }
}