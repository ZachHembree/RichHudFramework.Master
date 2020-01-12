using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using VRage;
using VRageMath;
using RichHudFramework.UI.Rendering;
using ApiMemberAccessor = System.Func<object, int, object>;
using GlyphFormatMembers = VRage.MyTuple<VRageMath.Vector2I, int, VRageMath.Color, float>;

namespace RichHudFramework
{
    using RichStringMembers = MyTuple<StringBuilder, GlyphFormatMembers>;

    namespace UI.Server
    {
        public class RebindPage : TerminalPageBase, IRebindPage
        {
            public IReadOnlyCollection<IBindGroup> BindGroups { get; }

            public IRebindPage BindContainer => this;

            private readonly ScrollBox<BindGroupBox> bindGroups;

            public RebindPage(IHudParent parent = null) : base(parent)
            {
                bindGroups = new ScrollBox<BindGroupBox>(this)
                {
                    Spacing = 30f,
                    Padding = new Vector2(32f, 0f),
                    FitToChain = false,
                    AlignVertical = true,
                    DimAlignment = DimAlignments.Both | DimAlignments.IgnorePadding
                };

                bindGroups.background.Visible = false;
                BindGroups = new ReadOnlyCollectionData<IBindGroup>(x => bindGroups.List[x].BindGroup, () => bindGroups.List.Count);
            }

            protected override void Draw()
            {
                for (int n = 0; n < bindGroups.List.Count; n++)
                    bindGroups.List[n].Width = Width - bindGroups.scrollBar.Width - bindGroups.Padding.X;
            }

            public void Add(IBindGroup bindGroup)
            {
                var bindBox = new BindGroupBox() { BindGroup = bindGroup };
                bindGroups.AddToList(bindBox);
            }

            public IEnumerator<IBindGroup> GetEnumerator() =>
                BindGroups.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() =>
                BindGroups.GetEnumerator();

            protected override object GetOrSetMember(object data, int memberEnum)
            {              
                if (memberEnum > 9)
                {
                    switch ((RebindPageAccessors)memberEnum)
                    {
                        case RebindPageAccessors.Add:
                            Add(data as IBindGroup);
                            break;
                    }

                    return null;
                }
                else
                    return base.GetOrSetMember(data, memberEnum);
            }

            private class BindGroupBox : HudElementBase, IListBoxEntry
            {
                public override float Height { get { return layout.Height; } set { scrollBox.Height = value - (layout.Height - scrollBox.Height); } }

                public override bool Visible
                {
                    set
                    {
                        if (value == true && base.Visible != value)
                            UpdateBindGroup();

                        base.Visible = value;
                    }
                }

                public bool Enabled { get; set; }

                public IBindGroup BindGroup
                {
                    get { return bindGroup; }
                    set
                    {
                        bindGroup = value;
                        UpdateBindGroup();
                    }
                }

                private readonly Label name;
                private readonly TerminalButton resetButton;
                private readonly ScrollBox<BindBox> scrollBox;
                private readonly HudChain<HudElementBase> layout;

                private IBindGroup bindGroup;

                public BindGroupBox(IHudParent parent = null) : base(parent)
                {
                    name = new Label()
                    {
                        Format = GlyphFormat.White,
                        AutoResize = false,
                        Height = 24f,
                        Padding = new Vector2(0f, 24f),
                    };

                    resetButton = new TerminalButton(this)
                    {
                        Name = "Defaults",
                        Size = new Vector2(234f, 44f),
                        ParentAlignment = ParentAlignments.Top | ParentAlignments.Right | ParentAlignments.Inner,
                    };

                    resetButton.border.Thickness = 1f;

                    scrollBox = new ScrollBox<BindBox>()
                    {
                        AlignVertical = true,
                        FitToChain = false,
                        Spacing = 8f,
                    };

                    scrollBox.background.Visible = false;

                    var divider1 = new TexturedBox()
                    {
                        Color = new Color(53, 66, 75),
                        Padding = new Vector2(0f, 16f),
                        Height = 2f,
                    };

                    var divider2 = new TexturedBox()
                    {
                        Color = new Color(53, 66, 75),
                        Padding = new Vector2(0f, 16f),
                        Height = 2f,
                    };

                    layout = new HudChain<HudElementBase>(this)
                    {
                        AlignVertical = true,
                        AutoResize = true,
                        DimAlignment = DimAlignments.Width | DimAlignments.IgnorePadding,
                        ChildContainer = { name, divider1, scrollBox, divider2 },
                    };

                    Height = 255f;
                    Enabled = true;
                }

                protected override void Draw()
                {
                    for (int n = 0; n < scrollBox.List.Count; n++)
                        scrollBox.List[n].Width = Width - scrollBox.scrollBar.Width;
                }

                private void UpdateBindGroup()
                {
                    if (BindGroup != null)
                    {
                        name.Text = $"Group: {BindGroup.Name}";

                        for (int n = 0; n < BindGroup.Count; n++)
                        {
                            if (scrollBox.List.Count == n)
                                scrollBox.AddToList(new BindBox());

                            scrollBox.List[n].Enabled = true;
                            scrollBox.List[n].Bind = BindGroup[n];
                        }
                    }
                }
            }

            private class BindBox : HudElementBase, IListBoxEntry
            {
                public override float Height { get { return layout.Height; } set { layout.Height = value; bindName.Height = value; } }

                public bool Enabled { get; set; }

                public IBind Bind
                {
                    get { return bind; }
                    set
                    {
                        bind = value;
                        UpdateBindText();
                    }
                }

                private readonly Label bindName;
                private readonly TerminalButton con1, con2, con3;
                private readonly HudChain<HudElementBase> layout;

                private IBind bind;

                public BindBox(IHudParent parent = null) : base(parent)
                {
                    bindName = new Label(this)
                    {
                        Text = "NewBindBox",
                        Format = GlyphFormat.Blueish,
                        AutoResize = false,
                        Size = new Vector2(150f, 44f),
                        ParentAlignment = ParentAlignments.Left | ParentAlignments.InnerH,
                    };

                    con1 = new TerminalButton()
                    {
                        Name = "none",
                        Padding = new Vector2(),
                        Size = new Vector2(126f, 44f),
                    };

                    con1.border.Thickness = 1f;
                    con1.MouseInput.OnLeftClick += () => GetNewControl(0);
                    con1.MouseInput.OnRightClick += () => RemoveControl(0);

                    con2 = new TerminalButton()
                    {
                        Name = "none",
                        Padding = new Vector2(),
                        Size = new Vector2(126f, 44f),
                    };

                    con2.border.Thickness = 1f;
                    con2.MouseInput.OnLeftClick += () => GetNewControl(1);
                    con2.MouseInput.OnRightClick += () => RemoveControl(1);

                    con3 = new TerminalButton()
                    {
                        Name = "none",
                        Padding = new Vector2(),
                        Size = new Vector2(126f, 44f),
                    };

                    con3.border.Thickness = 1f;
                    con3.MouseInput.OnLeftClick += () => GetNewControl(2);
                    con3.MouseInput.OnRightClick += () => RemoveControl(2);

                    layout = new HudChain<HudElementBase>(this)
                    {
                        AlignVertical = false,
                        AutoResize = true,
                        Spacing = 13f,
                        Padding = new Vector2(32f, 0f),
                        ParentAlignment = ParentAlignments.Right | ParentAlignments.InnerH,
                        ChildContainer = { con1, con2, con3 }
                    };

                    Size = new Vector2(400f, 44f);
                    Enabled = true;
                }

                private void GetNewControl(int index)
                {
                    RichHudTerminal.Open = false;
                    RebindDialog.UpdateBind(bind, index, UpdateBindText);
                }

                private void RemoveControl(int index)
                {
                    List<IControl> combo = new List<IControl>(bind.GetCombo());

                    if (index < combo.Count)
                    {
                        if (index == 0 && combo.Count == 1)
                        {
                            bind.ClearCombo();
                            UpdateBindText();
                        }
                        else
                        {
                            combo.RemoveAt(index);

                            if (bind.TrySetCombo(combo.ToArray(), false))
                                UpdateBindText();
                        }
                    }
                }

                private void UpdateBindText()
                {
                    bindName.Text = bind.Name;
                    IList<IControl> combo = bind.GetCombo();

                    if (combo != null && combo.Count > 0)
                    {
                        con1.Name = combo[0].DisplayName;

                        if (combo.Count > 1)
                            con2.Name = combo[1].DisplayName;
                        else
                            con2.Name = "none";

                        if (combo.Count > 2)
                            con3.Name = combo[2].DisplayName;
                        else
                            con3.Name = "none";
                    }
                    else
                    {
                        con1.Name = "none";
                        con2.Name = "none";
                        con3.Name = "none";
                    }

                    RichHudTerminal.Open = true;
                }
            }
        }
    }
}