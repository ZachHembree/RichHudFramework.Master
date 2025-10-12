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
using MasterVersionData = VRage.MyTuple<object, string, long, int, VRageMath.Vector4I>;

namespace RichHudFramework.Server
{
    using UI.Rendering.Server;
    using UI.Server;
    using ExtendedClientData = MyTuple<ClientData, Action<Action>, ApiMemberAccessor>;
    using MasterMessage = MyTuple<object, byte, object>; // { object sender, queryTypeEnum, object message }
    using MasterRegData = MyTuple<MasterVersionData, Action<object>>; // { Name, ModID, ApiVID, VersionID, Callback(object) }
    using SetVersionFunc = Action<MyTuple<MasterVersionData, Action<object>>>;

    enum ModQueryTypes : byte
    {
        None = 0,
        RegisterInstance = 1,
        ConfirmRegistration = 2,
        GetPriorityInstance = 3,
        TransferInstancePriority = 4,
        RejectRegistration = 5
    }

    /// <summary>
    /// Main class for Framework API server.
    /// </summary>
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public sealed partial class RichHudMaster : ModBase
    {
        public const long modID = 1965654081, queueID = 1314086443;
        public const int apiVID = 11, minApiVID = 7;
        public const string modName = "Rich HUD Master";
        public static readonly Vector4I versionID = new Vector4I(1, 3, 0, 6); // Major, Minor, Rev, Hotfix
        public static readonly string versionString = $"{versionID.X}.{versionID.Y}.{versionID.Z}.{versionID.W} ({apiVID})";

        class MasterRegEntry
        {
            /// <summary>
            /// Reference to the RHM instance registered
            /// </summary>
            public readonly object instance;

            /// <summary>
            /// Mod name string
            /// </summary>
            public readonly string name;

            /// <summary>
            /// 64bit steam mod ID
            /// </summary>
            public readonly long modID;

            /// <summary>
            /// RHF API version
            /// </summary>
            public readonly int apiVID;

            /// <summary>
            /// RHM version
            /// </summary>
            public readonly Vector4I versionID;

            /// <summary>
            /// Callback to RHM instance
            /// </summary>
            public readonly Action<object> CallbackAction;

            public MasterRegEntry(MasterRegData data)
            {
                instance = data.Item1.Item1;
                name = data.Item1.Item2;
                modID = data.Item1.Item3;
                apiVID = data.Item1.Item4;
                versionID = data.Item1.Item5;
                CallbackAction = data.Item2;
            }

            public MasterRegData GetData() { return new MasterRegData(new MasterVersionData(instance, name, modID, apiVID, versionID), CallbackAction); }
        }

        /// <summary>
        /// Read-only list of currently registered clients
        /// </summary>
        public static IReadOnlyList<ModClient> Clients => Instance.clients;

        public static RichHudMaster Instance { get; private set; }

        public static ICommandGroup Commands => Instance._commands;

        /// <summary>
        /// Stores a list of RHM instances, including version information and a reference to a corresponding
        /// instance. Only valid in the instance handling version negotiation.
        /// </summary>
        private readonly List<MasterRegEntry> versionPriorityList;

        /// <summary>
        /// Tuple containing version information for this instance.
        /// </summary>
        private readonly MasterRegData verRegData;

        /// <summary>
        /// Version registration message for this instance, including registration info and callback.
        /// </summary>
        private readonly MasterMessage regMessage;

        /// <summary>
        /// Set to true if this is the only instance and no other copies of this mod exist when AfterInit() is invoked.
        /// </summary>
        private bool hasPriority;

        /// <summary>
        /// Version and instance information for the mod version with priority. During AfterLoadData(), this will be the 
        /// first instance. On AfterInit(), it will be the first instance of the newest version.
        /// </summary>
        private MasterRegEntry priorityVersion;
        private int priorityIndex;

        private readonly List<ModClient> clients;
        private TerminalPageCategory demoCategory;
        private ICommandGroup _commands;
        
        public RichHudMaster() : base(true, true)
        {
            if (Instance == null)
                Instance = this;
            else
                throw new Exception("Only one instance of RichHudMaster can exist at any given time.");

            LogIO.FileName = "RichHudMasterLog.txt";
            MasterConfig.FileName = "RichHudMasterConfig.xml";

            ExceptionHandler.RecoveryLimit = 5;
            ExceptionHandler.ModName = modName;

            verRegData = new MasterRegData(new MasterVersionData(this, ExceptionHandler.ModName, modID, apiVID, versionID), VersionRegisteredCallback);
            regMessage = new MasterMessage(this, (byte)ModQueryTypes.RegisterInstance, verRegData);
            versionPriorityList = new List<MasterRegEntry>();
            hasPriority = false;

            clients = new List<ModClient>();
        }

        protected override void AfterLoadData()
        {
            priorityVersion = default(MasterRegEntry);
            SendModMessage(regMessage);

            // If another instance completed this phase first, the above query will get a response, and VersionRegisteredCallback()
            // will set priorityVersion. That oldest instance will take priority until they can agree which one
            // gets priority, sorting first by version, then load order. The oldest instance of the newest version will 
            // then take over query handling, and the others will unload themselves.
            if (priorityVersion?.instance == null)
                RegisterVersionHandler();
            else
            {
                ExceptionHandler.ModName = $"{modName} - Duplicate";
                ExceptionHandler.WriteToLogAndConsole($"Another RHM instance was detected. Deferring to first instance for version negotiation...");
            }
        }

        private void RegisterVersionHandler()
        {
            priorityVersion = new MasterRegEntry(verRegData);
            versionPriorityList.Add(priorityVersion);
            priorityIndex = -1;

            ExceptionHandler.WriteToLogAndConsole($"Initializing RHM version {versionString}", true);
            MyAPIUtilities.Static.RegisterMessageHandler(modID, VersionHandler);
        }

        private void UnregisterVersionHandler() =>
            MyAPIUtilities.Static.UnregisterMessageHandler(modID, VersionHandler);

        private void SendModMessage(MasterMessage query) =>
            MyAPIUtilities.Static.SendModMessage(modID, query);

        /// <summary>
        /// Handles prioritzation of RHM multiple instances
        /// </summary>
        private void VersionHandler(object message)
        {
            if (message is MasterMessage)
            {
                var msg = (MasterMessage)message;

                // If request sender exists and isn't you, reply
                if (msg.Item1 != this && msg.Item1 != null)
                {
                    switch ((ModQueryTypes)msg.Item2)
                    {
                        // Build list of RHM versions to prioritize and inform them of the instance handling negotiation
                        case ModQueryTypes.RegisterInstance:
                        {
                                if (!hasPriority)
                                {
                                    var instanceVersion = (MasterRegData)msg.Item3;
                                    versionPriorityList.Add(new MasterRegEntry(instanceVersion));

                                    var response = new MasterMessage(this, (byte)ModQueryTypes.ConfirmRegistration, priorityVersion.GetData());
                                    var RegCallback = instanceVersion.Item2;
                                    RegCallback(response);
                                }
                                else
                                {
                                    var instanceVersion = (MasterRegData)msg.Item3;
                                    var response = new MasterMessage(this, (byte)ModQueryTypes.RejectRegistration, priorityVersion.GetData());
                                    var RegCallback = instanceVersion.Item2;
                                    RegCallback(response);
                                }
                                break;
                        }
                        // Retrieve final prioritized instance and/or transfer priority
                        case ModQueryTypes.GetPriorityInstance:
                            {
                                var SetVersion = (SetVersionFunc)msg.Item3;
                                SetVersion(GetPriorityInstance().GetData());
                                break;
                            }
                        default:
                            ExceptionHandler.WriteToLogAndConsole("Unexpected response recieved to RHM version negotiation message.", true);
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Negotiates with the instance managing RHM version selection
        /// </summary>
        private void VersionRegisteredCallback(object message)
        {
            if (message is MasterMessage)
            {
                var msg = (MasterMessage)message;

                if (msg.Item1 != this && msg.Item1 != null)
                {
                    switch ((ModQueryTypes)msg.Item2)
                    {
                        // Another instance is managing prioritization, waiting for completion
                        case ModQueryTypes.ConfirmRegistration:
                            {
                                var instanceVersion = (MasterRegData)msg.Item3;
                                priorityVersion = new MasterRegEntry(instanceVersion);
                                break;
                            }
                        // This instance loaded too late for some reason
                        case ModQueryTypes.RejectRegistration:
                            {
                                var instanceVersion = (MasterRegData)msg.Item3;
                                priorityVersion = new MasterRegEntry(instanceVersion);
                                ExceptionHandler.WriteToLogAndConsole("Registration refused. Another RHM instance is already running. Aborting startup...", true);
                                break;
                            }
                        // The original instance has deferred control to this one, assume control
                        case ModQueryTypes.TransferInstancePriority:
                            var newPriority = new MasterRegEntry((MasterRegData)msg.Item3);

                            if (newPriority.instance == this && newPriority.instance != priorityVersion.instance)
                            {
                                ExceptionHandler.WriteToLogAndConsole($"RHM version {versionString} now has priority.");
                                priorityVersion = newPriority;
                                RegisterVersionHandler();
                            }

                            break;
                        default:
                            ExceptionHandler.WriteToLogAndConsole("Unexpected response recieved to RHM version negotiation message.", true);
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Returns the RHM instance that has priority if this instance is managing prioritization.
        /// If this instance is awaiting priority from another instance, it will return null.
        /// </summary>
        private MasterRegEntry GetPriorityInstance()
        {
            // If more than one version is present, choose the first instance of the newest version
            if (priorityIndex == -1 && versionPriorityList.Count > 1)
            {
                int lastIndex = 0;
                ulong lastVersion = (ulong)versionID.X << 48 | (ulong)versionID.Y << 32 | (ulong)versionID.Z << 16 | (ulong)versionID.W;

                for (int i = 1; i < versionPriorityList.Count; i++)
                {
                    var b = versionPriorityList[i];
                    ulong version = (ulong)b.versionID.X << 48 | (ulong)b.versionID.Y << 32 | (ulong)b.versionID.Z << 16 | (ulong)b.versionID.W;

                    if (version > lastVersion)
                    {
                        lastIndex = i;
                        lastVersion = version;
                    }
                }

                priorityIndex = lastIndex;
            }
            else
                priorityIndex = 0;

            ExceptionHandler.WriteToLogAndConsole($"Priority Index: {priorityIndex}", true);
            return versionPriorityList[priorityIndex];
        }

        /// <summary>
        /// Returns true if the instance has or can assume priority as the sole instance of RHM.
        /// </summary>
        private bool GetCanInitialize()
        {
            // If this was the first instance initialized, it will have priority by default
            if (priorityVersion.instance == this)
            {
                priorityVersion = GetPriorityInstance();

                if (priorityVersion.instance != this)
                {
                    ExceptionHandler.WriteToLogAndConsole("Newer RHM version found. Transferring priority...");
                    var transferMsg = new MasterMessage(this, (byte)ModQueryTypes.TransferInstancePriority, priorityVersion.GetData());
                    priorityVersion.CallbackAction(transferMsg);
                    MyAPIUtilities.Static.UnregisterMessageHandler(modID, VersionHandler);
                }
            }
            else
            {
                SetVersionFunc SetVersionCallback = (versionData) => { priorityVersion = new MasterRegEntry(versionData); };
                SendModMessage(new MasterMessage(this, (byte)ModQueryTypes.GetPriorityInstance, SetVersionCallback));
            }

            return priorityVersion.instance == this;
        }

        protected override void AfterInit()
        {
            if (GetCanInitialize())
            {
                ExceptionHandler.ModName = modName;
                hasPriority = true;
                _commands = CmdManager.GetOrCreateGroup("/rhd", GetChatCommands());

                RhServer.Init();
                BlacklistManager.Init();

                if (ExceptionHandler.IsClient)
                {
                    FontManager.Init();
                    BindManager.Init();
                    MasterConfig.Load(true);

                    HudMain.Init();
                    RebindDialog.Init();
                    RichHudDebug.Init();
                    InitSettingsMenu();

                    RegisterMessageHandler();
                    CheckClientQueue();

                    if (MenuUtilities.CanAddElements)
                        MenuUtilities.AddMenuElements(GetModMenuButton());
                }
            }
            else // Another instance has priority, abort
            {
                ExceptionHandler.ModName = $"{modName} - Duplicate";
                ExceptionHandler.WriteToLogAndConsole($"Another RHM instance has taken priority. Shutting down...");
                Close();
            }
        }

        /// <summary>
        /// Registers the callback method for client registration and inter-mod messaging.
        /// </summary>
        private void RegisterMessageHandler() =>
            MyAPIUtilities.Static.RegisterMessageHandler(modID, MessageHandler);

        /// <summary>
        /// Unegisters the callback method for client registration and inter-mod messaging.
        /// </summary>
        private void UnregisterMessageHandler() =>
            MyAPIUtilities.Static.UnregisterMessageHandler(modID, MessageHandler);

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
        private void MessageHandler(object message)
        {
            if (message is ExtendedClientData)
                RegisterClient((ExtendedClientData)message);
            else if (message is ClientData)
                RegisterClient((ClientData)message);
        }

        private void RegisterClient(ExtendedClientData regMessage)
        {
            ModClient client = RegisterClient(regMessage.Item1);

            if (client != null)
                client.RegisterExtendedAccessors(regMessage.Item2, regMessage.Item3);
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
            if (hasPriority)
            {
                UnregisterVersionHandler();
                MasterConfig.Save();
            }

            UnregisterMessageHandler();
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