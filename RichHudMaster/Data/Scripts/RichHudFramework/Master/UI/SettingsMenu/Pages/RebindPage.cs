﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using VRage;
using VRageMath;
using RichHudFramework.UI.Rendering;
using ApiMemberAccessor = System.Func<object, int, object>;
using GlyphFormatMembers = VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>;
using BindDefinitionData = VRage.MyTuple<string, string[]>;

namespace RichHudFramework
{
    using RichStringMembers = MyTuple<StringBuilder, GlyphFormatMembers>;

    namespace UI.Server
    {
        /// <summary>
        /// Scrollable list of bind group controls.
        /// </summary>
        public class RebindPage : TerminalPageBase, IRebindPage
        {
            /// <summary>
            /// List of bind groups registered to the page.
            /// </summary>
            public IReadOnlyCollection<IBindGroup> BindGroups { get; }

            public RebindPage GroupContainer => this;

            private readonly ScrollBox<BindGroupBox> bindGroups;

            public RebindPage(IHudParent parent = null) : base(parent)
            {
                bindGroups = new ScrollBox<BindGroupBox>(this)
                {
                    Spacing = 30f,
                    Padding = new Vector2(32f, 0f),
                    SizingMode = ScrollBoxSizingModes.None,
                    AlignVertical = true,
                    DimAlignment = DimAlignments.Both | DimAlignments.IgnorePadding
                };

                bindGroups.background.Visible = false;
                BindGroups = new ReadOnlyCollectionData<IBindGroup>(x => bindGroups.List[x].BindGroup, () => bindGroups.List.Count);
            }

            protected override void Layout()
            {
                for (int n = 0; n < bindGroups.List.Count; n++)
                    bindGroups.List[n].Width = Width - bindGroups.scrollBar.Width - bindGroups.Padding.X;

                SliderBar slider = bindGroups.scrollBar.slide;
                slider.BarColor = RichHudTerminal.ScrollBarColor.SetAlphaPct(HudMain.UiBkOpacity);
            }

            /// <summary>
            /// Adds the given bind group to the page.
            /// </summary>
            public void Add(IBindGroup bindGroup)
            {
                var bindBox = new BindGroupBox() { BindGroup = bindGroup };
                bindGroups.AddToList(bindBox);
            }

            /// <summary>
            /// Adds the given bind group to the page along with its associated default configuration.
            /// </summary>
            public void Add(IBindGroup bindGroup, BindDefinition[] defaultBinds)
            {
                var bindBox = new BindGroupBox() { BindGroup = bindGroup, DefaultBinds = defaultBinds };
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
                            {
                                var args = (MyTuple<object, BindDefinitionData[]>)data;
                                BindDefinition[] defaults = new BindDefinition[args.Item2.Length];

                                for (int n = 0; n < defaults.Length; n++)
                                    defaults[n] = args.Item2[n];

                                Add(args.Item1 as IBindGroup, defaults);
                                break;
                            }
                    }

                    return null;
                }
                else
                    return base.GetOrSetMember(data, memberEnum);
            }

            /// <summary>
            /// Scrollable list of keybinds. Supports, at most, three controls per bind.
            /// </summary>
            private class BindGroupBox : HudElementBase, IListBoxEntry
            {
                /// <summary>
                /// Determines whether or not the control will be visible in the list.
                /// </summary>
                public bool Enabled { get; set; }

                /// <summary>
                /// Bind group associated with the control.
                /// </summary>
                public IBindGroup BindGroup
                {
                    get { return _bindGroup; }
                    set
                    {
                        _bindGroup = value;
                        UpdateBindGroup();
                    }
                }

                /// <summary>
                /// Default configuration associated with the bind group.
                /// </summary>
                public BindDefinition[] DefaultBinds 
                {
                    get { return defaultBinds; } 
                    set 
                    { 
                        defaultBinds = value;
                        resetButton.Visible = defaultBinds != null;
                    }
                }

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

                private readonly Label name;
                private readonly TerminalButton resetButton;
                private readonly ScrollBox<BindBox> scrollBox;
                private readonly HudChain<HudElementBase> layout;

                private IBindGroup _bindGroup;
                private BindDefinition[] defaultBinds;

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
                        Visible = false,
                    };

                    resetButton.border.Thickness = 1f;
                    resetButton.MouseInput.OnLeftClick += ResetBinds;

                    scrollBox = new ScrollBox<BindBox>()
                    {
                        AlignVertical = true,
                        SizingMode = ScrollBoxSizingModes.None,
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

                protected override void Layout()
                {
                    SliderBar slider = scrollBox.scrollBar.slide;
                    slider.BarColor = RichHudTerminal.ScrollBarColor.SetAlphaPct(HudMain.UiBkOpacity);

                    for (int n = 0; n < scrollBox.List.Count; n++)
                        scrollBox.List[n].Width = Width - scrollBox.scrollBar.Width;
                }

                /// <summary>
                /// Applies the default configuration to the bind group if defaults are defined.
                /// </summary>
                private void ResetBinds()
                {
                    if (DefaultBinds != null)
                    {
                        BindGroup.TryLoadBindData(DefaultBinds);
                        UpdateBindGroup();
                    }
                }

                /// <summary>
                /// Updates bind controls.
                /// </summary>
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
                            scrollBox.List[n].SetBind(BindGroup[n], BindGroup);
                        }
                    }
                }
            }

            /// <summary>
            /// Control for individual keybinds. Supports up to three controls per bind.
            /// </summary>
            private class BindBox : HudElementBase, IListBoxEntry
            {
                /// <summary>
                /// Determines whether the bind will be visible in the list.
                /// </summary>
                public bool Enabled { get; set; }

                public override float Height { get { return layout.Height; } set { layout.Height = value; bindName.Height = value; } }

                private readonly Label bindName;
                private readonly TerminalButton con1, con2, con3;
                private readonly HudChain<HudElementBase> layout;

                private IBind bind;
                private IBindGroup group;

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

                public void SetBind(IBind bind, IBindGroup group)
                {
                    this.bind = bind;
                    this.group = group;
                    UpdateBindText();
                }

                /// <summary>
                /// Opens the rebind dialog for the given bind for the control specified.
                /// </summary>
                private void GetNewControl(int index)
                {
                    RichHudTerminal.Open = false;
                    RebindDialog.UpdateBind(bind, index, DialogClosed);
                }

                /// <summary>
                /// Rebind dialog callback.
                /// </summary>
                private void DialogClosed()
                {
                    UpdateBindText();
                    RichHudTerminal.Open = true;
                }

                /// <summary>
                /// Removes the control at the index specified.
                /// </summary>
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

                /// <summary>
                /// Updates the buttons for the bind controls to reflect the current configuration.
                /// </summary>
                private void UpdateBindText()
                {
                    bindName.Text = bind.Name;
                    IList<IControl> combo = bind.GetCombo();

                    if (combo != null && combo.Count > 0)
                    {
                        if (!group.DoesComboConflict(combo, bind))
                            bindName.TextBoard.SetFormatting(GlyphFormat.Blueish);
                        else
                            bindName.TextBoard.SetFormatting(RichHudTerminal.WarningFormat);

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
                        bindName.TextBoard.SetFormatting(RichHudTerminal.WarningFormat);
                        con1.Name = "none";
                        con2.Name = "none";
                        con3.Name = "none";
                    }
                }
            }
        }
    }
}