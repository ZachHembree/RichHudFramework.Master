using RichHudFramework.Game;
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
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation, 1)]
    internal sealed partial class RichHudMaster : ModBase
    {
        private const long modID = 1965654081, queueID = 1314086443;
        private const int versionID = 4;

        private static RichHudMaster Instance { get; set; }
        private readonly List<RichHudClient> clients;
        private CmdManager.Group rhdCommands;

        public RichHudMaster() : base(false, true)
        {
            if (Instance == null)
                Instance = this;
            else
                throw new Exception("Only one instance of RichHudMaster can exist at any given time.");

            ModName = "Rich HUD Master";
            LogIO.FileName = "RichHudMasterLog.txt";
            MasterConfig.FileName = "RichHudMasterConfig.xml";

            clients = new List<RichHudClient>();
        }

        protected override void AfterLoadData()
        {
            RichHudMain.MainModName = ModName;
            rhdCommands = CmdManager.AddOrGetCmdGroup("/rhd", GetChatCommands());

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

                RichHudClient client = clients.Find(x => (x.debugName == clientData.Item1));

                if (client == null && clientData.Item4 <= versionID)
                {
                    SendChatMessage($"Registered {clientData.Item1}");
                    clients.Add(new RichHudClient(clientData));
                }
                else
                {
                    Action<int, object> SendMsgAction = clientData.Item2;
                    SendChatMessage($"Failed to register {clientData.Item1}");

                    if (clientData.Item4 != versionID)
                        SendMsgAction((int)MsgTypes.RegistrationFailed, $"Client version mismatch. Server vID: {versionID}, Client vID: {clientData.Item4}");
                    else
                        SendMsgAction((int)MsgTypes.RegistrationFailed, "Client already registered.");
                }
            }
        }

        protected override void BeforeClose()
        {
            UnregisterClientHandler();

            MasterConfig.Save();
            MasterConfig.ClearSubscribers();
            rhdCommands = null;

            if (Reloading)
            {
                for (int n = clients.Count - 1; n >= 0; n--)
                    clients[n].Unregister();

                clients.Clear();
                RichHudMain.Instance.Reload();
            }
            else if (Unloading)
            {
                Instance = null;
            }
        }
    }
}