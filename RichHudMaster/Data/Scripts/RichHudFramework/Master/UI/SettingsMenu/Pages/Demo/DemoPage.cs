using System;
using VRageMath;

namespace RichHudFramework
{
    namespace UI.Server
    {
        /// <summary>
        /// Test page used to demonstrate library UI elements and world draw.
        /// </summary>
        public partial class DemoPage : TerminalPageBase
        {
            public DemoPage()
            {
                SetElement(new DemoBox());
            }

            private class DemoBox : HudElementBase
            {
                // Spawn controls
                private readonly ListBox<DemoElements> typeList;
                private readonly BorderedButton createButton;
                private readonly HudChain typeColumn;

                private readonly ListBox<TestWindowNode> instanceList;
                private readonly BorderedButton removeButton, clearAllButton;
                private readonly HudChain instanceButtonRow, instanceColumn, spawnControls;

                // Transform controls
                private readonly NamedCheckBox screenSpaceToggle;
                private readonly NamedSliderBox xAxisBar, yAxisBar, zAxisBar, angleBar;

                private readonly NamedCheckBox resScaleToggle;
                private readonly NamedSliderBox scaleBar, xPosBar, yPosBar, zPosBar;

                private readonly HudChain transformCol1, transformCol2, transformControls;

                // Test window container
                private readonly HudCollection demoRoot;

                public DemoBox(HudParentBase parent = null) : base(parent)
                {
                    // Spawn controls
                    //
                    // List of available control types
                    typeList = new ListBox<DemoElements>();
                    createButton = new BorderedButton() { Text = "Create", Padding = Vector2.Zero };

                    typeColumn = new HudChain(true)
                    {
                        SizingMode = HudChainSizingModes.FitMembersOffAxis | HudChainSizingModes.FitChainBoth,
                        CollectionContainer = { typeList, createButton },
                        Spacing = 8f
                    };

                    // Add list of supported test elements to the type list
                    var supportedTypes = Enum.GetValues(typeof(DemoElements)) as DemoElements[];

                    for (int n = 0; n < supportedTypes.Length; n++)
                        typeList.Add(supportedTypes[n].ToString(), supportedTypes[n]);

                    createButton.MouseInput.LeftClicked += InstantiateSelectedType;

                    // Instance list
                    instanceList = new ListBox<TestWindowNode>();
                    removeButton = new BorderedButton() { Text = "Remove", Padding = Vector2.Zero };
                    clearAllButton = new BorderedButton() { Text = "Clear All", Padding = Vector2.Zero };

                    instanceButtonRow = new HudChain(false)
                    {
                        SizingMode = HudChainSizingModes.FitMembersBoth | HudChainSizingModes.FitChainBoth,
                        CollectionContainer = { removeButton, clearAllButton },
                        Spacing = 8f,
                    };

                    instanceColumn = new HudChain(true)
                    {
                        SizingMode = HudChainSizingModes.FitMembersOffAxis | HudChainSizingModes.FitChainBoth,
                        CollectionContainer = { instanceList, instanceButtonRow },
                        Spacing = 8f
                    };

                    removeButton.MouseInput.LeftClicked += RemoveSelectedInstance;
                    clearAllButton.MouseInput.LeftClicked += ClearInstances;
                    instanceList.SelectionChanged += UpdateSelection;

                    // Transform controls
                    //
                    // Column 1
                    screenSpaceToggle = new NamedCheckBox() { Name = "Screen Space" };
                    xAxisBar = new NamedSliderBox() { Name = "AxisX", Padding = new Vector2(40f, 0f), Min = -1f, Max = 1f };
                    yAxisBar = new NamedSliderBox() { Name = "AxisY", Padding = new Vector2(40f, 0f), Min = -1f, Max = 1f };
                    zAxisBar = new NamedSliderBox() { Name = "AxisZ", Padding = new Vector2(40f, 0f), Min = -1f, Max = 1f };
                    angleBar = new NamedSliderBox() { Name = "Angle", Padding = new Vector2(40f, 0f), Min = -(float)(Math.PI), Max = (float)(Math.PI) };

                    transformCol1 = new HudChain(true)
                    {
                        Spacing = 10f,
                        CollectionContainer = { screenSpaceToggle, xAxisBar, yAxisBar, zAxisBar, angleBar }
                    };

                    // Column 2
                    resScaleToggle = new NamedCheckBox() { Name = "Res Scaling" };
                    scaleBar = new NamedSliderBox() { Name = "Scale", Padding = new Vector2(40f, 0f), Min = 0.001f, Max = 1f };
                    xPosBar = new NamedSliderBox() { Name = "PosX", Padding = new Vector2(40f, 0f), Min = -.5f, Max = .5f };
                    yPosBar = new NamedSliderBox() { Name = "PosY", Padding = new Vector2(40f, 0f), Min = -.5f, Max = .5f };
                    zPosBar = new NamedSliderBox() { Name = "PosZ", Padding = new Vector2(40f, 0f), Min = -2f, Max = 0f };

                    transformCol2 = new HudChain(true)
                    {
                        Spacing = 10f,
                        CollectionContainer = { resScaleToggle, scaleBar, xPosBar, yPosBar, zPosBar }
                    };

                    spawnControls = new HudChain(false)
                    {
                        CollectionContainer = { typeColumn, instanceColumn },
                        Spacing = 16f,
                    };

                    transformControls = new HudChain(false)
                    {
                        CollectionContainer = { transformCol1, transformCol2 }
                    };

                    var layout = new HudChain(true, this)
                    {
                        ParentAlignment = ParentAlignments.Left | ParentAlignments.Inner,
                        Spacing = 10f,
                        CollectionContainer = { spawnControls, transformControls }
                    };

                    Padding = new Vector2(40f, 8f);
                    demoRoot = new HudCollection(HudMain.Root);
                }

                protected override void Layout()
                {
                    // Adjust height of the top row to make room for the bottom row
                    Vector2 size = cachedSize - cachedPadding;
                    float colWidth = (size.X - spawnControls.Padding.X - spawnControls.Spacing) / 2f,
                        listHeight = (size.Y - spawnControls.Padding.Y - createButton.Height - typeColumn.Spacing - transformControls.Height);

                    typeColumn.Width = colWidth;
                    instanceColumn.Width = colWidth;
                    instanceButtonRow.MemberMaxSize = new Vector2((colWidth - instanceButtonRow.Spacing) / 2f, removeButton.Height);

                    typeList.Height = listHeight;
                    instanceList.Height = listHeight;

                    UpdateSliderValueText();
                    UpdateInstanceNames();
                }

                /// <summary>
                /// Updates slider value text readout to show their current values.
                /// </summary>
                private void UpdateSliderValueText()
                {
                    // Update col 1 slider value text
                    angleBar.ValueText = $"{angleBar.Current:G5} rad";
                    xAxisBar.ValueText = $"{xAxisBar.Current:G5}";
                    yAxisBar.ValueText = $"{yAxisBar.Current:G5}";
                    zAxisBar.ValueText = $"{zAxisBar.Current:G5}";

                    // Update col 2 slider value text
                    scaleBar.ValueText = $"{scaleBar.Current:G5}";
                    xPosBar.ValueText = $"{xPosBar.Current:G5}";
                    yPosBar.ValueText = $"{yPosBar.Current:G5}";
                    zPosBar.ValueText = $"{zPosBar.Current:G5}";
                }

                /// <summary>
                /// Updates instance names to make sure they're displaying the correct index and
                /// formatting 
                /// </summary>
                private void UpdateInstanceNames()
                {
                    var instanceCollection = instanceList.HudCollection;

                    for (int n = 0; n < instanceCollection.Count; n++)
                    {
                        GlyphFormat format;
                        var entry = instanceCollection[n];
                        var windowNode = entry.AssocMember;

                        // Update instance list text to color code entries based on whether the corresponding nodes are
                        // visible, in front of and facing the camera
                        if (entry.AssocMember.Visible)
                            format = GlyphFormat.White;
                        else if (!entry.AssocMember.HudSpace.IsFacingCamera)
                            format = new GlyphFormat(color: Color.Yellow);
                        else
                            format = TerminalFormatting.WarningFormat;

                        // Update index display
                        entry.Element.Format = format;
                        entry.Element.Text = $"[#{n}] {windowNode.elementEnum}";
                        windowNode.window.HeaderText = $"[#{n}] {windowNode.elementEnum}";
                    }
                }

                protected override void HandleInput(Vector2 cursorPos)
                {
                    // Update matrix transform based on input
                    if (instanceList.Selection != null)
                    {
                        CamSpaceNode node = instanceList.Selection.AssocMember.cameraNode;

                        // Scale
                        node.IsScreenSpace = screenSpaceToggle.IsBoxChecked;
                        node.UseResScaling = resScaleToggle.IsBoxChecked;
                        node.PlaneScale = scaleBar.Current;

                        // Rotation
                        node.RotationAxis = new Vector3(xAxisBar.Current, yAxisBar.Current, zAxisBar.Current);
                        node.RotationAngle = angleBar.Current;

                        // Translation
                        node.TransformOffset = new Vector3D(xPosBar.Current, yPosBar.Current, zPosBar.Current);
                    }
                }

                /// <summary>
                /// Updates UI to match the configuration of the node just selected
                /// </summary>
                private void UpdateSelection(object sender, EventArgs args)
                {
                    if (instanceList.Selection != null)
                    {
                        CamSpaceNode node = instanceList.Selection.AssocMember.cameraNode;

                        // Scale
                        screenSpaceToggle.IsBoxChecked = node.IsScreenSpace;
                        resScaleToggle.IsBoxChecked = node.UseResScaling;
                        scaleBar.Current = (float)node.PlaneScale;

                        // Rotation
                        angleBar.Current = node.RotationAngle;
                        xAxisBar.Current = node.RotationAxis.X;
                        yAxisBar.Current = node.RotationAxis.Y;
                        zAxisBar.Current = node.RotationAxis.Z;

                        // Translation
                        xPosBar.Current = (float)node.TransformOffset.X;
                        yPosBar.Current = (float)node.TransformOffset.Y;
                        zPosBar.Current = (float)node.TransformOffset.Z;
                    }
                }

                /// <summary>
                /// Creates a new test window instance with the child element type specified by the
                /// type list selection.
                /// </summary>
                private void InstantiateSelectedType(object sender, EventArgs args)
                {
                    if (typeList.Selection != null)
                    {
                        DemoElements selection = typeList.Selection.AssocMember;
                        var testElement = new TestWindowNode(selection);

                        demoRoot.Add(testElement);
                        instanceList.Add($"[#{instanceList.ListEntries.Count}] {selection}", testElement);

                        // If selection is empty set selection to new element
                        if (instanceList.Selection == null)
                            instanceList.SetSelection(testElement);
                    }
                }

                /// <summary>
                /// Removes the instance currently selected in the instance list when invoked.
                /// </summary>
                private void RemoveSelectedInstance(object sender, EventArgs args)
                {
                    if (instanceList.Selection != null)
                    {
                        ListBoxLabel<TestWindowNode> selection = instanceList.Selection;
                        TestWindowNode testNode = selection.AssocMember;
                        var instanceCollection = instanceList.HudCollection;

                        int index = instanceCollection.FindIndex(x => x.AssocMember == testNode);
                        instanceList.RemoveAt(index);
                        testNode.Unregister();

                        // Attempt to select the previous member
                        if (index > 0)
                            index--;

                        if (instanceCollection.Count > 0)
                            instanceList.SetSelectionAt(index);
                    }
                }

                /// <summary>
                /// Removes all test windows currently registered
                /// </summary>
                private void ClearInstances(object sender, EventArgs args)
                {
                    instanceList.ClearEntries();
                    demoRoot.Clear();
                }
            }
        }
    }
}