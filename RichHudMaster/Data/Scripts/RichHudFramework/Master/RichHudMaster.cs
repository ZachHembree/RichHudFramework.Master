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
using ClientData = VRage.MyTuple<string, System.Action<int, object>, System.Action, int>;
using ServerData = VRage.MyTuple<System.Action, System.Func<int, object>, int>;

namespace RichHudFramework.Server
{
    using UI.Rendering.Server;
    using UI.Server;
    using ExtendedClientData = MyTuple<ClientData, Action<Action>, ApiMemberAccessor>;

    /// <summary>
    /// Main class for Framework API server.
    /// </summary>
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation, 0)]
    public sealed partial class RichHudMaster : ModBase
    {
        private const long modID = 1965654081, queueID = 1314086443;
        private const int apiVID = 9, minApiVID = 7;
        public static readonly Vector4I versionID = new Vector4I(1, 2, 0, 0); // Major, Minor, Rev, Hotfix

        /// <summary>
        /// Read-only list of currently registered clients
        /// </summary>
        public static IReadOnlyList<ModClient> Clients => Instance.clients;

        public static RichHudMaster Instance { get; private set; }

        private readonly List<ModClient> clients;

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

            clients = new List<ModClient>();
        }

        protected override void AfterLoadData()
        {
            CmdManager.GetOrCreateGroup("/rhd", GetChatCommands());

            FontManager.Init();
            BindManager.Init();
            MasterConfig.Load(true);
        }

        protected override void AfterInit()
        {
            HudMain.Init();
            RichHudDebug.Init();
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
                    () => RichHudTerminal.OpenMenu())
            };
        }

        /// <summary>
        /// Processes registration requests from API clients.
        /// </summary>
        private void ClientHandler(object message)
        {
            if (message is ExtendedClientData)
                RegisterClient((ExtendedClientData)message);
            else if (message is ClientData)
                RegisterClient((ClientData)message);
        }

        private ModClient RegisterClient(ExtendedClientData regMessage)
        {
            ModClient client = RegisterClient(regMessage.Item1);

            if (client != null)
                client.RegisterExtendedAccessors(regMessage.Item2, regMessage.Item3);

            return client;
        }

        private ModClient RegisterClient(ClientData clientData)
        {
            int clientVID = clientData.Item4;
            bool supported = clientVID <= apiVID && clientVID >= minApiVID;
            ModClient client = clients.Find(x => (x.name == clientData.Item1));

            if (client == null && supported)
            {
                client = new ModClient(clientData);
                clients.Add(client);
            }
            else
            {
                Action<int, object> GetOrSendFunc = clientData.Item2;

                if (!supported)
                {
                    string error = $"Error: Client version for {clientData.Item1} is not supported. " +
                    $"API vID: Min: {minApiVID}, Max: {apiVID}; Client vID: {clientVID}";

                    GetOrSendFunc((int)MsgTypes.RegistrationFailed, error);
                    ExceptionHandler.WriteToLogAndConsole($"[RHF] {error}");
                }
                else
                    GetOrSendFunc((int)MsgTypes.RegistrationFailed, "Client already registered.");
            }

            return client;
        }

        public override void BeforeClose()
        {
            UnregisterClientHandler();

            MasterConfig.Save();
            MasterConfig.ClearSubscribers();

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