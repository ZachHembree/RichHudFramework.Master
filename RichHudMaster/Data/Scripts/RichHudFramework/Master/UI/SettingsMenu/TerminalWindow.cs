using RichHudFramework.Server;
using RichHudFramework.Internal;
using RichHudFramework.UI.Rendering;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage;
using VRageMath;
using VRage.Game.ModAPI;
using ApiMemberAccessor = System.Func<object, int, object>;

namespace RichHudFramework
{
    namespace UI.Server
    {
        public sealed partial class RichHudTerminal
        {
            /// <summary>
            /// Settings menu main window.
            /// </summary>
            private class TerminalWindow : Window
            {
                public override Color BorderColor
                {
                    set
                    {
                        base.BorderColor = value;
                        topDivider.Color = value;
                        bottomDivider.Color = value;
                        bodyDivider.Color = value;
                    }
                }

                /// <summary>
                /// Currently selected mod root
                /// </summary>
                public ModControlRoot SelectedModRoot => modList.SelectedModRoot;

                /// <summary>
                /// Currently selected control page.
                /// </summary>
                public TerminalPageBase SelectedPage => modList.SelectedPage;

                /// <summary>
                /// Read only collection of mod roots registered with the terminal
                /// </summary>
                public IReadOnlyList<ModControlRoot> ModRoots => modList.ModRoots;

                private readonly ModList modList;
                private readonly HudChain pageChain;
                private readonly TexturedBox topDivider, bodyDivider, bottomDivider;
                private readonly Button closeButton;
                private TerminalPageBase lastPage;
                private static readonly Material closeButtonMat = new Material("RichHudCloseButton", new Vector2(32f));

                public TerminalWindow(HudParentBase parent = null) : base(parent)
                {
                    HeaderBuilder.Format = TerminalFormatting.HeaderFormat;
                    HeaderBuilder.SetText("Rich HUD Terminal");
                    header.background.Visible = false;
                    header.Height = 60f;

                    topDivider = new TexturedBox(header)
                    {
                        ParentAlignment = ParentAlignments.Bottom,
                        DimAlignment = DimAlignments.Width,
                        Padding = new Vector2(80f, 0f),
                        Height = 1f,
                    };

                    modList = new ModList()
                    {
                        Width = 270f
                    };

                    bodyDivider = new TexturedBox()
                    {
                        Padding = new Vector2(24f, 0f),
                        Width = 26f,
                    };

                    pageChain = new HudChain(false, body)
                    {
                        DimAlignment = DimAlignments.Size,
                        Padding = new Vector2(40f, 80f),
                        Offset = new Vector2(20f, 20f),
                        Spacing = 12f,
                        SizingMode = HudChainSizingModes.FitMembersOffAxis,
                        CollectionContainer = { modList, bodyDivider },
                    };

                    bottomDivider = new TexturedBox(body)
                    {
                        ParentAlignment = ParentAlignments.InnerBottom,
                        DimAlignment = DimAlignments.UnpaddedWidth,
                        Height = 1f,
                        Padding = new Vector2(80f, 0f),
                        Offset = new Vector2(0f, 40f)
                    };

                    closeButton = new Button(header)
                    {
                        Material = closeButtonMat,
                        HighlightColor = Color.White,
                        ParentAlignment = ParentAlignments.InnerTopRight,
                        Size = new Vector2(30f),
                        Offset = new Vector2(-18f, -14f),
                        Color = new Color(173, 182, 189),
                    };

                    modList.SelectionChanged += HandleSelectionChange;
                    closeButton.MouseInput.LeftClicked += (sender, args) => CloseMenu();
                    SharedBinds.Escape.NewPressed += (sender, args) => CloseMenu();
                    MasterBinds.ToggleTerminalOld.NewPressed += (sender, args) => { if(MyAPIGateway.Gui.ChatEntryVisible) ToggleMenu(); };
                    MasterBinds.ToggleTerminal.NewPressed += (sender, args) => ToggleMenu();

                    BodyColor = new Color(37, 46, 53);
                    BorderColor = new Color(84, 98, 107);

                    MinimumSize = new Vector2(1044f, 500f);
                    Size = new Vector2(1044f, 850f);

                    Vector2 normScreenSize = new Vector2(HudMain.ScreenWidth, HudMain.ScreenHeight) / HudMain.ResScale;

                    if (normScreenSize.Y < 1080 || HudMain.AspectRatio < (16f / 9f))
                        Height = MinimumSize.Y;

                    Offset = (normScreenSize - Size) * .5f - new Vector2(40f);
                }

                /// <summary>
                /// Creates and returns a new control root with the name given.
                /// </summary>
                public ModControlRoot AddModRoot(string clientName) =>
                    modList.AddModRoot(clientName);

                /// <summary>
                /// Opens the given terminal page
                /// </summary>
                public void SetSelection(ModControlRoot modRoot, TerminalPageBase newPage) =>
                    modList.SetSelection(modRoot, newPage);

                /// <summary>
                /// Toggles menu visiblity
                /// </summary>
                public void ToggleMenu()
                {
                    if (!SharedBinds.Alt.IsPressed)
                    {
                        if (!Visible)
                            OpenMenu();
                        else
                            CloseMenu();
                    }
                }

                /// <summary>
                /// Opens the window if chat is visible
                /// </summary>
                public void OpenMenu()
                {
                    if (!Visible)
                    {
                        Visible = true;
                        HudMain.EnableCursor = true;
                        GetFocus();
                    }
                }

                /// <summary>
                /// Closes the window
                /// </summary>
                public void CloseMenu()
                {
                    if (Visible)
                    {
                        Visible = false;
                        HudMain.EnableCursor = false;
                    }
                }

                protected override void Layout()
                {
                    base.Layout();

                    // Update color opacity
                    BodyColor = BodyColor.SetAlphaPct(HudMain.UiBkOpacity);
                }

                protected override void HandleInput(Vector2 cursorPos)
                {
                    if (MyAPIGateway.Gui.IsCursorVisible)
                        CloseMenu();

                    base.HandleInput(cursorPos);

                    // Bound window offset to keep it from being moved off screen
                    Vector2 min = new Vector2(HudMain.ScreenWidth, HudMain.ScreenHeight) / (HudMain.ResScale * -2f), max = -min;
                    Offset = Vector2.Clamp(Offset, min, max);
                }

                private void HandleSelectionChange()
                {
                    if (lastPage != null)
                    {
                        var pageElement = lastPage.AssocMember as HudElementBase;
                        int index = pageChain.FindIndex(x => x.Element == pageElement);

                        pageChain.RemoveAt(index);
                    }

                    if (SelectedPage != null)
                        pageChain.Add(SelectedPage.AssocMember as HudElementBase, 1f);

                    lastPage = SelectedPage;
                }

                /// <summary>
                /// Scrollable list of mod control roots.
                /// </summary>
                private class ModList : HudElementBase
                {
                    /// <summary>
                    /// Invoked whenever the page selection changes
                    /// </summary>
                    public event Action SelectionChanged;

                    /// <summary>
                    /// Currently selected mod root
                    /// </summary>
                    public ModControlRoot SelectedModRoot { get; private set; }

                    /// <summary>
                    /// Currently selected subcategory
                    /// </summary>
                    public TerminalPageCategoryBase SelectedSubcategory { get; private set; }

                    /// <summary>
                    /// Currently selected control page.
                    /// </summary>
                    public TerminalPageBase SelectedPage { get; private set; }

                    /// <summary>
                    /// Returns a read only list of mod root containers registered to the list
                    /// </summary>
                    public IReadOnlyList<ModControlRoot> ModRoots => scrollBox.Collection;

                    private readonly LabelBox header;
                    private readonly ScrollBox<ModControlRoot, LabelElementBase> scrollBox;
                    private readonly ListInputElement<ModControlRoot, LabelElementBase> listInput;

                    public ModList(HudParentBase parent = null) : base(parent)
                    {
                        header = new LabelBox()
                        {
                            AutoResize = false,
                            Size = new Vector2(200f, 36f),
                            Color = new Color(32, 39, 45),
                            Format = TerminalFormatting.ControlFormat,
                            Text = "Mod List:",
                            TextPadding = new Vector2(30f, 0f),
                        };

                        scrollBox = new ScrollBox<ModControlRoot, LabelElementBase>(true)
                        {
                            SizingMode = HudChainSizingModes.FitMembersOffAxis,
                            Color = TerminalFormatting.DarkSlateGrey,
                            Padding = new Vector2(6f)
                        };

                        var layout = new HudChain(true, this)
                        {
                            DimAlignment = DimAlignments.UnpaddedSize,
                            SizingMode = HudChainSizingModes.FitMembersOffAxis,
                            CollectionContainer = { header, { scrollBox, 1f } }
                        };

                        listInput = new ListInputElement<ModControlRoot, LabelElementBase>(scrollBox);

                        var listDivider = new TexturedBox(header)
                        {
                            ParentAlignment = ParentAlignments.Bottom,
                            DimAlignment = DimAlignments.Width,
                            Height = 1f,
                            Color = new Color(53, 66, 75),
                        };

                        var listBorder = new BorderBox(this)
                        {
                            DimAlignment = DimAlignments.Size,
                            Thickness = 1f,
                            Color = new Color(53, 66, 75),
                        };
                    }

                    protected override void HandleInput(Vector2 cursorPos)
                    {
                        var nextRoot = listInput.Selection;
                        var nextCategory = nextRoot?.SelectedSubcategory;
                        var nextPage = nextRoot?.SelectedPage;

                        if (nextPage != null && nextPage != SelectedPage)
                        {
                            SelectedModRoot = nextRoot;
                            SelectedSubcategory = nextCategory;
                            SelectedPage = nextPage;

                            foreach (ModControlRoot root in scrollBox.Collection)
                            {
                                if (root != SelectedModRoot)
                                    root.ClearSelection();

                                foreach (TerminalPageCategoryBase category in root.Subcategories)
                                {
                                    if (category != SelectedSubcategory)
                                        category.ClearSelection();
                                }
                            }

                            SelectionChanged?.Invoke();
                            SelectedModRoot?.OnSelectionChanged(SelectedModRoot, EventArgs.Empty);
                        }

                        Vector2 listSize = scrollBox.Size,
                            listPos = scrollBox.Position;

                        listSize.X -= scrollBox.ScrollBar.Width;
                        listPos.X -= scrollBox.ScrollBar.Width;

                        listInput.ListRange = scrollBox.ClipRange;
                        listInput.ListPos = listPos;
                        listInput.ListSize = listSize;
                    }

                    protected override void Layout()
                    {
                        header.Color = TerminalFormatting.Dark.SetAlphaPct(HudMain.UiBkOpacity);
                        scrollBox.Color = TerminalFormatting.DarkSlateGrey.SetAlphaPct(HudMain.UiBkOpacity);

                        SliderBar slider = scrollBox.ScrollBar.slide;
                        slider.BarColor = TerminalFormatting.OuterSpace.SetAlphaPct(HudMain.UiBkOpacity);
                    }

                    /// <summary>
                    /// Creates and returns a new control root with the name given.
                    /// </summary>
                    public ModControlRoot AddModRoot(string clientName)
                    {
                        ModControlRoot modSettings = new ModControlRoot() { Name = clientName };
                        scrollBox.Add(modSettings);

                        return modSettings;
                    }

                    /// <summary>
                    /// Opens to the given terminal page
                    /// </summary>
                    public void SetSelection(ModControlRoot modRoot, TerminalPageBase newPage)
                    {
                        listInput.SetSelection(modRoot);

                        if (SelectedPage != newPage)
                        {
                            TerminalPageCategoryBase subcategory = null;
                            bool contains = false;

                            foreach (TerminalPageBase page in modRoot.Pages)
                            {
                                if (page == newPage)
                                {
                                    contains = true;
                                    break;
                                }
                            }

                            if (!contains)
                            {
                                foreach (TerminalPageCategoryBase cat in modRoot.Subcategories)
                                {
                                    foreach (TerminalPageBase page in cat.Pages)
                                    {
                                        if (page == newPage)
                                        {
                                            subcategory = cat;
                                            contains = true;
                                            break;
                                        }
                                    }
                                }
                            }

                            if (subcategory != null)
                            {
                                modRoot.SetSelection(subcategory);
                                subcategory.SetSelection(newPage);
                            }
                            else
                                modRoot.SetSelection(newPage);
                        }
                    }

                    /// <summary>
                    /// Clears the current page selection
                    /// </summary>
                    public void ClearSelection() =>
                        listInput.ClearSelection();
                }
            }
        }
    }
}