using RichHudFramework.Internal;
using Sandbox.ModAPI;
using System;
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

    namespace UI
    {
        using SettingsMenuMembers = MyTuple<
            ApiMemberAccessor, // GetOrSetMembers
            ControlContainerMembers, // MenuRoot
            Func<int, ControlMembers>, // GetNewControl
            Func<int, ControlContainerMembers>, // GetNewContainer
            Func<int, ControlMembers> // GetNewModPage
        >;
        
        namespace Server
        {
            /// <summary>
            /// Windowed settings menu shared by mods using the framework.
            /// </summary>
            public sealed partial class RichHudTerminal : RichHudComponentBase
            {
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

                private readonly ModControlRoot root;
                private readonly TerminalWindow settingsMenu;

                private RichHudTerminal() : base(false, true)
                {
                    settingsMenu = new TerminalWindow(HudMain.Root);
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

                public static MyTuple<object, HudElementBase> GetClientData(string clientName)
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

                    return new MyTuple<object, HudElementBase>(data, newRoot.Element);
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
                        case ModPages.TextPage:
                            return new TextPage().GetApiData();
                    }

                    return default(ControlMembers);
                }
            }
        }
    }
}