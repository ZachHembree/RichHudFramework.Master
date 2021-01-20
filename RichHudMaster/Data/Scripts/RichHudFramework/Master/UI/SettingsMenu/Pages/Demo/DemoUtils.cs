using VRageMath;

namespace RichHudFramework
{
    namespace UI.Server
    {
        public partial class DemoPage
        {
            private enum LibraryElements : int
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
                DoubleLabelBox = 19,
                Label = 20,
                LabelBox = 21,
                TexturedBox = 22
            }

            private abstract class TestWindow : WindowBase
            {
                public HudElementBase Element { get; protected set; }

                public TestWindow(HudParentBase parent = null) : base(parent)
                {
                    Size = new Vector2(400f);
                    BodyColor = new Color(41, 54, 62, 150);
                    BorderColor = new Color(58, 68, 77);
                }

                protected override void Layout()
                {
                    MinimumSize = Element.Size + header.Size;
                    base.Layout();
                }
            }

            private class TestWindow<TElement> : TestWindow where TElement : HudElementBase, new()
            {
                public TElement Subtype { get; protected set; }

                public TestWindow(HudParentBase parent = null) : base(parent)
                {
                    Subtype = new TElement();
                    Element = Subtype;
                    Element.Register(body);
                }
            }

            private class TestWindowNode : HudNodeBase
            {
                public readonly TestWindow window;
                public readonly LibraryElements elementEnum;
                public readonly CamSpaceNode cameraNode;

                public TestWindowNode(LibraryElements elementEnum, HudParentBase parent = null) : base(parent)
                {
                    this.elementEnum = elementEnum;
                    cameraNode = new CamSpaceNode(this);

                    window = GetTestWindow(elementEnum);
                    window.Register(cameraNode);
                }

                private static TestWindow GetTestWindow(LibraryElements subtypeEnum)
                {
                    switch (subtypeEnum)
                    {
                        case LibraryElements.BorderedButton:
                            return new TestWindow<BorderedButton>();
                        case LibraryElements.BorderedCheckBox:
                            return new TestWindow<BorderedCheckBox>();
                        case LibraryElements.Button:
                            return new TestWindow<Button>();
                        case LibraryElements.ColorPickerRGB:
                            return new TestWindow<ColorPickerRGB>();
                        case LibraryElements.Dropdown:
                            return new TestWindow<Dropdown<int>>();
                        case LibraryElements.LabelBoxButton:
                            return new TestWindow<LabelBoxButton>();
                        case LibraryElements.ListBox:
                            return new TestWindow<ListBox<int>>();
                        case LibraryElements.NamedCheckBox:
                            return new TestWindow<NamedCheckBox>();
                        case LibraryElements.NamedOnOffButton:
                            return new TestWindow<NamedOnOffButton>();
                        case LibraryElements.NamedSliderBox:
                            return new TestWindow<NamedSliderBox>();
                        case LibraryElements.OnOffButton:
                            return new TestWindow<OnOffButton>();
                        case LibraryElements.ScrollBar:
                            return new TestWindow<ScrollBar>();
                        case LibraryElements.TextBox:
                            return new TestWindow<TextBox>();
                        case LibraryElements.TreeBox:
                            return new TestWindow<TreeBox<int>>();
                        case LibraryElements.DoubleLabelBox:
                            return new TestWindow<DoubleLabelBox>();
                        case LibraryElements.Label:
                            return new TestWindow<Label>();
                        case LibraryElements.LabelBox:
                            return new TestWindow<LabelBox>();
                        case LibraryElements.TexturedBox:
                            return new TestWindow<TexturedBox>();
                        case LibraryElements.SliderBar:
                            return new TestWindow<SliderBar>();
                        case LibraryElements.SliderBox:
                            return new TestWindow<SliderBox>();
                        case LibraryElements.TextField:
                            return new TestWindow<TextField>();
                    }

                    return null;
                }
            }
        }
    }
}