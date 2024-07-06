using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using VRage;
using VRageMath;

namespace RichHudFramework
{
    using BindDefinitionDataOld = MyTuple<string, string[]>;
    using BindDefinitionData = MyTuple<string, string[], string[][]>;

    namespace UI.Server
    {
        /// <summary>
        /// Control panel for viewing/modifying a collection of <see cref="IBindGroup"/> binds
        /// </summary>
        public class RebindPage : TerminalPageBase, IRebindPage
        {
            private const float 
                listPaddingH = 20f,
                listPaddingV = 8f,
                lineSpacing = 8f,
                lineHeight = 44f,
                dividerHeight = 1f,
                bindPadding = 32f,
                btnBorderThickness = 1f,
                resetBtnWidth = 234f,
                comboBtnWidth = 200f;
            private static readonly Color dividerColor = new Color(53, 66, 75);
            
            /// <summary>
            /// List of bind groups registered to the page
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
            /// Adds the given bind group to the page
            /// </summary>
            public void Add(IBindGroup bindGroup)
            {
                var bindBox = new BindGroupBox(bindGroup);
                bindGroups.Add(bindBox);
            }

            /// <summary>
            /// Adds the given bind group to the page along with its associated default configuration
            /// </summary>
            public void Add(IBindGroup bindGroup, BindDefinition[] defaultBinds)
            {
                var bindBox = new BindGroupBox(bindGroup, defaultBinds);
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
                                if (data is MyTuple<object, BindDefinitionDataOld[]>)
                                {
                                    var args = (MyTuple<object, BindDefinitionDataOld[]>)data;
                                    BindDefinition[] defaults = new BindDefinition[args.Item2.Length];

                                    for (int n = 0; n < defaults.Length; n++)
                                        defaults[n] = args.Item2[n];

                                    Add(args.Item1 as IBindGroup, defaults);
                                    break;
                                }
                                else
                                {
                                    var args = (MyTuple<object, BindDefinitionData[]>)data;
                                    BindDefinition[] defaults = new BindDefinition[args.Item2.Length];

                                    for (int n = 0; n < defaults.Length; n++)
                                        defaults[n] = (BindDefinition)args.Item2[n];

                                    Add(args.Item1 as IBindGroup, defaults);
                                    break;
                                }
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
                    SizingMode = HudChainSizingModes.FitMembersOffAxis;
                    Background.Visible = false;

                    Spacing = lineSpacing;
                    Padding = new Vector2(listPaddingH, listPaddingV);
                }

                protected override void Layout()
                {
                    foreach (var groupBoxEntry in Collection)
                    {
                        if (groupBoxEntry.Enabled && groupBoxEntry.Element.Visible)
                        {
                            BindGroupBox bgBox = groupBoxEntry.Element;
                            bgBox.Size = bgBox.GetRangeSize();
                        }
                    }

                    base.Layout();

                    SliderBar slider = ScrollBar.slide;
                    slider.BarColor = TerminalFormatting.OuterSpace.SetAlphaPct(HudMain.UiBkOpacity);
                }
            }

            /// <summary>
            /// Non-scrollable list of keybinds representing a <see cref="IBindGroup"/>
            /// </summary>
            private class BindGroupBox : HudChain
            {
                /// <summary>
                /// Bind group associated with the control
                /// </summary>
                public IBindGroup BindGroup { get; private set; }

                /// <summary>
                /// Default configuration associated with the bind group
                /// </summary>
                public BindDefinition[] DefaultBinds { get; private set; }

                private readonly Label name;
                private readonly BorderedButton resetButton;
                private readonly int bindOffset;

                public BindGroupBox(IBindGroup group, BindDefinition[] defaults = null) : base(null)
                {
                    AlignVertical = true;
                    SizingMode = HudChainSizingModes.FitMembersOffAxis;
                    Spacing = lineSpacing;

                    var divider1 = new TexturedBox()
                    {
                        Color = dividerColor,
                        Height = dividerHeight,
                    };
                    Add(divider1);

                    name = new Label()
                    {
                        Format = GlyphFormat.White,
                        AutoResize = false,
                        Height = lineHeight
                    };
                    Add(name);

                    resetButton = new BorderedButton(this)
                    {
                        Text = "Defaults",
                        Size = new Vector2(resetBtnWidth, lineHeight),
                        Padding = new Vector2(0f),
                        Offset = new Vector2(0f, -(dividerHeight + lineSpacing)),
                        ParentAlignment = ParentAlignments.InnerTopRight,
                        Visible = false,
                        BorderThickness = btnBorderThickness,
                    };

                    resetButton.MouseInput.LeftClicked += ResetBinds;

                    var divider2 = new TexturedBox()
                    {
                        Color = dividerColor,
                        Height = dividerHeight,
                    };
                    Add(divider2);
                    bindOffset = Count;

                    SetGroup(group);
                    DefaultBinds = defaults;
                }

                protected override void Layout()
                {
                    resetButton.Visible = (DefaultBinds != null);
                    base.Layout();
                }

                private void SetGroup(IBindGroup group)
                {
                    if (group != null)
                    {
                        BindGroup = group;
                        name.Text = $"Group: {BindGroup.Name}";

                        for (int n = 0; n < BindGroup.Count; n++)
                        {
                            if (n >= (Count - bindOffset))
                            {
                                // Individual inserts should be nicer than this
                                var container = new HudElementContainer();
                                container.SetElement(new BindBox());
                                Insert(Count, container);
                            }
                        }

                        UpdateBindGroup();
                    }
                    else
                    {
                        throw new Exception("BindGroup cannot be null.");
                    }
                }

                /// <summary>
                /// Updates bind combo text
                /// </summary>
                private void UpdateBindGroup()
                {
                    if (BindGroup != null)
                    {
                        for (int i = 0; i < BindGroup.Count; i++)
                        {
                            var bind = Collection[i + bindOffset].Element as BindBox;
                            bind.SetBind(BindGroup[i], BindGroup);
                        }
                    }
                }

                /// <summary>
                /// Applies the default configuration to the bind group, if defaults are defined
                /// </summary>
                private void ResetBinds(object sender, EventArgs args)
                {
                    if (DefaultBinds != null)
                    {
                        BindGroup.TryLoadBindData(DefaultBinds);
                        UpdateBindGroup();
                    }
                }
            }

            /// <summary>
            /// Control used to represent <see cref="IBind"/> key combos and update them
            /// </summary>
            private class BindBox : HudElementBase
            {
                private readonly Label bindName;
                private readonly BorderedButton[] combos;

                private BindManager.BindGroup.Bind bind;
                private BindManager.BindGroup group;

                public BindBox(HudParentBase parent = null) : base(parent)
                {                    
                    bindName = new Label()
                    {
                        Text = "NewBindBox",
                        Format = GlyphFormat.Blueish,
                        AutoResize = false,
                        Size = new Vector2(150f, lineHeight),
                        ParentAlignment = ParentAlignments.InnerLeft,
                        DimAlignment = DimAlignments.UnpaddedHeight,
                    };

                    combos = new BorderedButton[2];
                    combos[0] = new BorderedButton()
                    {
                        Text = "none",
                        Padding = new Vector2(),
                        Size = new Vector2(comboBtnWidth, lineHeight),
                        BorderThickness = btnBorderThickness,
                    };

                    combos[0].MouseInput.LeftClicked += (sender, args) => GetNewCombo(0);
                    combos[0].MouseInput.RightClicked += (sender, args) => bind.ClearCombo(0);

                    combos[1] = new BorderedButton()
                    {
                        Text = "none",
                        Padding = new Vector2(),
                        Size = new Vector2(comboBtnWidth, lineHeight),
                        BorderThickness = btnBorderThickness,
                    };

                    combos[1].MouseInput.LeftClicked += (sender, args) => GetNewCombo(1);
                    combos[1].MouseInput.RightClicked += (sender, args) => bind.ClearCombo(1);

                    var layout = new HudChain(false, this)
                    {
                        Spacing = 13f,
                        Padding = new Vector2(bindPadding, 0f),
                        DimAlignment = DimAlignments.UnpaddedSize,
                        SizingMode = HudChainSizingModes.FitMembersOffAxis,
                        CollectionContainer = { { bindName, 1f }, { combos[0], 0f }, { combos[1], 0f } }
                    };

                    Height = lineHeight;
                }

                public void Reset()
                {
                    group.BindChanged -= OnBindChanged;
                    bindName.TextBoard.Clear();
                    bind = null;
                    group = null;
                }

                public void SetBind(IBind bind, IBindGroup group)
                {
                    this.bind = bind as BindManager.BindGroup.Bind;
                    this.group = group as BindManager.BindGroup;
                    UpdateBindText();

                    this.group.BindChanged += OnBindChanged;
                }

                private void OnBindChanged(object sender, EventArgs args)
                {
                    UpdateBindText();
                }

                /// <summary>
                /// Opens the rebind dialog for the given bind alias
                /// </summary>
                private void GetNewCombo(int alias)
                {
                    RichHudTerminal.CloseMenu();
                    RebindDialog.UpdateBind(bind, alias, DialogClosed);
                }

                /// <summary>
                /// Rebind dialog callback.
                /// </summary>
                private void DialogClosed()
                {
                    RichHudTerminal.OpenMenu();
                }

                /// <summary>
                /// Updates the buttons for the bind controls to reflect the current configuration
                /// </summary>
                private void UpdateBindText()
                {
                    bindName.Text = bind.Name;

                    if (bind.AliasCount > 0)
                    {
                        bool isConflicting = false;

                        for (int i = 0; i < bind.AliasCount; i++)
                        {
                            if (group.DoesComboConflict(bind, i))
                            {
                                isConflicting = true;
                                break;
                            }
                        }

                        if (isConflicting)
                            bindName.TextBoard.SetFormatting(TerminalFormatting.WarningFormat);
                        else
                            bindName.TextBoard.SetFormatting(GlyphFormat.Blueish);

                        for (int i = 0; i < combos.Length; i++)
                            combos[i].Text = bind.ToString(i, false);
                    }
                    else
                    {
                        bindName.TextBoard.SetFormatting(TerminalFormatting.WarningFormat);

                        for (int i = 0; i < combos.Length; i++)
                            combos[i].Text = "none";
                    }
                }
            }
        }
    }
}