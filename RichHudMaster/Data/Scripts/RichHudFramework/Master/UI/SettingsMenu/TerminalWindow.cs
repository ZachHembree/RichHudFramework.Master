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
                public ModControlRoot Selection { get; private set; }

                /// <summary>
                /// Currently selected control page.
                /// </summary>
                public TerminalPageBase CurrentPage => Selection?.SelectedElement;

                /// <summary>
                /// Read only collection of mod roots registered with the terminal
                /// </summary>
                public ReadOnlyCollection<ModControlRoot> ModRoots => modList.scrollBox.List;

                public override bool Visible => base.Visible && MyAPIGateway.Gui.ChatEntryVisible;

                private readonly ModList modList;
                private readonly HudChain<HudElementBase> chain;
                private readonly TexturedBox topDivider, middleDivider, bottomDivider;
                private readonly Button closeButton;
                private readonly List<TerminalPageBase> pages;
                private static readonly Material closeButtonMat = new Material("RichHudCloseButton", new Vector2(32f));

                public TerminalWindow(IHudParent parent = null) : base(parent)
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

                protected override void Layout()
                {
                    Scale = HudMain.ResScale;

                    base.Layout();

                    if (CurrentPage != null)
                        CurrentPage.Width = Width - Padding.X - modList.Width - chain.Spacing;

                    chain.Height = Height - header.Height - topDivider.Height - Padding.Y - bottomDivider.Height;
                    modList.Width = 250f * Scale;

                    BodyColor = BodyColor.SetAlphaPct(HudMain.UiBkOpacity);
                    header.Color = BodyColor;
                }

                private void UpdateSelection(ModControlRoot selection)
                {
                    UpdateSelectionVisibilty();

                    for (int n = 0; n < modList.scrollBox.List.Count; n++)
                    {
                        if (modList.scrollBox.List[n] != selection)
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

                    protected override void Layout()
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