using System;
using System.Collections.Generic;
using VRage;
using VRageMath;
using ApiMemberAccessor = System.Func<object, int, object>;
using HudSpaceDelegate = System.Func<VRage.MyTuple<float, VRageMath.MatrixD>>;
using HudLayoutDelegate = System.Func<bool, bool>;
using HudDrawDelegate = System.Func<object, object>;
using HudInputDelegate = System.Func<
    VRageMath.Vector3, // CursorPos
    System.Func<VRage.MyTuple<float, VRageMath.MatrixD>>, // GetHudSpaceFunc
    VRage.MyTuple<VRageMath.Vector3, System.Func<VRage.MyTuple<float, VRageMath.MatrixD>>> // Return
>;

namespace RichHudFramework
{
    using HudUpdateAccessors = MyTuple<
        int, // ZOffset
        uint, // Depth
        HudLayoutDelegate, // BeforeLayout
        HudDrawDelegate, // BeforeDraw
        HudInputDelegate // HandleInput
    >;

    namespace Server
    {
        using UI;
        using UI.Server;
        using UI.Rendering.Server;
        using Internal;
        using ClientData = MyTuple<string, ApiMemberAccessor, Action, int>;
        using ServerData = MyTuple<Action, Func<int, object>, int>;
        using HudAccessorDelegate = Action<List<HudUpdateAccessors>, int>;

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
                /// Delegate used to retrieve UI update delegates from clients
                /// </summary>
                public readonly HudAccessorDelegate GetUpdateAccessorFunc;

                private readonly IBindClient bindClient;

                private readonly ApiMemberAccessor GetOrSendFunc;
                private readonly Action ReloadAction;
                private MyTuple<object, HudElementBase> menuData;
                private bool registered;

                public Client(ClientData data)
                {
                    name = data.Item1;
                    GetOrSendFunc = data.Item2;
                    ReloadAction = data.Item3;
                    versionID = data.Item4;

                    bindClient = BindManager.GetNewBindClient();
                    menuData = RichHudTerminal.GetClientData(name);
                    registered = true;

                    GetOrSendData
                    (
                        new ServerData(
                            () => ExceptionHandler.Run(Unregister),
                            GetApiData,
                            versionID
                        ),
                        MsgTypes.RegistrationSuccessful
                    );

                    GetUpdateAccessorFunc = GetOrSendData(null, MsgTypes.GetHudUpdateAccessor) as HudAccessorDelegate;
                }

                /// <summary>
                /// Accessor for API client modules
                /// </summary>
                private object GetApiData(int typeID)
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
                public object GetOrSendData(object data, MsgTypes msgType) =>
                    GetOrSendFunc(data, (int)msgType);

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
}