using RichHudFramework.Server;
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
                public ModControlRoot SelectedMod { get; private set; }

                /// <summary>
                /// Currently selected control page.
                /// </summary>
                public TerminalPageBase CurrentPage { get; private set; }

                /// <summary>
                /// Read only collection of mod roots registered with the terminal
                /// </summary>
                public IReadOnlyList<ModControlRoot> ModRoots => modList.ModRoots;

                public override bool Visible => base.Visible && MyAPIGateway.Gui.ChatEntryVisible;

                private readonly ModList modList;
                private readonly HudChain layout;
                private readonly TexturedBox topDivider, middleDivider, bottomDivider;
                private readonly Button closeButton;
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

                    closeButton.MouseInput.OnLeftClick += (sender, args) => CloseMenu();
                    SharedBinds.Escape.OnNewPress += CloseMenu;
                    MasterBinds.ToggleTerminal.OnNewPress += ToggleMenu;

                    BodyColor = new Color(37, 46, 53);
                    BorderColor = new Color(84, 98, 107);

                    Padding = new Vector2(80f, 40f);
                    MinimumSize = new Vector2(1024f, 500f);

                    Size = new Vector2(1320, 850f);
                    Vector2 screenSize = new Vector2(HudMain.ScreenWidth, HudMain.ScreenHeight);

                    if (screenSize.X < 1920)
                        Width = MinimumSize.X;

                    if (screenSize.Y < 1080 || HudMain.AspectRatio < (16f/9f))
                        Height = MinimumSize.Y;

                    Offset = (screenSize - Size) / 2f - new Vector2(40f);
                }

                /// <summary>
                /// Creates and returns a new control root with the name given.
                /// </summary>
                public ModControlRoot AddModRoot(string clientName)
                {
                    ModControlRoot modSettings = new ModControlRoot() { Name = clientName };

                    modList.Add(modSettings);
                    modSettings.OnSelectionChanged += UpdateSelection;

                    return modSettings;
                }

                /// <summary>
                /// Toggles menu visiblity, but only if chat is open.
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
                    if (!Visible && MyAPIGateway.Gui.ChatEntryVisible)
                    {
                        Visible = true;
                        GetFocus();
                        HudMain.EnableCursor = true;
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

                /// <summary>
                /// Opens the given terminal page
                /// </summary>
                public void SetSelection(ModControlRoot modRoot, TerminalPageBase newPage)
                {
                    SelectedMod = modRoot;

                    if (CurrentPage != null && newPage != CurrentPage)
                        layout.RemoveAt(2); // I'm sure this wont bite me in the ass

                    if (newPage != null && newPage != CurrentPage)
                        layout.Add(newPage);

                    CurrentPage = newPage;

                    for (int n = 0; n < modList.ModRoots.Count; n++)
                    {
                        if (modList.ModRoots[n] != SelectedMod)
                            modList.ModRoots[n].Element.ClearSelection();
                    }
                }

                private void UpdateSelection(object sender, EventArgs args)
                {
                    var modRoot = sender as ModControlRoot;
                    var newPage = SelectedMod?.Selection as TerminalPageBase;

                    SetSelection(modRoot, newPage);
                }

                protected override void Layout()
                {
                    Scale = HudMain.ResScale;

                    base.Layout();

                    if (CurrentPage != null)
                        CurrentPage.Element.Width = Width - Padding.X - modList.Width - layout.Spacing;

                    layout.Height = Height - header.Height - topDivider.Height - Padding.Y - bottomDivider.Height;
                    modList.Width = 250f * Scale;

                    BodyColor = BodyColor.SetAlphaPct(HudMain.UiBkOpacity);
                    header.Color = BodyColor;
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

                    public IReadOnlyList<ModControlRoot> ModRoots => scrollBox.ChainEntries;

                    private readonly LabelBox header;
                    private readonly ScrollBox<ModControlRoot, ModControlRootTreeBox> scrollBox;

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
                    }

                    /// <summary>
                    /// Adds a new mod control root to the list.
                    /// </summary>
                    public void Add(ModControlRoot modSettings)
                    {
                        scrollBox.Add(modSettings);
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