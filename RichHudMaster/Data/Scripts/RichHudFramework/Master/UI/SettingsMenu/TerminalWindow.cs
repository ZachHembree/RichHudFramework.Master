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
            private class TerminalWindow : WindowBase
            {
                public override Color BorderColor
                {
                    set
                    {
                        base.BorderColor = value;
                        topDivider.Color = value;
                        bottomDivider.Color = value;
                        middleDivider.Color = value;
                    }
                }

                /// <summary>
                /// Currently selected mod root
                /// </summary>
                public ModControlRoot SelectedMod => modList.SelectedMod;

                /// <summary>
                /// Currently selected control page.
                /// </summary>
                public TerminalPageBase CurrentPage => modList.CurrentPage;

                /// <summary>
                /// Read only collection of mod roots registered with the terminal
                /// </summary>
                public IReadOnlyList<ModControlRoot> ModRoots => modList.ModRoots;

                private readonly ModList modList;
                private readonly HudChain bodyChain;
                private readonly TexturedBox topDivider, middleDivider, bottomDivider;
                private readonly Button closeButton;
                private readonly LabelBox warningBox;
                private TerminalPageBase lastPage;
                private static readonly Material closeButtonMat = new Material("RichHudCloseButton", new Vector2(32f));

                public TerminalWindow(HudParentBase parent = null) : base(parent)
                {
                    HeaderBuilder.Format = TerminalFormatting.HeaderFormat;
                    HeaderBuilder.SetText("Rich HUD Terminal");

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

                    middleDivider = new TexturedBox()
                    {
                        Padding = new Vector2(24f, 0f),
                        Width = 26f,
                    };

                    bodyChain = new HudChain(false, topDivider)
                    {
                        SizingMode = HudChainSizingModes.FitMembersOffAxis | HudChainSizingModes.ClampChainBoth,
                        ParentAlignment = ParentAlignments.Bottom | ParentAlignments.Left | ParentAlignments.InnerH,
                        Padding = new Vector2(80f, 40f),
                        Spacing = 12f,
                        CollectionContainer = { modList, middleDivider },
                    };

                    bottomDivider = new TexturedBox(this)
                    {
                        ParentAlignment = ParentAlignments.Bottom | ParentAlignments.InnerV,
                        DimAlignment = DimAlignments.Width,
                        Offset = new Vector2(0f, 40f),
                        Padding = new Vector2(80f, 0f),
                        Height = 1f,
                    };

                    closeButton = new Button(header)
                    {
                        Material = closeButtonMat,
                        HighlightColor = Color.White,
                        ParentAlignment = ParentAlignments.Top | ParentAlignments.Right | ParentAlignments.Inner,
                        Size = new Vector2(30f),
                        Offset = new Vector2(-18f, -14f),
                        Color = new Color(173, 182, 189),
                    };

                    warningBox = new LabelBox(this)
                    {
                        Height = 30f,
                        AutoResize = false,
                        ParentAlignment = ParentAlignments.Bottom,
                        DimAlignment = DimAlignments.Width,
                        TextPadding = new Vector2(30f, 0f),
                        Color = new Color(126, 39, 44),
                        Format = new GlyphFormat(Color.White, textSize: .8f),
                        Text = "Input disabled. Open chat to enable cursor.",
                    };

                    var warningBorder = new BorderBox(warningBox)
                    {
                        DimAlignment = DimAlignments.Both,
                        Color = new Color(156, 65, 74)
                    };

                    modList.SelectionChanged += HandleSelectionChange;
                    closeButton.MouseInput.LeftClicked += (sender, args) => CloseMenu();
                    SharedBinds.Escape.NewPressed += CloseMenu;
                    MasterBinds.ToggleTerminal.NewPressed += ToggleMenu;

                    BodyColor = new Color(37, 46, 53);
                    BorderColor = new Color(84, 98, 107);

                    Padding = new Vector2(80f, 40f);
                    MinimumSize = new Vector2(1044f, 500f);

                    Size = new Vector2(1044f, 850f);
                    Vector2 normScreenSize = new Vector2(HudMain.ScreenWidth, HudMain.ScreenHeight) / HudMain.ResScale;

                    if (normScreenSize.Y < 1080 || HudMain.AspectRatio < (16f/9f))
                        Height = MinimumSize.Y;

                    Offset = (normScreenSize - Size) / 2f - new Vector2(40f);
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
                    if (!Visible)
                        OpenMenu();
                    else
                        CloseMenu();
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
                    if (MyAPIGateway.Gui.IsCursorVisible)
                        CloseMenu();

                    LocalScale = HudMain.ResScale;

                    base.Layout();

                    // Update sizing
                    if (CurrentPage != null)
                        CurrentPage.Element.Width = Width - Padding.X - modList.Width - bodyChain.Spacing;

                    bodyChain.Height = Height - header.Height - topDivider.Height - Padding.Y - bottomDivider.Height;
                    modList.Width = 270f * Scale;

                    // Bound window offset to keep it from being moved off screen
                    Vector2 min = new Vector2(HudMain.ScreenWidth, HudMain.ScreenHeight) / -2f, max = -min;
                    Offset = Vector2.Clamp(Offset, min, max);

                    // Update color opacity
                    BodyColor = BodyColor.SetAlphaPct(HudMain.UiBkOpacity);
                    header.Color = BodyColor;

                    // Display warning if cursor is disabled
                    warningBox.Visible = !HudMain.Cursor.Visible;

                    // Update enabled/disabled pages
                    IReadOnlyList<ModControlRoot> rootList = modList.ModRoots;

                    for (int i = 0; i < rootList.Count; i++)
                    {
                        IReadOnlyList<ListBoxEntry<TerminalPageBase>> treePages = rootList[i].ListEntries;

                        for (int j = 0; j < treePages.Count; j++)
                        {
                            ListBoxEntry<TerminalPageBase> entry = treePages[j];
                            TerminalPageBase page = entry.AssocMember;
                            entry.Enabled = page.Enabled;
                            entry.Element.Visible = entry.Enabled;

                            if (!page.Enabled && CurrentPage == page)
                                modList.ClearSelection();
                        }
                    }
                }

                private void HandleSelectionChange()
                {
                    if (lastPage != null)
                        bodyChain.Remove(lastPage, true);

                    if (CurrentPage != null)
                        bodyChain.Add(CurrentPage);

                    lastPage = CurrentPage;
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
                    public ModControlRoot SelectedMod { get; private set; }

                    /// <summary>
                    /// Currently selected control page.
                    /// </summary>
                    public TerminalPageBase CurrentPage { get; private set; }

                    /// <summary>
                    /// Returns a read only list of mod root containers registered to the list
                    /// </summary>
                    public IReadOnlyList<ModControlRoot> ModRoots => scrollBox.Collection;

                    public override float Width
                    {
                        get { return scrollBox.Width; }
                        set
                        {
                            header.Width = value;
                            scrollBox.Width = value;
                        }
                    }

                    public override float Height
                    {
                        get { return scrollBox.Height + header.Height; }
                        set { scrollBox.Height = value - header.Height; }
                    }

                    private readonly LabelBox header;
                    private readonly ScrollBox<ModControlRoot, ModControlRootTreeBox> scrollBox;
                    private readonly EventHandler SelectionHandler;

                    public ModList(HudParentBase parent = null) : base(parent)
                    {
                        scrollBox = new ScrollBox<ModControlRoot, ModControlRootTreeBox>(true, this)
                        {
                            SizingMode = HudChainSizingModes.FitMembersOffAxis | HudChainSizingModes.ClampChainBoth,
                            ParentAlignment = ParentAlignments.Bottom | ParentAlignments.InnerV,
                            Color = TerminalFormatting.DarkSlateGrey,
                            Padding = new Vector2(6f)
                        };

                        header = new LabelBox(scrollBox)
                        {
                            AutoResize = false,
                            ParentAlignment = ParentAlignments.Top,
                            DimAlignment = DimAlignments.Width,
                            Size = new Vector2(200f, 36f),
                            Color = new Color(32, 39, 45),
                            Format = TerminalFormatting.ControlFormat,
                            Text = "Mod List:",
                            TextPadding = new Vector2(30f, 0f),
                        };

                        var listDivider = new TexturedBox(header)
                        {
                            ParentAlignment = ParentAlignments.Bottom,
                            DimAlignment = DimAlignments.Width,
                            Height = 1f,
                            Color = new Color(53, 66, 75),
                        };

                        var listBorder = new BorderBox(this)
                        {
                            DimAlignment = DimAlignments.Both,
                            Thickness = 1f,
                            Color = new Color(53, 66, 75),
                        };

                        SelectionHandler = UpdateSelection;
                    }

                    private void UpdateSelection(object sender, EventArgs args)
                    {
                        var modRoot = sender as ModControlRoot;
                        var newPage = modRoot?.Selection as TerminalPageBase;

                        SetSelection(modRoot, newPage);
                    }

                    /// <summary>
                    /// Creates and returns a new control root with the name given.
                    /// </summary>
                    public ModControlRoot AddModRoot(string clientName)
                    {
                        ModControlRoot modSettings = new ModControlRoot() { Name = clientName };

                        scrollBox.Add(modSettings);
                        modSettings.SelectionChanged += SelectionHandler;

                        return modSettings;
                    }

                    /// <summary>
                    /// Opens the given terminal page
                    /// </summary>
                    public void SetSelection(ModControlRoot modRoot, TerminalPageBase newPage)
                    {
                        SelectedMod = modRoot;

                        if (CurrentPage != newPage)
                        {
                            for (int n = 0; n < scrollBox.Collection.Count; n++)
                            {
                                if (scrollBox.Collection[n] != SelectedMod)
                                    scrollBox.Collection[n].Element.ClearSelection();
                            }

                            CurrentPage = newPage;
                            SelectedMod?.SetSelection(newPage);
                            SelectionChanged?.Invoke();
                        }
                    }

                    /// <summary>
                    /// Clears the current page selection
                    /// </summary>
                    public void ClearSelection() =>
                        SetSelection(null, null);

                    protected override void Layout()
                    {
                        header.Color = TerminalFormatting.Dark.SetAlphaPct(HudMain.UiBkOpacity);
                        scrollBox.Color = TerminalFormatting.DarkSlateGrey.SetAlphaPct(HudMain.UiBkOpacity);

                        SliderBar slider = scrollBox.ScrollBar.slide;
                        slider.BarColor = TerminalFormatting.OuterSpace.SetAlphaPct(HudMain.UiBkOpacity);
                    }
                }
            }
        }
    }
}