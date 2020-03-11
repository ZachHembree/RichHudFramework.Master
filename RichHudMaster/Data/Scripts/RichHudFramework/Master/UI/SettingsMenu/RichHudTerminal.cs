using RichHudFramework.Game;
using RichHudFramework.Server;
using RichHudFramework.UI.Rendering;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage;
using VRageMath;
using ApiMemberAccessor = System.Func<object, int, object>;
using ControlMembers = VRage.MyTuple<
    System.Func<object, int, object>, // GetOrSetMember
    object // ID
>;

namespace RichHudFramework
{
    using ControlContainerMembers = MyTuple<
        ApiMemberAccessor, // GetOrSetMember,
        MyTuple<object, Func<int>>, // Member List
        object // ID
    >;

    namespace UI.Server
    {
        using SettingsMenuMembers = MyTuple<
            ApiMemberAccessor, // GetOrSetMembers
            ControlContainerMembers, // MenuRoot
            Func<int, ControlMembers>, // GetNewControl
            Func<int, ControlContainerMembers>, // GetNewContainer
            Func<int, ControlMembers> // GetNewModPage
        >;

        /// <summary>
        /// Windowed settings menu shared by mods using the framework.
        /// </summary>
        public sealed partial class RichHudTerminal : RichHudComponentBase
        {
            public static readonly GlyphFormat 
                HeaderFormat = new GlyphFormat(Color.White, TextAlignment.Center, 1.15f),
                ControlFormat = GlyphFormat.Blueish.WithSize(1.08f),
                WarningFormat = new GlyphFormat(new Color(200, 55, 55));

            public static readonly Color
                ScrollBarColor = new Color(41, 51, 61),
                TileColor = new Color(39, 50, 57),
                ListHeaderColor = new Color(32, 39, 45),
                ListBgColor = new Color(41, 54, 62),
                BorderColor = new Color(53, 66, 75),
                SelectionBgColor = new Color(34, 44, 53),
                HighlightOverlayColor = new Color(255, 255, 255, 40),
                HighlightColor = new Color(214, 213, 218),
                AccentHighlightColor = new Color(181, 185, 190);

            private static RichHudTerminal Instance
            {
                get { Init(); return instance; }
                set { instance = value; }
            }

            /// <summary>
            /// Determines whether or not the terminal is currently open.
            /// </summary>
            public static bool Open { get { return Instance.settingsMenu.Visible; } set { Instance.settingsMenu.Visible = value; } }

            /// <summary>
            /// Mod control root used by Rich HUD Master.
            /// </summary>
            public static IModControlRoot Root => Instance.root;

            private static RichHudTerminal instance;

            private readonly IModControlRoot root;
            private readonly SettingsMenu settingsMenu;

            private RichHudTerminal() : base(false, true)
            {
                settingsMenu = new SettingsMenu(HudMain.Root);
                root = settingsMenu.AddModRoot("Rich HUD Master");
                MyAPIGateway.Utilities.MessageEntered += MessageHandler;
            }

            private void MessageHandler(string message, ref bool sendToOthers)
            {
                if (Open)
                    sendToOthers = false;
            }

            public static void Init()
            {
                if (instance == null)
                {
                    instance = new RichHudTerminal();
                    Open = false;
                }
            }

            public override void Close()
            {
                MyAPIGateway.Utilities.MessageEntered -= MessageHandler;
                instance = null;
            }

            public static IModControlRoot AddModRoot(string name) =>
                Instance.settingsMenu.AddModRoot(name);

            public static MyTuple<object, IHudElement> GetClientData(string clientName)
            {
                ModControlRoot newRoot = Instance.settingsMenu.AddModRoot(clientName);

                var data = new SettingsMenuMembers()
                {
                    Item1 = GetOrSetMembers,
                    Item2 = newRoot.GetApiData(),
                    Item3 = GetNewTerminalControl,
                    Item4 = GetNewControlContainer,
                    Item5 = GetNewModPage,
                };

                return new MyTuple<object, IHudElement>(data, newRoot);
            }

            private static object GetOrSetMembers(object data, int memberEnum)
            {
                return null;
            }

            private static ControlMembers GetNewTerminalControl(int controlEnum)
            {
                switch ((MenuControls)controlEnum)
                {
                    case MenuControls.Checkbox:
                        return new TerminalCheckbox().GetApiData();
                    case MenuControls.ColorPicker:
                        return new TerminalColorPicker().GetApiData();
                    case MenuControls.DropdownControl:
                        return new TerminalDropdown<object>().GetApiData();
                    case MenuControls.ListControl:
                        return new TerminalList<object>().GetApiData();
                    case MenuControls.OnOffButton:
                        return new TerminalOnOffButton().GetApiData();
                    case MenuControls.SliderSetting:
                        return new TerminalSlider().GetApiData();
                    case MenuControls.TerminalButton:
                        return new TerminalButton().GetApiData();
                    case MenuControls.TextField:
                        return new TerminalTextField().GetApiData();
                    case MenuControls.DragBox:
                        return new TerminalDragBox().GetApiData();
                }

                return default(ControlMembers);
            }

            private static ControlContainerMembers GetNewControlContainer(int containerEnum)
            {
                switch ((ControlContainers)containerEnum)
                {
                    case ControlContainers.Tile:
                        return new ControlTile().GetApiData();
                    case ControlContainers.Category:
                        return new ControlCategory().GetApiData();
                }

                return default(ControlContainerMembers);
            }

            private static ControlMembers GetNewModPage(int pageEnum)
            {
                switch ((ModPages)pageEnum)
                {
                    case ModPages.ControlPage:
                        return new ControlPage().GetApiData();
                    case ModPages.RebindPage:
                        return new RebindPage().GetApiData();
                }

                return default(ControlMembers);
            }

            /// <summary>
            /// Settings menu main window.
            /// </summary>
            private class SettingsMenu : WindowBase
            {
                public event Action OnSelectionChanged;

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

                public ModControlRoot Selection { get; private set; }

                /// <summary>
                /// Currently selected control page.
                /// </summary>
                public TerminalPageBase CurrentPage => Selection?.SelectedElement;

                public ReadOnlyCollection<ModControlRoot> ModRoots => modList.scrollBox.List;

                public override bool Visible => base.Visible && MyAPIGateway.Gui.ChatEntryVisible;

                private readonly ModList modList;
                private readonly HudChain<HudElementBase> chain;
                private readonly TexturedBox topDivider, middleDivider, bottomDivider;
                private readonly Button closeButton;
                private readonly List<TerminalPageBase> pages;
                private static readonly Material closeButtonMat = new Material("RichHudCloseButton", new Vector2(32f));

                public SettingsMenu(IHudParent parent = null) : base(parent)
                {
                    pages = new List<TerminalPageBase>();

                    Header.Format = HeaderFormat;
                    Header.SetText("Rich HUD Terminal");

                    header.Height = 60f;

                    topDivider = new TexturedBox(header)
                    {
                        ParentAlignment = ParentAlignments.Bottom,
                        DimAlignment = DimAlignments.Width,
                        Padding = new Vector2(80f, 0f),
                        Height = 1f,
                    };

                    modList = new ModList();

                    middleDivider = new TexturedBox()
                    {
                        Padding = new Vector2(24f, 0f),
                        Width = 26f,
                    };

                    chain = new HudChain<HudElementBase>(topDivider)
                    {
                        AutoResize = true,
                        AlignVertical = false,
                        Spacing = 12f,
                        Padding = new Vector2(80f, 40f),
                        ParentAlignment = ParentAlignments.Bottom | ParentAlignments.Left | ParentAlignments.InnerH,
                        ChildContainer = { modList, middleDivider },
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
                        Size = new Vector2(30f),
                        Offset = new Vector2(-18f, -14f),
                        Color = new Color(173, 182, 189),
                        highlightColor = Color.White,
                        ParentAlignment = ParentAlignments.Top | ParentAlignments.Right | ParentAlignments.Inner
                    };

                    closeButton.MouseInput.OnLeftClick += CloseMenu;
                    SharedBinds.Escape.OnNewPress += CloseMenu;
                    MasterBinds.ToggleTerminal.OnNewPress += ToggleMenu;

                    BodyColor = new Color(37, 46, 53);
                    BorderColor = new Color(84, 98, 107);

                    Padding = new Vector2(80f, 40f);
                    MinimumSize = new Vector2(1024f, 500f);

                    modList.Width = 200f;
                    Size = new Vector2(1320, 850f);
                    Offset = new Vector2(252f, 70f);

                    if (HudMain.ScreenWidth < 1920)
                        Width = MinimumSize.X;

                    if (HudMain.ScreenHeight < 1050)
                        Height = MinimumSize.Y;
                }

                /// <summary>
                /// Creates and returns a new control root with the name given.
                /// </summary>
                public ModControlRoot AddModRoot(string clientName)
                {
                    ModControlRoot modSettings = new ModControlRoot(this) { Name = clientName };

                    modList.AddToList(modSettings);
                    modSettings.OnModUpdate += UpdateSelection;

                    return modSettings;
                }

                /// <summary>
                /// Adds a new control page to the settings menu.
                /// </summary>
                public void AddPage(TerminalPageBase page)
                {
                    pages.Add(page);
                    chain.Add(page);
                }

                /// <summary>
                /// Toggles menu visiblity, but only if chat is open.
                /// </summary>
                public void ToggleMenu()
                {
                    if (MyAPIGateway.Gui.ChatEntryVisible)
                        Visible = !Visible;
                }

                /// <summary>
                /// Closes the settings menu.
                /// </summary>
                public void CloseMenu()
                {
                    if (Visible)
                        Visible = false;
                }

                protected override void BeforeDraw()
                {
                    Scale = HudMain.ResScale;

                    base.BeforeDraw();

                    if (CurrentPage != null)
                        CurrentPage.Width = Width - Padding.X - modList.Width - chain.Spacing;

                    chain.Height = Height - header.Height - topDivider.Height - Padding.Y - bottomDivider.Height;
                    modList.Width = 250f * Scale;

                    BodyColor = BodyColor.SetAlphaPct(HudMain.UiBkOpacity);
                    header.Color = BodyColor;
                }

                private void UpdateSelection(ModControlRoot selection)
                {
                    Selection = selection;
                    UpdateSelectionVisibilty();
                    OnSelectionChanged?.Invoke();

                    for (int n = 0; n < modList.scrollBox.List.Count; n++)
                    {
                        if (modList.scrollBox.List[n] != Selection)
                            modList.scrollBox.List[n].ClearSelection();
                    }
                }

                private void UpdateSelectionVisibilty()
                {
                    for (int n = 0; n < pages.Count; n++)
                        pages[n].Visible = false;

                    if (CurrentPage != null)
                        CurrentPage.Visible = true;
                }

                /// <summary>
                /// Scrollable list of mod control roots.
                /// </summary>
                private class ModList : HudElementBase
                {
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

                    public readonly LabelBox header;
                    public readonly ScrollBox<ModControlRoot> scrollBox;

                    public ModList(IHudParent parent = null) : base(parent)
                    {
                        scrollBox = new ScrollBox<ModControlRoot>(this)
                        {
                            AlignVertical = true,
                            SizingMode = ScrollBoxSizingModes.FitMembersToBox,
                            Color = ListBgColor,
                            ParentAlignment = ParentAlignments.Bottom | ParentAlignments.InnerV,
                        };

                        //scrollBox.Members.Padding = new Vector2(8f, 8f);

                        header = new LabelBox(scrollBox)
                        {
                            AutoResize = false,
                            Format = ControlFormat,
                            Text = "Mod List:",
                            TextPadding = new Vector2(30f, 0f),
                            Color = new Color(32, 39, 45),
                            Size = new Vector2(200f, 36f),
                            ParentAlignment = ParentAlignments.Top,
                            DimAlignment = DimAlignments.Width
                        };

                        var listDivider = new TexturedBox(header)
                        {
                            Color = new Color(53, 66, 75),
                            Height = 1f,
                            ParentAlignment = ParentAlignments.Bottom,
                            DimAlignment = DimAlignments.Width,
                        };

                        var listBorder = new BorderBox(this)
                        {
                            Color = new Color(53, 66, 75),
                            Thickness = 1f,
                            DimAlignment = DimAlignments.Both,
                        };
                    }

                    /// <summary>
                    /// Adds a new mod control root to the list.
                    /// </summary>
                    public void AddToList(ModControlRoot modSettings)
                    {
                        scrollBox.AddToList(modSettings);
                    }

                    protected override void Draw()
                    {
                        header.Width = scrollBox.Width;

                        header.Color = ListHeaderColor.SetAlphaPct(HudMain.UiBkOpacity);
                        scrollBox.Color = ListBgColor.SetAlphaPct(HudMain.UiBkOpacity);

                        SliderBar slider = scrollBox.scrollBar.slide;
                        slider.BarColor = RichHudTerminal.ScrollBarColor.SetAlphaPct(HudMain.UiBkOpacity);
                    }
                }
            }
        }
    }
}