using RichHudFramework.Game;
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

        public sealed partial class RichHudTerminal : ModBase.ComponentBase
        {
            private class ModControlRoot : HudElementBase, IModControlRoot, IListBoxEntry
            {
                public event Action OnSelectionChanged;
                internal event Action<ModControlRoot> OnModUpdate;

                public override float Width { get { return pageControl.Width; } set { pageControl.Width = value; } }
                public override float Height { get { return pageControl.Height; } set { pageControl.Height = value; } }
                public override Vector2 Padding { get { return pageControl.Padding; } set { pageControl.Padding = value; } }

                public RichText Name { get { return pageControl.Name; } set { pageControl.Name = value; } }
                public IReadOnlyCollection<ITerminalPage> Pages { get; }
                public IModControlRoot PageContainer => this;
                public ITerminalPage Selection => SelectedElement;
                public TerminalPageBase SelectedElement => pageControl.Selection?.AssocMember;
                public bool Enabled { get; set; }

                private readonly TreeBox<TerminalPageBase> pageControl;
                private readonly SettingsMenu menu;

                public ModControlRoot(SettingsMenu menu) : base(null)
                {
                    this.menu = menu;

                    pageControl = new TreeBox<TerminalPageBase>(this) 
                    { 
                        HeaderColor = TileColor,
                    };

                    Pages = new ReadOnlyCollectionData<ITerminalPage>(x => pageControl.Members[x].AssocMember, () => pageControl.Members.Count);
                    pageControl.MouseInput.OnLeftClick += UpdateSelection;
                    pageControl.OnSelectionChanged += () => UpdateSelection();

                    Enabled = true;
                    Visible = true;
                }

                protected override void Draw()
                {
                    pageControl.HeaderColor = pageControl.HeaderColor.SetAlpha((byte)(HudMain.UiBkOpacity * 255f));

                    base.Draw();
                }

                private void UpdateSelection()
                {
                    OnSelectionChanged?.Invoke();
                    OnModUpdate?.Invoke(this);
                }

                public void ClearSelection()
                {
                    pageControl.ClearSelection();
                }

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

                public new ControlContainerMembers GetApiData()
                {
                    return new ControlContainerMembers()
                    {
                        Item1 = GetOrSetMember,
                        Item2 = new MyTuple<object, Func<int>>()
                        {
                            Item1 = (Func<int, ControlMembers>)(x => pageControl.Members[x].AssocMember.GetApiData()),
                            Item2 = () => pageControl.Members.Count
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
                                    return Name.ApiData;
                                else
                                    Name = new RichText(data as IList<RichStringMembers>);

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