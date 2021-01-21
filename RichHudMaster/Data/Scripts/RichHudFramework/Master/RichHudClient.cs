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
    using ApiMemberAccessor = System.Func<object, int, object>;

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

            /// <summary>
            /// Delegate used to invoke methods within the scope of the client's exception handler. 
            /// </summary>
            public Action<Action> RunOnExceptionHandler { get; private set; }

            /// <summary>
            /// Accessor delegate for general data access
            /// </summary>
            public ApiMemberAccessor GetOrSetMemberFunc { get; private set; }

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
                bindClient = new BindManager.Client(this);
                menuData = RichHudTerminal.GetClientData(name);

                Registered = true;

                SendData(MsgTypes.RegistrationSuccessful, new ServerData(Unregister, GetApiData, versionID));
                ExceptionHandler.WriteToLogAndConsole($"[RHF] Successfully registered {name} with the API.");
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

            public void RegisterExtendedAccessors(Action<Action> RunWithExceptionHandler, ApiMemberAccessor GetOrSetMemberFunc)
            {
                if (this.RunOnExceptionHandler == null && this.GetOrSetMemberFunc == null)
                {
                    this.RunOnExceptionHandler = RunWithExceptionHandler;
                    this.GetOrSetMemberFunc = GetOrSetMemberFunc;
                }
            }

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
                    ExceptionHandler.WriteToLogAndConsole($"[RHF] Unregistered {name} from API.", true);
                }
            }
        }
    }
}