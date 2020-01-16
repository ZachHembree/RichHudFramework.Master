using RichHudFramework.Game;
using RichHudFramework.UI;
using RichHudFramework.UI.FontData;
using RichHudFramework.UI.Rendering;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRageMath;

namespace RichHudFramework.Server
{
    using UI.Server;
    using UI.Rendering.Server;
    using ClientData = MyTuple<string, Action<int, object>, Action, int>;

    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation, 1)]
    internal sealed partial class RichHudMaster : ModBase
    {
        private const long modID = 1965654081, queueID = 1314086443;
        private const int versionID = 1;

        private static new RichHudMaster Instance { get; set; }
        private readonly List<RichHudClient> clients;
        private CmdManager.Group rhdCommands;
        private bool unload;

        static RichHudMaster()
        {
            ModName = "Rich HUD Master";
            LogFileName = "RichHudMasterLog.txt";
        }

        public RichHudMaster() : base(false, true)
        {
            clients = new List<RichHudClient>();
        }

        protected override void AfterInit()
        {
            Instance = this;
            rhdCommands = CmdManager.AddOrGetCmdGroup("/rhd", GetChatCommands());
            InitializeFonts();
            BindManager.Init();
            HudMain.Init();

            RegisterClientHandler();
            CheckClientQueue();

            if (MenuUtilities.CanAddElements)
                MenuUtilities.AddMenuElements(GetModMenuButton());
        }

        private void RegisterClientHandler() =>
            MyAPIUtilities.Static.RegisterMessageHandler(modID, ClientHandler);

        private void UnregisterClientHandler() =>
            MyAPIUtilities.Static.UnregisterMessageHandler(modID, ClientHandler);

        private void CheckClientQueue() =>
            MyAPIUtilities.Static.SendModMessage(queueID, modID);

        private static List<MenuUtilities.IMenuElement> GetModMenuButton()
        {
            return new List<MenuUtilities.IMenuElement>()
            {
                new MenuUtilities.MenuButton(
                        $"Open Rich Hud Terminal",
                        () => RichHudTerminal.Open = true)
            };
        }

        private List<CmdManager.Command> GetChatCommands()
        {
            return new List<CmdManager.Command>
            {
                new CmdManager.Command ("reload",
                    () => Instance.unload = true),
            };
        }

        private void InitializeFonts()
        {
            FontManager.TryAddFont(SeFont.fontData);
            FontManager.TryAddFont(SeFontShadowed.fontData);
            FontManager.TryAddFont(MonoFont.fontData);
            FontManager.TryAddFont(AbhayaLibreMedium.fontData);
            FontManager.TryAddFont(BitstreamVeraSans.fontData);
        }

        private void ClientHandler(object message)
        {
            if (message is ClientData)
            {
                var clientData = (ClientData)message;
                Utils.Debug.AssertNotNull(clientData.Item1);
                RichHudClient client = clients.Find(x => (x.debugName == clientData.Item1));

                if (client == null && clientData.Item4 <= versionID)
                {
                    clients.Add(new RichHudClient(clientData));
                }
                else
                {
                    Action<int, object> SendMsgAction = clientData.Item2;

                    if (clientData.Item4 > versionID)
                        SendMsgAction((int)MsgTypes.RegistrationFailed, $"Client version mismatch. Server vID: {versionID}, Client vID: {clientData.Item4}");
                    else
                        SendMsgAction((int)MsgTypes.RegistrationFailed, "Client already registered.");
                }
            }
        }

        protected override void Update()
        {
            if (unload)
            {
                unload = false;
                Reload();
            }
        }

        protected override void BeforeClose()
        {
            UnregisterClientHandler();

            for (int n = 0; n < clients.Count; n++)
                clients[n].Unregister();

            Instance = null;
        }
    }
}