using VRageMath;

namespace RichHudFramework
{
    namespace UI.Server
    {
        public partial class DemoPage
        {
            /// <summary>
            /// Enum list of library elements that can be instantiated by the DemoPage
            /// </summary>
            private enum DemoElements : int
            {
                BorderedButton = 0,
                BorderedCheckBox = 1,
                Button = 2,
                ColorPickerRGB = 3,
                Dropdown = 4,
                LabelBoxButton = 5,
                ListBox = 7,
                NamedCheckBox = 8,
                NamedOnOffButton = 9,
                NamedSliderBox = 10,
                OnOffButton = 11,
                ScrollBar = 12,
                SliderBar = 13,
                SliderBox = 14,
                TextBox = 15,
                TextField = 16,
                TreeBox = 17,
                Label = 20,
                LabelBox = 21,
                TexturedBox = 22
            }

            /// <summary>
            /// Base class for windows used to contain the demo elements.
            /// </summary>
            private abstract class TestWindow : WindowBase
            {
                /// <summary>
                /// Demo element instance
                /// </summary>
                public HudElementBase Element { get; protected set; }

                public TestWindow(HudParentBase parent = null) : base(parent)
                {
                    Size = new Vector2(400f);
                    BodyColor = new Color(41, 54, 62, 150);
                    BorderColor = new Color(58, 68, 77);
                    AllowResizing = false;
                }
            }

            /// <summary>
            /// Generic test window that instantiates and parents a new instance of the given
            /// element type on construction.
            /// </summary>
            private class TestWindow<TElement> : TestWindow where TElement : HudElementBase, new()
            {
                /// <summary>
                /// Demo element instance
                /// </summary>
                public TElement Subtype { get; protected set; }

                public TestWindow(HudParentBase parent = null) : base(parent)
                {
                    Subtype = new TElement();
                    Element = Subtype;
                    Element.Register(body);
                }
            }

            /// <summary>
            /// Container node for test windows used by the demo page
            /// </summary>
            private class TestWindowNode : HudNodeBase
            {
                /// <summary>
                /// Window containing the test element, parented to cameraNode
                /// </summary>
                public readonly TestWindow window;

                /// <summary>
                /// Enum corresponding to the type of element parented to the test window
                /// </summary>
                public readonly DemoElements elementEnum;

                /// <summary>
                /// HudSpace node used by the test window
                /// </summary>
                public readonly CamSpaceNode cameraNode;

                public TestWindowNode(DemoElements elementEnum, HudParentBase parent = null) : base(parent)
                {
                    this.elementEnum = elementEnum;
                    cameraNode = new CamSpaceNode(this) { DrawCursorInHudSpace = true };

                    window = GetTestWindow(elementEnum);
                    window.Register(cameraNode);
                }

                /// <summary>
                /// Instantiates a new generic test window with the library element type
                /// corresponding to the DemoElements enum
                /// </summary>
                private static TestWindow GetTestWindow(DemoElements subtypeEnum)
                {
                    switch (subtypeEnum)
                    {
                        case DemoElements.BorderedButton:
                            return new TestWindow<BorderedButton>();
                        case DemoElements.BorderedCheckBox:
                            return new TestWindow<BorderedCheckBox>();
                        case DemoElements.Button:
                            return new TestWindow<Button>();
                        case DemoElements.ColorPickerRGB:
                            return new TestWindow<ColorPickerRGB>();
                        case DemoElements.Dropdown:
                            {
                                var dropdown = new TestWindow<Dropdown<int>>();
                                dropdown.Subtype.Add("Entry 1", 1);
                                dropdown.Subtype.Add("Entry 2", 2);
                                dropdown.Subtype.Add("Entry 3", 3);
                                return dropdown;
                            }
                        case DemoElements.LabelBoxButton:
                            return new TestWindow<LabelBoxButton>();
                        case DemoElements.ListBox:
                            {
                                var listbox = new TestWindow<ListBox<int>>();
                                listbox.Subtype.Add("Entry 1", 1);
                                listbox.Subtype.Add("Entry 2", 1);
                                listbox.Subtype.Add("Entry 3", 1);
                                return listbox;
                            }
                        case DemoElements.NamedCheckBox:
                            return new TestWindow<NamedCheckBox>();
                        case DemoElements.NamedOnOffButton:
                            return new TestWindow<NamedOnOffButton>();
                        case DemoElements.NamedSliderBox:
                            return new TestWindow<NamedSliderBox>();
                        case DemoElements.OnOffButton:
                            return new TestWindow<OnOffButton>();
                        case DemoElements.ScrollBar:
                            return new TestWindow<ScrollBar>();
                        case DemoElements.TextBox:
                            return new TestWindow<TextBox>();
                        case DemoElements.TreeBox:
                            {
                                var treebox = new TestWindow<TreeList<int>>();
                                treebox.Subtype.Add("Entry 1", 1);
                                treebox.Subtype.Add("Entry 2", 1);
                                treebox.Subtype.Add("Entry 3", 1);
                                return treebox;
                            }
                        case DemoElements.Label:
                            return new TestWindow<Label>();
                        case DemoElements.LabelBox:
                            return new TestWindow<LabelBox>();
                        case DemoElements.TexturedBox:
                            return new TestWindow<TexturedBox>();
                        case DemoElements.SliderBar:
                            return new TestWindow<SliderBar>();
                        case DemoElements.SliderBox:
                            return new TestWindow<SliderBox>();
                        case DemoElements.TextField:
                            return new TestWindow<TextField>();
                    }

                    return null;
                }
            }
        }
    }
}