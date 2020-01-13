using RichHudFramework.Game;
using RichHudFramework.UI.Rendering;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using VRage;
using VRageMath;
using Sandbox.ModAPI;
using ApiMemberAccessor = System.Func<object, int, object>;
using GlyphFormatMembers = VRage.MyTuple<VRageMath.Vector2I, int, VRageMath.Color, float>;
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
    using RichStringMembers = MyTuple<StringBuilder, GlyphFormatMembers>;

    namespace UI.Server
    {
        using SettingsMenuMembers = MyTuple<
            ApiMemberAccessor, // GetOrSetMembers
            ControlContainerMembers, // MenuRoot
            Func<int, ControlMembers>, // GetNewControl
            Func<int, ControlContainerMembers>, // GetNewContainer
            Func<int, ControlMembers> // GetNewModPage
        >;

        public sealed partial class RichHudTerminal : ModBase.ComponentBase
        {
            public static readonly GlyphFormat HeaderFormat = new GlyphFormat(Color.White, TextAlignment.Center, 1.15f);
            public static readonly GlyphFormat ControlFormat = GlyphFormat.Blueish.WithSize(1.08f);

            private static RichHudTerminal Instance
            {
                get { Init(); return instance; }
                set { instance = value; }
            }
            public static bool Open { get { return Instance.settingsMenu.Visible; } set { Instance.settingsMenu.Visible = value; } }
            private static RichHudTerminal instance;
            private readonly SettingsMenu settingsMenu;

            private RichHudTerminal() : base(false, true)
            {
                settingsMenu = new SettingsMenu(HudMain.Root);
                MyAPIGateway.Utilities.MessageEntered += MessageHandler;
            }

            private void MessageHandler(string message, ref bool sendToOthers)
            {
                if (settingsMenu.Visible)
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
                instance = null;
            }

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
                        return new Checkbox().GetApiData();
                    case MenuControls.ColorPicker:
                        return new ColorPicker().GetApiData();
                    case MenuControls.DropdownControl:
                        return new DropdownControl<object>().GetApiData();
                    case MenuControls.ListControl:
                        return new ListControl<object>().GetApiData();
                    case MenuControls.OnOffButton:
                        return new OnOffButton().GetApiData();
                    case MenuControls.SliderSetting:
                        return new SliderSetting().GetApiData();
                    case MenuControls.TerminalButton:
                        return new TerminalButton().GetApiData();
                    case MenuControls.TextField:
                        return new TextField().GetApiData();
                    case MenuControls.DragBox:
                        return new DragBox().GetApiData();
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

                public TerminalPageBase CurrentPage => Selection?.SelectedElement;

                public override bool Visible => base.Visible && MyAPIGateway.Gui.ChatEntryVisible;

                public override float Scale => HudMain.ResScale;

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
                    header.Background.Visible = false;

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
                    SharedBinds.Tilde.OnNewPress += ToggleMenu;

                    BodyColor = new Color(37, 46, 53);
                    BorderColor = new Color(84, 98, 107);

                    Padding = new Vector2(80f, 40f);
                    MinimumSize = new Vector2(700f, 500f);

                    modList.Width = 200f;
                    Size = new Vector2(1320, 850f);
                    Offset = new Vector2(252f, 103f);
                }

                public ModControlRoot AddModRoot(string clientName)
                {
                    ModControlRoot modSettings = new ModControlRoot(this) { Name = clientName };

                    modList.AddToList(modSettings);
                    modSettings.OnModUpdate += UpdateSelection;

                    return modSettings;
                }

                public void AddPage(TerminalPageBase page)
                {
                    pages.Add(page);
                    chain.Add(page);
                }

                public void ToggleMenu()
                {
                    if (MyAPIGateway.Gui.ChatEntryVisible)
                        Visible = !Visible;
                }

                public void CloseMenu()
                {
                    if (Visible)
                        Visible = false;
                }

                protected override void BeforeDraw()
                {
                    base.BeforeDraw();

                    if (CurrentPage != null)
                        CurrentPage.Width = Width - Padding.X - modList.Width - chain.Spacing;

                    chain.Height = Height - header.Height - topDivider.Height - Padding.Y - bottomDivider.Height;
                    modList.Width = 250f;

                    BodyColor = BodyColor.SetAlpha((byte)(HudMain.UiBkOpacity * 255f));
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
                            FitToChain = false,
                            Color = new Color(41, 54, 62, 230),
                            ParentAlignment = ParentAlignments.Bottom | ParentAlignments.InnerV,
                        };

                        header = new LabelBox(scrollBox)
                        {
                            AutoResize = false,
                            Format = ControlFormat,
                            Text = "Mod List:",
                            Color = new Color(32, 39, 45, 230),
                            TextPadding = new Vector2(30f, 0f),
                            Size = new Vector2(200f, 36f),
                            ParentAlignment = ParentAlignments.Top,
                        };

                        var listBorder = new BorderBox(this)
                        {
                            Color = new Color(53, 66, 75),
                            Thickness = 2f,
                            DimAlignment = DimAlignments.Both,
                        };
                    }

                    public void AddToList(ModControlRoot modSettings)
                    {
                        scrollBox.AddToList(modSettings);
                    }

                    protected override void Draw()
                    {
                        header.Width = scrollBox.Width;
                    }
                }
            }
        }
    }
}