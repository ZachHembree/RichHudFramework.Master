using RichHudFramework.Internal;
using RichHudFramework.IO;
using RichHudFramework.UI;
using RichHudFramework.UI.Rendering;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRageMath;
using ApiMemberAccessor = System.Func<object, int, object>;

namespace RichHudFramework.Server
{
    using UI.Rendering.Server;
    using UI.Server;
    using ClientData = MyTuple<string, ApiMemberAccessor, Action, int>;

    /// <summary>
    /// Main class for Framework API server.
    /// </summary>
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation, 0)]
    public sealed partial class RichHudMaster : ModBase
    {
        private const long modID = 1965654081, queueID = 1314086443;
        private const int versionID = 7, minSupportedVersion = 7;

        /// <summary>
        /// Read-only list of currently registered clients
        /// </summary>
        public static IReadOnlyList<Client> Clients => Instance.clients;

        private static RichHudMaster Instance { get; set; }
        private readonly List<Client> clients;
        private ICommandGroup rhdCommands;

        private LabelBox debugBox;
        private ScrollBox<ScrollBoxEntry<LabelBox>, LabelBox> chainTest;

        public RichHudMaster() : base(false, true)
        {
            if (Instance == null)
                Instance = this;
            else
                throw new Exception("Only one instance of RichHudMaster can exist at any given time.");

            LogIO.FileName = "RichHudMasterLog.txt";
            MasterConfig.FileName = "RichHudMasterConfig.xml";

            ExceptionHandler.RecoveryLimit = 5;
            ExceptionHandler.ModName = "Rich HUD Master";

            clients = new List<Client>();
        }

        protected override void AfterLoadData()
        {
            rhdCommands = CmdManager.GetOrCreateGroup("/rhd", GetChatCommands());

            FontManager.Init();
            BindManager.Init();
            MasterConfig.Load(true);
        }

        protected override void AfterInit()
        {
            HudMain.Init();
            InitSettingsMenu();

            RegisterClientHandler();
            CheckClientQueue();

            if (MenuUtilities.CanAddElements)
                MenuUtilities.AddMenuElements(GetModMenuButton());

            var window = new Window(HudMain.Root)
            {
                MinimumSize = new Vector2(100f),
                Size = new Vector2(400f)
            };

            // test parent alignment next
            chainTest = new ScrollBox<ScrollBoxEntry<LabelBox>, LabelBox>(true, window.body)
            {
                Visible = false,
                DimAlignment = DimAlignments.Both,
                SizingMode = HudChainSizingModes.ClampChainBoth,
                //Size = new Vector2(300f, 100f),
                Padding = new Vector2(24f),
                Spacing = 4f,
                MinVisibleCount = 2,
                ChainContainer =
                {
                    new LabelBox()
                    {
                        AutoResize = false,
                        Height = 70f,
                        Width = 100f,
                        ParentAlignment = ParentAlignments.Right,
                        Color = new Color(255, 255, 255),
                        Format = GlyphFormat.Black,
                        Text = "Test Text 1"
                    },
                    new LabelBox()
                    {
                        AutoResize = false,
                        Height = 80f,
                        Width = 100f,
                        ParentAlignment = ParentAlignments.Left,
                        Color = new Color(255, 255, 255, 128),
                        Format = GlyphFormat.Black,
                        Text = "Test Text 2"
                    },
                    new LabelBox()
                    {
                        AutoResize = false,
                        Height = 50f,
                        Width = 100f,
                        ParentAlignment = ParentAlignments.Right,
                        Color = new Color(255, 255, 255, 128),
                        Format = GlyphFormat.Black,
                        Text = "Test Text 3"
                    },
                    new LabelBox()
                    {
                        AutoResize = false,
                        Height = 100f,
                        Width = 100f,
                        ParentAlignment = ParentAlignments.Left,
                        Color = new Color(255, 255, 255, 128),
                        Format = GlyphFormat.Black,
                        Text = "Test Text 4"
                    }
                }
            };

            var listTest = new Dropdown<int>(window.body)
            {
                Visible = true,
                DimAlignment = DimAlignments.Width | DimAlignments.IgnorePadding,
                ParentAlignment = ParentAlignments.Top | ParentAlignments.Inner,
                Padding = new Vector2(20f),
                ListContainer =
                {
                    { "Entry 1", 0 },
                    { "Entry 2", 0 },
                    { "Entry 3", 0 },
                    { "Entry 4", 0 },
                    { "Entry 5", 0 },
                    { "Entry 6", 0 },
                },
            };

            debugBox = new LabelBox(chainTest)
            {
                Color = new Color(0, 0, 0, 196),
                Format = GlyphFormat.White,
                BuilderMode = TextBuilderModes.Lined,
                ParentAlignment = ParentAlignments.Right,
                VertCenterText = false,
                AutoResize = false,
                Size = new Vector2(450f, 500f)
            };
        }

        public override void Draw()
        {
            base.Draw();

            if (chainTest.Visible)
            {
                ITextBuilder textBuilder = debugBox.TextBoard;
                var cursorElement = HudMain.Cursor as HudElementBase;
                textBuilder.Clear();

                textBuilder.Append(new RichText
                {
                    { $"Size: {chainTest.Size}\n" },
                    { $"Chain[0] Size: {chainTest.ChainEntries[0].Element.Size}\n" },
                    { $"Chain[0] Enabled: {chainTest.ChainEntries[0].Enabled}\n" },
                    { $"Padding: {chainTest.Padding}\n\n" },

                    { $"MemberMinSize: {chainTest.MemberMinSize}\n" },
                    { $"MemberMaxSize: {chainTest.MemberMaxSize}\n" },
                    { $"MinLength: {chainTest.MinLength}\n" },
                    { $"MinVisibleCount: {chainTest.MinVisibleCount}\n\n" },

                    { $"Start: {chainTest.Start}\n" },
                    { $"End: {chainTest.End}\n" },
                    { $"EnabledCount: {chainTest.EnabledCount}\n" },
                    { $"VisCount: {chainTest.VisCount}\n" },
                    { $"VisCount: {chainTest.ChainEntries.Count}\n\n" },

                    { $"Cursor ScreenPos: {HudMain.Cursor.ScreenPos}\n" },
                    { $"Captured: {HudMain.Cursor.IsCaptured}\n" },
                    { $"Size: {cursorElement.Size}\n" },
                    { $"Scale: {cursorElement.Scale}\n" },
                    { $"Visible: {HudMain.Cursor.Visible}\n\n" },
                });
            }
        }

        public class Window : WindowBase
        {
            public Window(HudParentBase parent = null) : base(parent)
            {
                BodyColor = new Color(0, 0, 0, 0);
            }
        }

        /// <summary>
        /// Registers the callback method for client registration.
        /// </summary>
        private void RegisterClientHandler() =>
            MyAPIUtilities.Static.RegisterMessageHandler(modID, ClientHandler);

        /// <summary>
        /// Unegisters the callback method for client registration.
        /// </summary>
        private void UnregisterClientHandler() =>
            MyAPIUtilities.Static.UnregisterMessageHandler(modID, ClientHandler);

        /// <summary>
        /// Queries the client queue for any clients awaiting registration.
        /// </summary>
        private void CheckClientQueue() =>
            MyAPIUtilities.Static.SendModMessage(queueID, modID);

        /// <summary>
        /// Generates a ModMenu button for opening the Rich HUD Terminal.
        /// </summary>
        private static List<MenuUtilities.IMenuElement> GetModMenuButton()
        {
            return new List<MenuUtilities.IMenuElement>()
            {
                new MenuUtilities.MenuButton(
                    $"Open Rich Hud Terminal",
                    () => RichHudTerminal.Open = true)
            };
        }

        /// <summary>
        /// Processes registration requests from API clients.
        /// </summary>
        private void ClientHandler(object message)
        {
            if (message is ClientData)
            {
                var clientData = (ClientData)message;
                Utils.Debug.AssertNotNull(clientData.Item1);
                Utils.Debug.AssertNotNull(clientData.Item2);
                Utils.Debug.AssertNotNull(clientData.Item3);

                Client client = clients.Find(x => (x.name == clientData.Item1));

                int clientVID = clientData.Item4;
                bool supported = clientVID <= versionID && clientVID >= minSupportedVersion;

                if (client == null && supported)
                {
                    clients.Add(new Client(clientData));
                }
                else
                {
                    ApiMemberAccessor GetOrSendFunc = clientData.Item2;

                    if (!supported)
                    {
                        string error = $"Error: Client version for {clientData.Item1} is not supported. " +
                        $"API vID: Min: {minSupportedVersion}, Max: {versionID}; Client vID: {clientVID}";

                        GetOrSendFunc(error, (int)MsgTypes.RegistrationFailed);
                        ExceptionHandler.SendChatMessage(error);
                    }
                    else
                        GetOrSendFunc("Client already registered.", (int)MsgTypes.RegistrationFailed);
                }
            }
        }

        public override void BeforeClose()
        {
            UnregisterClientHandler();

            MasterConfig.Save();
            MasterConfig.ClearSubscribers();
            rhdCommands = null;

            if (ExceptionHandler.Reloading)
            {
                for (int n = clients.Count - 1; n >= 0; n--)
                    clients[n].Unregister();

                clients.Clear();
            }
            else if (ExceptionHandler.Unloading)
            {
                Instance = null;
            }
        }
    }
}