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
                public event EventHandler OnSelectionChanged;

                /// <summary>
                /// Determines whether or not the element will appear in the list.
                /// Disabled by default.
                /// </summary>
                public override bool Enabled 
                { 
                    get { return _enabled && Element.ListEntries.Count > 0; } 
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
                public IReadOnlyCollection<ITerminalPage> Pages { get; }

                /// <summary>
                /// Used to allow the addition of child elements using collection-initializer syntax in
                /// conjunction with normal initializers.
                /// </summary>
                public IModControlRoot PageContainer => this;

                private Action ApiCallbackAction;
                private bool _enabled;

                public ModControlRoot()
                {
                    Element = new ModControlRootTreeBox();
                    Pages = new ReadOnlyCollectionData<ITerminalPage>
                    (
                        x => Element.ListEntries[x].AssocMember,
                        () => Element.ListEntries.Count
                    );

                    OnSelectionChanged += InvokeCallback;
                }

                public IEnumerator<ITerminalPage> GetEnumerator() =>
                    Pages.GetEnumerator();

                IEnumerator IEnumerable.GetEnumerator() =>
                    Pages.GetEnumerator();

                /// <summary>
                /// Adds the given <see cref="TerminalPageBase"/> to the object.
                /// </summary>
                public void Add(TerminalPageBase page)
                {
                    ListBoxEntry<TerminalPageBase> listMember = Element.Add(page.Name, page);
                    page.NameBuilder = listMember.Element.TextBoard;
                }

                private void InvokeCallback(object sender, EventArgs args)
                {
                    OnSelectionChanged?.Invoke(this, EventArgs.Empty);
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
                                return Element.Selection?.AssocMember.GetApiData();
                            }
                        case ModControlRootAccessors.AddPage:
                            {
                                Add(data as TerminalPageBase);
                                break;
                            }
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
                            Item1 = (Func<int, ControlMembers>)(x => Element.ListEntries[x].AssocMember.GetApiData()),
                            Item2 = () => Element.ListEntries.Count
                        },
                        Item3 = this
                    };
                }
            }

            /// <summary>
            /// TreeBox modified for use as the ModControlRoot's UI element.
            /// </summary>
            private class ModControlRootTreeBox : TreeBox<TerminalPageBase>
            {
                public ModControlRootTreeBox(HudParentBase parent = null) : base(parent)
                {
                    HeaderColor = TileColor;
                }

                protected override void Layout()
                {
                    display.Color = display.Color.SetAlphaPct(HudMain.UiBkOpacity);
                    base.Layout();
                }
            }
        }
    }
}