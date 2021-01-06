using System;
using VRage;

namespace RichHudFramework.Server
{
    using UI;
    using UI.Server;
    using UI.Rendering.Server;
    using Internal;
    using ClientData = MyTuple<string, Action<int, object>, Action, int>;
    using ServerData = MyTuple<Action, Func<int, object>, int>;

    public sealed partial class RichHudMaster
    {
        public class Client
        {
            /// <summary>
            /// Name of the client mod as reported by the client
            /// </summary>
            public readonly string name;

            /// <summary>
            /// VersionID of the client
            /// </summary>
            public readonly int versionID;

            /// <summary>
            /// If true then the client is currently registered with master
            /// </summary>
            public bool Registered { get; private set; }

            private readonly Action<int, object> SendMsgAction;
            private readonly Action ReloadAction;

            private readonly HudMain.Client hudClient;
            private readonly BindManager.Client bindClient;
            private MyTuple<object, HudElementBase> menuData;

            public Client(ClientData data)
            {
                name = data.Item1;
                SendMsgAction = data.Item2;
                ReloadAction = data.Item3;
                versionID = data.Item4;

                hudClient = new HudMain.Client();
                bindClient = new BindManager.Client();
                menuData = RichHudTerminal.GetClientData(name);

                Registered = true;

                SendData(MsgTypes.RegistrationSuccessful, new ServerData(() => ExceptionHandler.Run(Unregister), GetApiData, versionID));
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
                        return hudClient.GetApiData();
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
                if (Registered)
                {
                    Registered = false;

                    if (Instance != null && !ExceptionHandler.Unloading)
                    {
                        Instance.clients.Remove(this);
                        bindClient.Unload();
                        menuData.Item2.Unregister();
                    }

                    ReloadAction();
                }
            }
        }
    }
}