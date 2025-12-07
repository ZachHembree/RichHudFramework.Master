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
            public DemoPage() : base(new DemoBox())
            { }

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
					// Add list of supported test elements to the type list
					typeList = new ListBox<DemoElements>();
					var supportedTypes = Enum.GetValues(typeof(DemoElements)) as DemoElements[];

					for (int n = 0; n < supportedTypes.Length; n++)
						typeList.Add(supportedTypes[n].ToString(), supportedTypes[n]);

					// Spawn controls
					//
					// List of available control types
                    createButton = new BorderedButton() 
                    { 
                        Text = "Create", Padding = Vector2.Zero,
                        MouseInput = { LeftClickedCallback = InstantiateSelectedType }
                    };

                    typeColumn = new HudChain(true)
                    {
                        SizingMode = HudChainSizingModes.FitMembersOffAxis,
                        CollectionContainer = { { typeList, 1f }, createButton },
                        Spacing = 8f
                    };

                    // Instance list
                    instanceList = new ListBox<TestWindowNode>() { UpdateValueCallback = UpdateSelection };
                    removeButton = new BorderedButton() 
                    { 
                        Text = "Remove", Padding = Vector2.Zero,
                        MouseInput = { LeftClickedCallback = RemoveSelectedInstance }
                    };
                    clearAllButton = new BorderedButton() 
                    { 
                        Text = "Clear All", Padding = Vector2.Zero,
                        MouseInput = { LeftClickedCallback = ClearInstances }
                    };

                    instanceButtonRow = new HudChain(false)
                    {
                        Height = removeButton.Height,
                        SizingMode = HudChainSizingModes.FitMembersOffAxis,
                        CollectionContainer = { { removeButton, 1f }, { clearAllButton, 1f } },
                        Spacing = 8f,
                    };

                    instanceColumn = new HudChain(true)
                    {
                        SizingMode = HudChainSizingModes.FitMembersOffAxis,
                        CollectionContainer = { { instanceList, 1f }, instanceButtonRow },
                        Spacing = 8f
                    };

                    // Transform controls
                    //
                    // Column 1
                    screenSpaceToggle = new NamedCheckBox() 
                    { 
                        Name = "Screen Space", 
                        MouseInput = { ToolTip = "Compensates for FOV and resolution scaling" }
                    };
					xAxisBar = new NamedSliderBox() 
                    { 
                        Name = "AxisX", 
                        MouseInput = { ToolTip = "Quaternion rotation axis, X-component" },
                        Padding = new Vector2(40f, 0f), Min = -1f, Max = 1f,
						UpdateValueCallback = (obj, args) => 
                        { 
                            var slider = obj as NamedSliderBox; 
                            slider.ValueText = $"{slider.Value:G5}"; 
                        }
					};
					yAxisBar = new NamedSliderBox() 
                    { 
                        Name = "AxisY", 
                        MouseInput = { ToolTip = "Quaternion rotation axis, Y-component" },
                        Padding = new Vector2(40f, 0f), Min = -1f, Max = 1f,
						UpdateValueCallback = (obj, args) =>
						{
							var slider = obj as NamedSliderBox;
							slider.ValueText = $"{slider.Value:G5}";
						}
					};
					zAxisBar = new NamedSliderBox() 
                    { 
                        Name = "AxisZ",
                        MouseInput = { ToolTip = "Quaternion rotation axis, Z-component" },
                        Padding = new Vector2(40f, 0f), Min = -1f, Max = 1f,
						UpdateValueCallback = (obj, args) =>
						{
							var slider = obj as NamedSliderBox;
							slider.ValueText = $"{slider.Value:G5}";
						}
					};
                    angleBar = new NamedSliderBox() 
                    { 
                        Name = "Angle", 
                        MouseInput = { ToolTip = "Rotation around the axis, seen above, from -pi to +pi" },
                        Padding = new Vector2(40f, 0f), Min = -(float)(Math.PI), Max = (float)(Math.PI),
						UpdateValueCallback = (obj, args) => { angleBar.ValueText = $"{angleBar.Value:G5} rad"; }
					};

                    transformCol1 = new HudChain(true)
                    {
                        Spacing = 10f,
                        CollectionContainer = { screenSpaceToggle, xAxisBar, yAxisBar, zAxisBar, angleBar },
                    };

                    // Column 2
                    resScaleToggle = new NamedCheckBox() 
                    { 
                        Name = "High DPI Scaling" 
                    };
                    scaleBar = new NamedSliderBox() 
                    { 
                        Name = "Scale", 
                        Padding = new Vector2(40f, 0f), Min = 0.01f, Max = 2f,
						UpdateValueCallback = (obj, args) =>
						{
							var slider = obj as NamedSliderBox;
							slider.ValueText = $"{slider.Value:P1}";
						}
					};
                    xPosBar = new NamedSliderBox() 
                    { 
                        Name = "PosX", 
                        MouseInput = { ToolTip = "Matrix translation offset from camera, in meters. X-direction" },
                        Padding = new Vector2(40f, 0f), Min = -.5f, Max = .5f,
						UpdateValueCallback = (obj, args) =>
						{
							var slider = obj as NamedSliderBox;
							slider.ValueText = $"{slider.Value:G5}m";
						}
					};
                    yPosBar = new NamedSliderBox() 
                    { 
                        Name = "PosY",
						MouseInput = { ToolTip = "Matrix translation offset from camera, in meters. Y-direction" },
						Padding = new Vector2(40f, 0f), Min = -.5f, Max = .5f,
						UpdateValueCallback = (obj, args) =>
						{
							var slider = obj as NamedSliderBox;
							slider.ValueText = $"{slider.Value:G5}m";
						}
					};
                    zPosBar = new NamedSliderBox() 
                    { 
                        Name = "PosZ",
						MouseInput = { ToolTip = "Matrix translation offset from camera, in meters. Z-direction" },
						Padding = new Vector2(40f, 0f), Min = -.2f * 1E3f, Max = -.05f * 1E3f,
						UpdateValueCallback = (obj, args) =>
						{
							var slider = obj as NamedSliderBox;
							slider.ValueText = $"{slider.Value:G5}mm";
						}
					};

                    transformCol2 = new HudChain(true)
                    {
                        Spacing = 10f,
                        CollectionContainer = { resScaleToggle, scaleBar, xPosBar, yPosBar, zPosBar }
                    };

                    spawnControls = new HudChain(false)
                    {
                        Spacing = 16f,
                        SizingMode = HudChainSizingModes.FitMembersOffAxis,
                        IsMasking = true,
                        CollectionContainer = { { typeColumn, 1f }, { instanceColumn, 1f } },
                    };

                    transformControls = new HudChain(false)
                    {
                        SizingMode = HudChainSizingModes.AlignMembersCenter,
                        CollectionContainer = { transformCol1, transformCol2 },
                    };

                    var layout = new HudChain(true, this)
                    {
                        DimAlignment = DimAlignments.UnpaddedSize,
                        ParentAlignment = ParentAlignments.InnerLeft,
                        Spacing = 10f,
                        SizingMode = HudChainSizingModes.FitMembersOffAxis,
                        CollectionContainer = { { spawnControls, 1f }, transformControls }
                    };

                    Padding = new Vector2(40f, 8f);
                    demoRoot = new HudCollection(HudMain.HighDpiRoot);
				}

				protected override void HandleInput(Vector2 cursorPos)
                {
                    UpdateInstanceNames();

                    // Update matrix transform based on input
                    if (instanceList.Value != null)
                    {
                        CamSpaceNode node = instanceList.Value.AssocMember.cameraNode;

                        // Scale
                        node.IsScreenSpace = screenSpaceToggle.Value;
                        node.UseResScaling = resScaleToggle.Value;
                        node.PlaneScale = scaleBar.Value;

                        // Rotation
                        node.RotationAxis = new Vector3(xAxisBar.Value, yAxisBar.Value, zAxisBar.Value);
                        node.RotationAngle = angleBar.Value;

                        // Translation
                        node.TransformOffset = new Vector3D(xPosBar.Value, yPosBar.Value, zPosBar.Value * 1E-3f);
                    }
                }

                /// <summary>
                /// Updates instance names to make sure they're displaying the correct index and
                /// formatting 
                /// </summary>
                private void UpdateInstanceNames()
                {
                    var instanceCollection = instanceList.EntryList;

                    for (int n = 0; n < instanceCollection.Count; n++)
                    {
                        var entry = instanceCollection[n];
                        var windowNode = entry.AssocMember;

                        // Update index display
                        entry.Element.Text = $"[#{n}] {windowNode.elementEnum}";
                        windowNode.window.HeaderText = $"[#{n}] {windowNode.elementEnum}";
                    }
                }


                /// <summary>
                /// Updates UI to match the configuration of the node just selected
                /// </summary>
                private void UpdateSelection(object sender, EventArgs args)
                {
                    if (instanceList.Value != null)
                    {
                        CamSpaceNode node = instanceList.Value.AssocMember.cameraNode;

                        // Scale
                        screenSpaceToggle.Value = node.IsScreenSpace;
                        resScaleToggle.Value = node.UseResScaling;
                        scaleBar.Value = (float)node.PlaneScale;

                        // Rotation
                        angleBar.Value = node.RotationAngle;
                        xAxisBar.Value = node.RotationAxis.X;
                        yAxisBar.Value = node.RotationAxis.Y;
                        zAxisBar.Value = node.RotationAxis.Z;

                        // Translation
                        xPosBar.Value = (float)node.TransformOffset.X;
                        yPosBar.Value = (float)node.TransformOffset.Y;
                        zPosBar.Value = (float)node.TransformOffset.Z * 1E3f;
                    }
                }

                /// <summary>
                /// Creates a new test window instance with the child element type specified by the
                /// type list selection.
                /// </summary>
                private void InstantiateSelectedType(object sender, EventArgs args)
                {
                    if (typeList.Value != null)
                    {
                        DemoElements selection = typeList.Value.AssocMember;
                        var testElement = new TestWindowNode(selection);

                        demoRoot.Add(testElement);
                        instanceList.Add($"[#{instanceList.EntryList.Count}] {selection}", testElement);

                        // If selection is empty set selection to new element
                        if (instanceList.Value == null)
                            instanceList.SetSelection(testElement);
                    }
                }

                /// <summary>
                /// Removes the instance currently selected in the instance list when invoked.
                /// </summary>
                private void RemoveSelectedInstance(object sender, EventArgs args)
                {
                    if (instanceList.Value != null)
                    {
                        ListBoxEntry<TestWindowNode> selection = instanceList.Value;
                        TestWindowNode testNode = selection.AssocMember;
                        var instanceCollection = instanceList.EntryChain;

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