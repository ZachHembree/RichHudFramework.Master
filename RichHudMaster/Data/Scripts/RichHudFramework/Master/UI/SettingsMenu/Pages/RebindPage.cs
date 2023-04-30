using System;
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
            public IReadOnlyList<IBindGroup> BindGroups { get; }

            public RebindPage GroupContainer => this;

            private readonly BindGroupList bindGroups;

            public RebindPage() : base(new BindGroupList())
            {
                bindGroups = AssocMember as BindGroupList;

                BindGroups = new ReadOnlyCollectionData<IBindGroup>
                (
                    x => bindGroups.Collection[x].Element.BindGroup, 
                    () => bindGroups.Collection.Count
                );
            }

            /// <summary>
            /// Adds the given bind group to the page.
            /// </summary>
            public void Add(IBindGroup bindGroup)
            {
                var bindBox = new BindGroupBox() { BindGroup = bindGroup };
                bindGroups.Add(bindBox);
            }

            /// <summary>
            /// Adds the given bind group to the page along with its associated default configuration.
            /// </summary>
            public void Add(IBindGroup bindGroup, BindDefinition[] defaultBinds)
            {
                var bindBox = new BindGroupBox() { BindGroup = bindGroup, DefaultBinds = defaultBinds };
                bindGroups.Add(bindBox);
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
            /// Scrollable list of bind groups
            /// </summary>
            private class BindGroupList : ScrollBox<ScrollBoxEntry<BindGroupBox>, BindGroupBox>
            {
                public BindGroupList(HudParentBase parent = null) : base(true, parent)
                {
                    Spacing = 30f;
                    SizingMode = HudChainSizingModes.FitMembersOffAxis;

                    Background.Visible = false;
                }

                protected override void Layout()
                {
                    base.Layout();

                    SliderBar slider = ScrollBar.slide;
                    slider.BarColor = TerminalFormatting.OuterSpace.SetAlphaPct(HudMain.UiBkOpacity);
                }
            }

            /// <summary>
            /// Scrollable list of keybinds. Supports, at most, three controls per bind.
            /// </summary>
            private class BindGroupBox : HudElementBase
            {
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

                private readonly Label name;
                private readonly BorderedButton resetButton;
                private readonly ScrollBox<ScrollBoxEntry<BindBox>, BindBox> scrollBox;

                private IBindGroup _bindGroup;
                private BindDefinition[] defaultBinds;

                public BindGroupBox(HudParentBase parent = null) : base(parent)
                {
                    name = new Label()
                    {
                        Format = GlyphFormat.White,
                        AutoResize = false,
                        Height = 24f,
                        Padding = new Vector2(0f, 24f),
                    };

                    resetButton = new BorderedButton(this)
                    {
                        Text = "Defaults",
                        Size = new Vector2(234f, 44f),
                        ParentAlignment = ParentAlignments.InnerTopRight,
                        Visible = false,
                        BorderThickness = 1f,
                    };

                    resetButton.MouseInput.LeftClicked += ResetBinds;

                    scrollBox = new ScrollBox<ScrollBoxEntry<BindBox>, BindBox>(true)
                    {
                        SizingMode = HudChainSizingModes.FitMembersOffAxis,
                        Spacing = 8f,
                    };

                    scrollBox.Background.Visible = false;

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

                    var layout = new HudChain(true, this)
                    {
                        SizingMode = HudChainSizingModes.FitMembersOffAxis,
                        DimAlignment = DimAlignments.UnpaddedSize,
                        Padding = new Vector2(20f, 0f),
                        Offset = new Vector2(-10f, 0f),
                        CollectionContainer = { name, divider1, { scrollBox, 1f}, divider2 },
                    };

                    Height = 338f;
                }

                protected override void Layout()
                {
                    SliderBar slider = scrollBox.ScrollBar.slide;
                    slider.BarColor = TerminalFormatting.OuterSpace.SetAlphaPct(HudMain.UiBkOpacity);
                }

                /// <summary>
                /// Applies the default configuration to the bind group if defaults are defined.
                /// </summary>
                private void ResetBinds(object sender, EventArgs args)
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
                            if (scrollBox.Collection.Count == n)
                                scrollBox.Add(new BindBox());

                            scrollBox.Collection[n].Enabled = true;
                            scrollBox.Collection[n].Element.SetBind(BindGroup[n], BindGroup);
                        }
                    }
                }
            }

            /// <summary>
            /// Control for individual keybinds. Supports up to three controls per bind.
            /// </summary>
            private class BindBox : HudElementBase
            {
                private readonly Label bindName;
                private readonly BorderedButton con1, con2, con3;

                private IBind bind;
                private IBindGroup group;

                public BindBox(HudParentBase parent = null) : base(parent)
                {                    
                    bindName = new Label(this)
                    {
                        Text = "NewBindBox",
                        Format = GlyphFormat.Blueish,
                        AutoResize = false,
                        Size = new Vector2(150f, 44f),
                        ParentAlignment = ParentAlignments.InnerLeft,
                        DimAlignment = DimAlignments.UnpaddedHeight,
                    };

                    con1 = new BorderedButton()
                    {
                        Text = "none",
                        Padding = new Vector2(),
                        Size = new Vector2(126f, 44f),
                        BorderThickness = 1f,
                    };

                    con1.MouseInput.LeftClicked += (sender, args) => GetNewControl(0);
                    con1.MouseInput.RightClicked += (sender, args) => RemoveControl(0);

                    con2 = new BorderedButton()
                    {
                        Text = "none",
                        Padding = new Vector2(),
                        Size = new Vector2(126f, 44f),
                        BorderThickness = 1f,
                    };

                    con2.MouseInput.LeftClicked += (sender, args) => GetNewControl(1);
                    con2.MouseInput.RightClicked += (sender, args) => RemoveControl(1);

                    con3 = new BorderedButton()
                    {
                        Text = "none",
                        Padding = new Vector2(),
                        Size = new Vector2(126f, 44f),
                        BorderThickness = 1f,
                    };

                    con3.MouseInput.LeftClicked += (sender, args) => GetNewControl(2);
                    con3.MouseInput.RightClicked += (sender, args) => RemoveControl(2);

                    var layout = new HudChain(false, this)
                    {
                        SizingMode = HudChainSizingModes.FitMembersOffAxis,
                        Spacing = 13f,
                        Width = 404f,
                        Padding = new Vector2(32f, 0f),
                        ParentAlignment = ParentAlignments.InnerRight,
                        DimAlignment = DimAlignments.UnpaddedHeight,
                        CollectionContainer = { con1, con2, con3 }
                    };

                    Size = new Vector2(400f, 44f);
                }

                public void Reset()
                {
                    var fullGroup = group as BindManager.BindGroup;
                    fullGroup.BindChanged -= OnBindChanged;

                    bindName.TextBoard.Clear();
                    bind = null;
                    group = null;
                }

                public void SetBind(IBind bind, IBindGroup group)
                {
                    this.bind = bind;
                    this.group = group;
                    UpdateBindText();

                    var fullGroup = group as BindManager.BindGroup;
                    fullGroup.BindChanged += OnBindChanged;
                }

                private void OnBindChanged(object sender, EventArgs args)
                {
                    UpdateBindText();
                }

                /// <summary>
                /// Opens the rebind dialog for the given bind for the control specified.
                /// </summary>
                private void GetNewControl(int index)
                {
                    RichHudTerminal.CloseMenu();
                    RebindDialog.UpdateBind(bind, index, DialogClosed);
                }

                /// <summary>
                /// Rebind dialog callback.
                /// </summary>
                private void DialogClosed()
                {
                    RichHudTerminal.OpenMenu();
                }

                /// <summary>
                /// Removes the control at the index specified.
                /// </summary>
                private void RemoveControl(int index)
                {
                    if (index < 3)
                    {
                        List<IControl> combo = bind.GetCombo();

                        if (index < combo.Count)
                        {
                            if (index == 0 && combo.Count == 1)
                            {
                                bind.ClearCombo();
                            }
                            else
                            {
                                combo.RemoveAt(index);
                                bind.TrySetCombo(combo, false);
                            }
                        }
                    }
                }

                /// <summary>
                /// Updates the buttons for the bind controls to reflect the current configuration.
                /// </summary>
                private void UpdateBindText()
                {
                    bindName.Text = bind.Name;
                    List<IControl> combo = bind.GetCombo();

                    if (combo != null && combo.Count > 0)
                    {
                        if (!group.DoesComboConflict(combo, bind))
                            bindName.TextBoard.SetFormatting(GlyphFormat.Blueish);
                        else
                            bindName.TextBoard.SetFormatting(TerminalFormatting.WarningFormat);

                        con1.Text = combo[0].DisplayName;

                        if (combo.Count > 1)
                            con2.Text = combo[1].DisplayName;
                        else
                            con2.Text = "none";

                        if (combo.Count > 2)
                            con3.Text = combo[2].DisplayName;
                        else
                            con3.Text = "none";
                    }
                    else
                    {
                        bindName.TextBoard.SetFormatting(TerminalFormatting.WarningFormat);
                        con1.Text = "none";
                        con2.Text = "none";
                        con3.Text = "none";
                    }
                }
            }
        }
    }
}