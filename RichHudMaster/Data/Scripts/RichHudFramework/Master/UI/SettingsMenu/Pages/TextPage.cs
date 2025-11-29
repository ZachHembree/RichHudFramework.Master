using RichHudFramework.UI.Rendering;
using RichHudFramework.UI.Rendering.Server;
using System;
using System.Collections.Generic;
using System.Text;
using VRage;
using VRageMath;
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

                // Child elements
                private readonly Label header;
                private readonly Label subheader;
                private readonly TextBox textBox;
                private readonly ScrollBar verticalScroll;
                private readonly BindInputElement scrollBinds;

                public ScrollableTextBox(HudParentBase parent = null) : base(parent)
                {
                    // Header label (topmost)
                    header = new Label()
                    {
                        Height = 24f,
                        AutoResize = false,
                        Format = new GlyphFormat(Color.White, TextAlignment.Center)
                    };

                    // Subheader placed below header
                    subheader = new Label()
                    {
                        Height = 20f,
                        Padding = new Vector2(0f, 10f),
                        BuilderMode = TextBuilderModes.Wrapped,
                        AutoResize = false,
                        VertCenterText = false,
                        Format = new GlyphFormat(Color.White, TextAlignment.Center, 0.8f)
                    };

                    // Main scrollable text area
                    textBox = new TextBox()
                    {
                        Padding = new Vector2(8f, 8f),
                        BuilderMode = TextBuilderModes.Wrapped,
                        AutoResize = false,
                        Format = GlyphFormat.White,
                        VertCenterText = false,
                        EnableEditing = false,
                        EnableHighlighting = true,
                        ClearSelectionOnLoseFocus = true
                    };

                    // Horizontal divider below header/subheader area
                    var headerDivider = new TexturedBox(textBox)
                    {
                        Color = new Color(53, 66, 75),
                        ParentAlignment = ParentAlignments.Top,
                        DimAlignment = DimAlignments.Width,
                        Padding = new Vector2(0f, 2f),
                        Height = 1f
                    };

                    var vChain = new HudChain()
                    {
                        AlignVertical = true,
                        SizingMode = HudChainSizingModes.FitMembersOffAxis,
                        CollectionContainer =
                        {
                            header,
                            subheader,
                            { textBox, 1f }
                        }
                    };

                    // Vertical scrollbar
                    verticalScroll = new ScrollBar()
                    {
                        Vertical = true,
                        UpdateValueCallback = UpdateScrollOffset
                    };

                    // Vertical divider between text and scrollbar
                    var scrollDivider = new TexturedBox(verticalScroll)
                    {
                        Color = new Color(53, 66, 75),
                        ParentAlignment = ParentAlignments.InnerLeft,
                        DimAlignment = DimAlignments.Height,
                        Padding = new Vector2(2f, 0f),
                        Width = 1f
                    };

                    // Keyboard/mouse wheel input bindings
                    scrollBinds = new BindInputElement(this)
                    {
                        { SharedBinds.MousewheelUp,   ScrollUp },
                        { SharedBinds.UpArrow,        ScrollUp,   ScrollUp },
                        { SharedBinds.MousewheelDown, ScrollDown },
                        { SharedBinds.DownArrow,      ScrollDown, ScrollDown },
                        { SharedBinds.PageUp,         PageUp,     PageUp },
                        { SharedBinds.PageDown,       PageDown,   PageDown }
                    };

                    var hChain = new HudChain(this)
                    {
                        AlignVertical = false,
                        DimAlignment = DimAlignments.UnpaddedSize,
                        SizingMode = HudChainSizingModes.FitMembersOffAxis,
                        CollectionContainer = { { vChain, 1f }, verticalScroll }
                    };

                    // Default content
                    HeaderText = "Text Page Header";
                    SubHeaderText = "Subheading\nLine 1\nLine 2\nLine 3\nLine 4";

                    UseCursor = true;
                    ShareCursor = true;
                }

                protected override void Layout()
                {
                    ITextBoard board = textBox.TextBoard;

                    // Update scrollbar appearance and thumb size
                    verticalScroll.SlideInput.BarColor = TerminalFormatting.OuterSpace.SetAlphaPct(HudMain.UiBkOpacity);
                    // Update maximum scroll range
                    verticalScroll.Max = Math.Max(0f, board.TextSize.Y - board.Size.Y);
                    // Set scroll offset to current text offset
                    verticalScroll.Current = board.TextOffset.Y;
                    verticalScroll.VisiblePercent = (board.Size.Y / board.TextSize.Y);
                }

                protected override void HandleInput(Vector2 cursorPos)
                {
                    bool scrollbarCaptured = verticalScroll.SlideInput.IsLeftClicked;
                    scrollBinds.InputEnabled = !scrollbarCaptured && (IsMousedOver || verticalScroll.IsMousedOver);
                }

                private void UpdateScrollOffset(object sender, EventArgs args)
                {
                    ITextBoard board = textBox.TextBoard;
                    // Set text offset to current scroll offset
                    board.TextOffset = new Vector2(board.TextOffset.X, verticalScroll.Current);
                }

                private void ScrollUp(object sender, EventArgs args)
                {
                    ITextBoard board = textBox.TextBoard;
                    Vector2I range = board.VisibleLineRange;
                    board.MoveToChar(new Vector2I(range.X - 1, 0));
                }

                private void ScrollDown(object sender, EventArgs args)
                {
                    ITextBoard board = textBox.TextBoard;
                    Vector2I range = board.VisibleLineRange;
                    board.MoveToChar(new Vector2I(range.Y + 1, 0));
                }

                private void PageUp(object sender, EventArgs args)
                {
                    ITextBoard board = textBox.TextBoard;
                    Vector2I range = board.VisibleLineRange;
                    // Jump to top, then move down to the beginning of the visible range
                    board.MoveToChar(new Vector2I(0, 0));
                    board.MoveToChar(new Vector2I(range.X - 1, 0));
                }

                private void PageDown(object sender, EventArgs args)
                {
                    ITextBoard board = textBox.TextBoard;
                    Vector2I range = board.VisibleLineRange;
                    // Jump to bottom, then move up to the end of the visible range
                    board.MoveToChar(new Vector2I(board.Count - 1, 0));
                    board.MoveToChar(new Vector2I(range.Y + 1, 0));
                }
            }
        }
    }
}