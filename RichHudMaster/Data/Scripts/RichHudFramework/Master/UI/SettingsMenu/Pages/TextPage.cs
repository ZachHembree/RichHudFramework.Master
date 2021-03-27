using RichHudFramework.UI.Rendering;
using RichHudFramework.UI.Rendering.Server;
using System;
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
        /// <summary>
        /// Scrollable vertically scrolling, wrapped text page used by the terminal.
        /// </summary>
        public class TextPage : TerminalPageBase, ITextPage
        {
            /// <summary>
            /// Gets/sets header text
            /// </summary>
            public RichText HeaderText { get { return textBox.HeaderText; } set { textBox.HeaderText = value; } }

            /// <summary>
            /// Gets/sets subheader text
            /// </summary>
            public RichText SubHeaderText { get { return textBox.SubHeaderText; } set { textBox.SubHeaderText = value; } }

            /// <summary>
            /// Contents of the text box.
            /// </summary>
            public RichText Text { get { return textBox.Text; } set { textBox.Text = value; } }

            /// <summary>
            /// Text builder used to control the contents of the page
            /// </summary>
            public ITextBuilder TextBuilder => textBox.TextBuilder;

            private readonly ScrollableTextBox textBox;

            public TextPage() : base(new ScrollableTextBox())
            {
                textBox = AssocMember as ScrollableTextBox;
            }

            protected override object GetOrSetMember(object data, int memberEnum)
            {
                if (memberEnum > 9)
                {
                    switch ((TextPageAccessors)memberEnum)
                    {
                        case TextPageAccessors.GetOrSetHeader:
                            {
                                if (data == null)
                                    return textBox.HeaderText.apiData;
                                else
                                    textBox.HeaderText = new RichText(data as List<RichStringMembers>); break;
                            }
                        case TextPageAccessors.GetOrSetSubheader:
                            {
                                if (data == null)
                                    return textBox.SubHeaderText.apiData;
                                else
                                    textBox.SubHeaderText = new RichText(data as List<RichStringMembers>); break;
                            }
                        case TextPageAccessors.GetOrSetText:
                            {
                                if (data == null)
                                    return textBox.Text.apiData;
                                else
                                    textBox.Text = new RichText(data as List<RichStringMembers>); break;
                            }
                        case TextPageAccessors.GetTextBuilder:
                            return (TextBuilder as TextBuilder).GetApiData();
                    }

                    return null;
                }
                else
                    return base.GetOrSetMember(data, memberEnum);
            }

            private class ScrollableTextBox : HudElementBase
            {
                /// <summary>
                /// Gets/sets header text
                /// </summary>
                public RichText HeaderText { get { return header.Text; } set { header.Text = value; } }

                /// <summary>
                /// Gets/sets subheader text
                /// </summary>
                public RichText SubHeaderText { get { return subheader.Text; } set { subheader.Text = value; } }

                /// <summary>
                /// Contents of the text box.
                /// </summary>
                public RichText Text { get { return textBox.Text; } set { textBox.Text = value; } }

                /// <summary>
                /// Text builder used to control the contents of the page
                /// </summary>
                public ITextBuilder TextBuilder => textBox.TextBoard;

                private readonly TextBox textBox;
                private readonly Label header, subheader;
                private readonly ScrollBar verticalScroll;
                private readonly TexturedBox headerDivider, scrollDivider;

                public ScrollableTextBox(HudParentBase parent = null) : base(parent)
                {
                    header = new Label(this)
                    {
                        ParentAlignment = ParentAlignments.Top | ParentAlignments.Left | ParentAlignments.Inner,
                        Height = 24f,
                        AutoResize = false,
                        Format = new GlyphFormat(Color.White, TextAlignment.Center)
                    };

                    subheader = new Label(header)
                    {
                        ParentAlignment = ParentAlignments.Bottom,
                        Height = 20f,
                        Padding = new Vector2(0f, 10f),
                        BuilderMode = TextBuilderModes.Wrapped,
                        AutoResize = false,
                        VertCenterText = false,
                        Format = new GlyphFormat(Color.White, TextAlignment.Center, .8f),
                    };

                    textBox = new TextBox(subheader)
                    {
                        ParentAlignment = ParentAlignments.Bottom | ParentAlignments.Left | ParentAlignments.InnerH,
                        Padding = new Vector2(8f, 8f),
                        BuilderMode = TextBuilderModes.Wrapped,
                        AutoResize = false,
                        Format = GlyphFormat.White,
                        VertCenterText = false,
                        EnableEditing = false,
                        EnableHighlighting = true,
                        ClearSelectionOnLoseFocus = true
                    };

                    headerDivider = new TexturedBox(textBox)
                    {
                        Color = new Color(53, 66, 75),
                        ParentAlignment = ParentAlignments.Top,
                        DimAlignment = DimAlignments.Width,
                        Padding = new Vector2(0f, 2f),
                        Height = 1f,
                    };

                    verticalScroll = new ScrollBar(textBox)
                    {
                        ParentAlignment = ParentAlignments.Right,
                        DimAlignment = DimAlignments.Height | DimAlignments.IgnorePadding,
                        Vertical = true,
                    };

                    scrollDivider = new TexturedBox(verticalScroll)
                    {
                        Color = new Color(53, 66, 75),
                        ParentAlignment = ParentAlignments.Left | ParentAlignments.InnerH,
                        DimAlignment = DimAlignments.Height,
                        Padding = new Vector2(2f, 0f),
                        Width = 1f,
                    };

                    HeaderText = "Text Page Header";
                    SubHeaderText = "Subheading\nLine 1\nLine 2\nLine 3\nLine 4";

                    UseCursor = true;
                    ShareCursor = true;
                }

                protected override void Layout()
                {
                    ITextBoard textBoard = textBox.TextBoard;

                    SliderBar slider = verticalScroll.slide;
                    slider.BarColor = TerminalFormatting.OuterSpace.SetAlphaPct(HudMain.UiBkOpacity);
                    slider.SliderHeight = (textBoard.Size.Y / textBoard.TextSize.Y) * verticalScroll.Height;

                    textBox.Height = Height - header.Height - subheader.Height - Padding.Y;
                    textBox.Width = Width - verticalScroll.Width - Padding.X;

                    header.Width = textBox.Width;
                    subheader.Width = textBox.Width;
                }

                protected override void HandleInput(Vector2 cursorPos)
                {
                    ITextBoard textBoard = textBox.TextBoard;
                    IMouseInput vertControl = verticalScroll.slide.MouseInput;

                    verticalScroll.Max = Math.Max(0f, textBoard.TextSize.Y - textBoard.Size.Y);

                    // Update the ScrollBar positions to represent the current offset unless they're being clicked.
                    if (!vertControl.IsLeftClicked)
                    {
                        if (IsMousedOver || verticalScroll.IsMousedOver)
                        {
                            Vector2I lineRange = textBoard.VisibleLineRange;

                            if (SharedBinds.MousewheelUp.IsPressed || SharedBinds.UpArrow.IsNewPressed || SharedBinds.UpArrow.IsPressedAndHeld)
                                textBoard.MoveToChar(new Vector2I(lineRange.X - 1, 0));
                            else if (SharedBinds.MousewheelDown.IsPressed || SharedBinds.DownArrow.IsNewPressed || SharedBinds.DownArrow.IsPressedAndHeld)
                                textBoard.MoveToChar(new Vector2I(lineRange.Y + 1, 0));
                            else if (SharedBinds.PageUp.IsNewPressed || SharedBinds.PageUp.IsPressedAndHeld)
                            {
                                // A hacky, somewhat inefficient solution, but it works well
                                textBoard.MoveToChar(new Vector2I(0, 0));
                                textBoard.MoveToChar(new Vector2I(lineRange.X - 1, 0));
                            }
                            else if (SharedBinds.PageDown.IsNewPressed || SharedBinds.PageDown.IsPressedAndHeld)
                            {
                                textBoard.MoveToChar(new Vector2I(textBoard.Count - 1, 0));
                                textBoard.MoveToChar(new Vector2I(lineRange.Y + 1, 0));
                            }
                        }

                        verticalScroll.Current = textBoard.TextOffset.Y;
                    }

                    textBoard.TextOffset = new Vector2(0, verticalScroll.Current);
                }
            }
        }
    }
}