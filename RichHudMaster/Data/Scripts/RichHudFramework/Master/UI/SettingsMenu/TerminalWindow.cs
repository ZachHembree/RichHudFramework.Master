using RichHudFramework.Server;
using RichHudFramework.Internal;
using RichHudFramework.UI.Rendering;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage;
using VRageMath;
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
                private readonly HudChain layout;
                private readonly TexturedBox topDivider, middleDivider, bottomDivider;
                private readonly Button closeButton;
                private readonly LabelBox warningBox;
                private TerminalPageBase lastPage;
                private static readonly Material closeButtonMat = new Material("RichHudCloseButton", new Vector2(32f));

                public TerminalWindow(HudParentBase parent = null) : base(parent)
                {
                    Header.Format = TerminalFormatting.HeaderFormat;
                    Header.SetText("Rich HUD Terminal");

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
                        Width = 250f
                    };

                    middleDivider = new TexturedBox()
                    {
                        Padding = new Vector2(24f, 0f),
                        Width = 26f,
                    };

                    layout = new HudChain(false, topDivider)
                    {
                        SizingMode = HudChainSizingModes.FitMembersOffAxis | HudChainSizingModes.ClampChainBoth,
                        ParentAlignment = ParentAlignments.Bottom | ParentAlignments.Left | ParentAlignments.InnerH,
                        Padding = new Vector2(80f, 40f),
                        Spacing = 12f,
                        ChainContainer = { modList, middleDivider },
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
                        highlightColor = Color.White,
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

                    modList.OnSelectionChanged += HandleSelectionChange;
                    closeButton.MouseInput.OnLeftClick += (sender, args) => CloseMenu();
                    SharedBinds.Escape.OnNewPress += CloseMenu;
                    MasterBinds.ToggleTerminal.OnNewPress += ToggleMenu;

                    BodyColor = new Color(37, 46, 53);
                    BorderColor = new Color(84, 98, 107);

                    Padding = new Vector2(80f, 40f);
                    MinimumSize = new Vector2(1024f, 500f);

                    Size = new Vector2(1024f, 850f);
                    Vector2 screenSize = new Vector2(HudMain.ScreenWidth, HudMain.ScreenHeight);

                    if (screenSize.Y < 1080 || HudMain.AspectRatio < (16f/9f))
                        Height = MinimumSize.Y;

                    Offset = (screenSize - Size) / 2f - new Vector2(40f);
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
                    LocalScale = HudMain.ResScale;

                    base.Layout();

                    if (CurrentPage != null)
                        CurrentPage.Element.Width = Width - Padding.X - modList.Width - layout.Spacing;

                    layout.Height = Height - header.Height - topDivider.Height - Padding.Y - bottomDivider.Height;
                    modList.Width = 250f * Scale;

                    BodyColor = BodyColor.SetAlphaPct(HudMain.UiBkOpacity);
                    header.Color = BodyColor;

                    warningBox.Visible = !HudMain.Cursor.Visible;
                }

                private void HandleSelectionChange()
                {
                    if (lastPage != null)
                        layout.Remove(lastPage, true);

                    if (CurrentPage != null)
                        layout.Add(CurrentPage);

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
                    public event Action OnSelectionChanged;

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
                    public IReadOnlyList<ModControlRoot> ModRoots => scrollBox.ChainEntries;

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
                            Color = TerminalFormatting.ListBgColor,
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
                        modSettings.OnSelectionChanged += SelectionHandler;

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
                            for (int n = 0; n < scrollBox.ChainEntries.Count; n++)
                            {
                                if (scrollBox.ChainEntries[n] != SelectedMod)
                                    scrollBox.ChainEntries[n].Element.ClearSelection();
                            }

                            CurrentPage = newPage;
                            SelectedMod.SetSelection(newPage);
                            OnSelectionChanged?.Invoke();
                        }
                    }

                    protected override void Layout()
                    {
                        header.Color = TerminalFormatting.ListHeaderColor.SetAlphaPct(HudMain.UiBkOpacity);
                        scrollBox.Color = TerminalFormatting.ListBgColor.SetAlphaPct(HudMain.UiBkOpacity);

                        SliderBar slider = scrollBox.scrollBar.slide;
                        slider.BarColor = TerminalFormatting.ScrollBarColor.SetAlphaPct(HudMain.UiBkOpacity);
                    }
                }
            }
        }
    }
}