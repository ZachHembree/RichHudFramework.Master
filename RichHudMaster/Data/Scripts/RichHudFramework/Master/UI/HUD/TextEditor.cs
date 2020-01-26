using VRageMath;
using System;
using RichHudFramework.UI.Rendering;

namespace RichHudFramework.UI
{
    using Rendering.Client;
    using Rendering.Server;

    /// <summary>
    /// Simple text editor used for debugging TextBoard
    /// </summary>
    public class TextEditor : WindowBase
    {
        public readonly TextBox textBox;

        private readonly HudChain<HudElementBase> toolbar;
        private readonly EditorDropdown<int> fontList;
        private readonly EditorDropdown<float> sizeList;
        private readonly EditorDropdown<TextBuilderModes> textBuilderModes;
        private readonly ScrollBar scrollVert, scrollHorz;

        private static readonly float[] textSizes = new float[] { .75f, .875f, 1f, 1.125f, 1.25f, 1.375f, 1.5f };

        public TextEditor(IHudParent parent = null) : base(parent)
        {
            textBox = new TextBox(body)
            {
                ParentAlignment = ParentAlignments.Bottom | ParentAlignments.Left | ParentAlignments.Inner,
                Padding = new Vector2(8f, 8f),
                Format = new GlyphFormat(Color.White, textSize: 1.1f),
                VertCenterText = false,
                AutoResize = false
            };

            scrollVert = new ScrollBar(textBox)
            {
                Width = 26f,
                Padding = new Vector2(4f),
                DimAlignment = DimAlignments.Height | DimAlignments.IgnorePadding,
                ParentAlignment = ParentAlignments.Right,
            };

            scrollHorz = new ScrollBar(textBox)
            {
                Height = 26f,
                Padding = new Vector2(4f),
                DimAlignment = DimAlignments.Width | DimAlignments.IgnorePadding,
                ParentAlignment = ParentAlignments.Bottom,
                Vertical = false,
            };

            scrollHorz.slide.Reverse = true;

            fontList = new EditorDropdown<int>()
            {
                Height = 24f,
                Width = 140f,
                Format = GlyphFormat.White,
            };

            foreach (IFontMin font in FontManager.Fonts)
                fontList.Add(new RichText(font.Name, new GlyphFormat(Color.White, fontStyle: font.Regular)), font.Index);

            fontList.SetSelection(0);
            fontList.OnSelectionChanged += UpdateFont;

            sizeList = new EditorDropdown<float>()
            {
                Height = 24f,
                Width = 60f,
                Format = GlyphFormat.White,
            };

            for (int n = 0; n < textSizes.Length; n++)
                sizeList.Add(textSizes[n].ToString(), textSizes[n]);

            sizeList.SetSelection(2);
            sizeList.OnSelectionChanged += UpdateFontSize;

            textBuilderModes = new EditorDropdown<TextBuilderModes>()
            {
                Height = 24f,
                Width = 140f,
                Format = GlyphFormat.White,
            };

            textBuilderModes.Add("Unlined", TextBuilderModes.Unlined);
            textBuilderModes.Add("Lined", TextBuilderModes.Lined);
            textBuilderModes.Add("Wrapped", TextBuilderModes.Wrapped);

            textBuilderModes.SetSelection(TextBuilderModes.Unlined);
            textBuilderModes.OnSelectionChanged += UpdateBuilderMode;

            IFontMin abhaya = FontManager.GetFont("AbhayaLibreMedium");
            GlyphFormat buttonFormat = new GlyphFormat(Color.White, TextAlignment.Center, 1.1625f, abhaya.Regular);

            TextBoxButton
                bold = new TextBoxButton()
                {
                    Format = buttonFormat,
                    Text = "B",
                    AutoResize = false,
                    VertCenterText = true,
                    Size = new Vector2(32f, 30f),
                    Color = new Color(41, 54, 62),
                },
                italic = new TextBoxButton()
                {
                    Format = buttonFormat,
                    Text = "I",
                    AutoResize = false,
                    VertCenterText = true,
                    Size = new Vector2(32f, 30f),
                    Color = new Color(41, 54, 62),
                };

            toolbar = new HudChain<HudElementBase>(header)
            {
                Height = 26f,
                ParentAlignment = ParentAlignments.Bottom | ParentAlignments.Left | ParentAlignments.InnerH,
                AutoResize = true,
                AlignVertical = false,
                ChildContainer =
                {
                    fontList,
                    sizeList,
                    bold,
                    italic,
                    textBuilderModes,
                }
            };

            bold.MouseInput.OnLeftClick += ToggleBold;
            italic.MouseInput.OnLeftClick += ToggleItalic;

            BodyColor = new Color(41, 54, 62, 150);
            BorderColor = new Color(58, 68, 77);

            Header.Format = new GlyphFormat(GlyphFormat.Blueish.Color, TextAlignment.Center, 1.08f);
            header.Height = 30f;
        }

        protected override void HandleInput()
        {
            base.HandleInput();

            ITextBoard textBoard = textBox.TextBoard;
            IClickableElement horzControl = scrollHorz.slide.mouseInput,
                vertControl = scrollVert.slide.mouseInput;

            scrollHorz.Min = -Math.Max(0f, textBoard.TextSize.X - textBoard.Size.X);
            scrollVert.Max = Math.Max(0f, textBoard.TextSize.Y - textBoard.Size.Y);

            if (!horzControl.IsLeftClicked)
                scrollHorz.Current = textBoard.TextOffset.X;

            if (!vertControl.IsLeftClicked)
                scrollVert.Current = textBoard.TextOffset.Y;

            textBoard.TextOffset = new Vector2(scrollHorz.Current, scrollVert.Current);
        }

        protected override void Draw()
        {
            ITextBoard textBoard = textBox.TextBoard;

            scrollHorz.slide.slider.Width = (textBoard.Size.X / textBoard.TextSize.X) * scrollHorz.Width;
            scrollVert.slide.slider.Height = (textBoard.Size.Y / textBoard.TextSize.Y) * scrollVert.Height;

            textBox.Offset = new Vector2(0f, scrollHorz.Height);
            textBox.Width = Width - scrollVert.Width;
            textBox.Height = Height - header.Height - toolbar.Height - scrollHorz.Height;
        }

        protected void UpdateFont()
        {
            GlyphFormat format = textBox.Format;
            Vector2I current = format.StyleIndex;
            int index = fontList.Selection.AssocMember;

            if (FontManager.Fonts[index].IsStyleDefined(current.Y))
                textBox.Format = format.WithFont(new Vector2I(index, current.Y));
            else
                textBox.Format = format.WithFont(new Vector2I(index, 0));
        }

        private void UpdateFontSize()
        {
            textBox.Format = textBox.Format.WithSize(sizeList.Selection.AssocMember);
        }

        private void UpdateBuilderMode()
        {
            textBox.BuilderMode = textBuilderModes.Selection.AssocMember;
        }

        protected void ToggleBold()
        {
            GlyphFormat format = textBox.Format;
            Vector2I index = format.StyleIndex;
            int bold = (int)FontStyleEnum.Bold;

            if ((format.StyleIndex.Y & bold) == bold)
                index.Y -= bold;  
            else
                index.Y |= bold;

            if (FontManager.Fonts[index.X].IsStyleDefined(index.Y))
                textBox.Format = format.WithFont(index);
        }

        protected void ToggleItalic()
        {
            GlyphFormat format = textBox.Format;
            Vector2I index = format.StyleIndex;

            int italic = (int)FontStyleEnum.Italic;

            if ((format.StyleIndex.Y & italic) == italic)
                index.Y -= italic;
            else
                index.Y |= italic;

            if (FontManager.Fonts[index.X].IsStyleDefined(index.Y))
                textBox.Format = format.WithFont(index);
        }

        private class EditorDropdown<T> : Dropdown<T>
        {
            public EditorDropdown(IHudParent parent = null) : base(parent)
            {
                ScrollBar scrollBar = list.scrollBox.scrollBar;

                scrollBar.Padding = new Vector2(12f, 8f);
                scrollBar.Width = 20f;

                display.divider.Padding = new Vector2(4f, 8f);
                display.arrow.Width = 22f;
            }
        }
    }
}