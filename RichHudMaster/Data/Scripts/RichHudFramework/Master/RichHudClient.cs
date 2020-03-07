using System;
using VRage;

namespace RichHudFramework.Server
{
    using UI;
    using UI.Server;
    using UI.Rendering.Server;
    using ClientData = MyTuple<string, Action<int, object>, Action, int>;
    using ServerData = MyTuple<Action, Func<int, object>, int>;

    internal sealed partial class RichHudMaster
    {
        private class RichHudClient
        {
            public readonly string debugName;
            public readonly int versionID;

            private readonly IBindClient bindClient;

            private readonly Action<int, object> SendMsgAction;
            private readonly Action ReloadAction;
            private MyTuple<object, IHudElement> menuData;
            private bool registered;

            public RichHudClient(ClientData data)
            {
                debugName = data.Item1;
                SendMsgAction = data.Item2;
                ReloadAction = data.Item3;
                versionID = data.Item4;

                bindClient = BindManager.GetNewBindClient();
                menuData = RichHudTerminal.GetClientData(debugName);
                registered = true;

                SendData(MsgTypes.RegistrationSuccessful, new ServerData(() => Instance.RunSafeAction(Unregister), GetApiData, versionID));
            }

            /// <summary>
            /// Accessor for API client modules
            /// </summary>
            public object GetApiData(int typeID)
            {
                switch ((ApiModuleTypes)typeID)
                {
                    case ApiModuleTypes.BindManager:
                        return bindClient.GetApiData();
                    case ApiModuleTypes.HudMain:
                        return HudMain.GetApiData();
                    case ApiModuleTypes.FontManager:
                        return FontManager.GetApiData();
                    case ApiModuleTypes.SettingsMenu:
                        return menuData.Item1;
                }

                return null;
            }

            /// <summary>
            /// Sends a message to the client.
            /// </summary>
            public void SendData(MsgTypes msgType, object data) =>
                SendMsgAction((int)msgType, data);

            public void Unregister()
            {
                if (registered)
                {
                    registered = false;
                    Instance.clients.Remove(this);

                    bindClient.Unload();
                    menuData.Item2.Unregister();
                    ReloadAction();
                }
            }
        }
    }
}