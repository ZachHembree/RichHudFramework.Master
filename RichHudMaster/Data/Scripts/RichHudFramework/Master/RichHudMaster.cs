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

namespace RichHudFramework.Server
{
    using UI.Rendering.Server;
    using UI.Server;
    using ClientData = MyTuple<string, Action<int, object>, Action, int>;

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
                    Action<int, object> GetOrSendFunc = clientData.Item2;

                    if (!supported)
                    {
                        string error = $"Error: Client version for {clientData.Item1} is not supported. " +
                        $"API vID: Min: {minSupportedVersion}, Max: {versionID}; Client vID: {clientVID}";

                        GetOrSendFunc((int)MsgTypes.RegistrationFailed, error);
                        ExceptionHandler.WriteToLogAndConsole($" [RHF] {error}");
                    }
                    else
                        GetOrSendFunc((int)MsgTypes.RegistrationFailed, "Client already registered.");
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
        }

        public override void Close()
        {
            base.Close();

            if (ExceptionHandler.Unloading)
            {
                Instance = null;
            }
        }
    }
}