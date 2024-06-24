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
                btnBorderThickness = 1f;
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
                        Size = new Vector2(234f, lineHeight),
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
                private readonly BorderedButton con1, con2, con3;

                private IBind bind;
                private IBindGroup group;

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

                    con1 = new BorderedButton()
                    {
                        Text = "none",
                        Padding = new Vector2(),
                        Size = new Vector2(126f, lineHeight),
                        BorderThickness = btnBorderThickness,
                    };

                    con1.MouseInput.LeftClicked += (sender, args) => GetNewControl(0);
                    con1.MouseInput.RightClicked += (sender, args) => RemoveControl(0);

                    con2 = new BorderedButton()
                    {
                        Text = "none",
                        Padding = new Vector2(),
                        Size = new Vector2(126f, lineHeight),
                        BorderThickness = btnBorderThickness,
                    };

                    con2.MouseInput.LeftClicked += (sender, args) => GetNewControl(1);
                    con2.MouseInput.RightClicked += (sender, args) => RemoveControl(1);

                    con3 = new BorderedButton()
                    {
                        Text = "none",
                        Padding = new Vector2(),
                        Size = new Vector2(126f, lineHeight),
                        BorderThickness = btnBorderThickness,
                    };

                    con3.MouseInput.LeftClicked += (sender, args) => GetNewControl(2);
                    con3.MouseInput.RightClicked += (sender, args) => RemoveControl(2);

                    var layout = new HudChain(false, this)
                    {
                        Spacing = 13f,
                        Padding = new Vector2(bindPadding, 0f),
                        DimAlignment = DimAlignments.UnpaddedSize,
                        SizingMode = HudChainSizingModes.FitMembersOffAxis,
                        CollectionContainer = { { bindName, 1f }, { con1, 0f }, { con2, 0f }, { con3, 0f } }
                    };

                    Height = lineHeight;
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
                /// Opens the rebind dialog for the given bind for the control specified
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
                                bind.TrySetCombo(combo, 0, false);
                            }
                        }
                    }
                }

                /// <summary>
                /// Updates the buttons for the bind controls to reflect the current configuration
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