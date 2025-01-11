using RichHudFramework;
using RichHudFramework.UI;
using RichHudFramework.Internal;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage;
using VRage.ModAPI;
using SecureMsgHandler = System.Action<ushort, byte[], ulong, bool>;

namespace RichHudFramework.Server
{
    /// <summary>
    /// Used to get/send information from/to the server, dedicated or not.
    /// </summary>
    public sealed partial class RhServer : ModBase.ModuleBase
    {
        private const ushort serverHandlerID = 50972;
        private static RhServer instance;

        private readonly List<SecureMessage> incomingMessages;
        private readonly List<MyTuple<ulong, ServerMessage>> receivedClientMessages;
        private readonly List<ReplyMessage> receivedServerMessages;

        private readonly List<MyTuple<ulong, List<ReplyMessage>>> serverOutgoing;
        private readonly ObjectPool<List<ReplyMessage>> replyListPool;

        private readonly List<ServerMessage> clientOutgoing;
        private readonly CallbackManager callbackManager;

        private SecureMsgHandler messageHandler;

        private RhServer() : base(true, true, RichHudMaster.Instance)
        {
            clientOutgoing = new List<ServerMessage>();

            incomingMessages = new List<SecureMessage>();
            receivedClientMessages = new List<MyTuple<ulong, ServerMessage>>();
            receivedServerMessages = new List<ReplyMessage>();

            serverOutgoing = new List<MyTuple<ulong, List<ReplyMessage>>>();
            replyListPool = new ObjectPool<List<ReplyMessage>>(GetNewReplyList, ResetReplyList);

            if (ExceptionHandler.IsClient)
                callbackManager = new CallbackManager();
            else
                callbackManager = null;
        }

        public static void Init()
        {
            if (instance == null)
            {
                instance = new RhServer();
            }

            instance.messageHandler = new SecureMsgHandler(instance.NetworkMessageHandler);
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(serverHandlerID, instance.messageHandler);
        }

        /// <summary>
        /// Release resources
        /// </summary>
        public override void Close()
        {
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(serverHandlerID, messageHandler);
            instance = null;
        }

        /// <summary>
        /// Sends an action to be executed on the server
        /// </summary>
        public static void SendActionToServer(ServerActions actionID, byte[] message, Action<byte[]> callback = null, bool uniqueCallback = true)
        {
            int callbackID = -1;

            if ((actionID & ServerActions.RequireReply) == ServerActions.RequireReply)
            {
                if (callback != null)
                {
                    callbackID = instance.callbackManager.RegisterCallback(callback, uniqueCallback);

                    if (callbackID == -1)
                        return;
                }
                else
                    throw new Exception($"Callback missing for {actionID}");
            }
            
            instance.clientOutgoing.Add(new ServerMessage(actionID, message, callbackID));
        }

        /// <summary>
        /// Receives serialized data sent over the network
        /// </summary>
        private void NetworkMessageHandler(ushort id, byte[] message, ulong plyID, bool sentFromServer)
        {
            if (ExceptionHandler.IsServer || (ExceptionHandler.IsClient && sentFromServer))
            {
                incomingMessages.Add(new SecureMessage(sentFromServer, id, plyID, message));
            }
        }

        public override void Update()
        {
            if (ExceptionHandler.IsClient)
                SendMessagesToServer();

            if (ExceptionHandler.IsServer)
                SendMessagesToClient();

            ParseIncommingMessages();

            if (ExceptionHandler.IsServer)
                ProcessMessagesFromClient();

            if (ExceptionHandler.IsClient)
                ProcessMessagesFromServer();
        }

        /// <summary>
        /// Serializes client messages and sends them to the server
        /// </summary>
        private void SendMessagesToServer()
        {
            if (clientOutgoing.Count > 0)
            {
                ExceptionHandler.WriteToLogAndConsole($"Sending {clientOutgoing.Count} message(s) to server.", true);

                byte[] bin;
                KnownException exception = Utils.ProtoBuf.TrySerialize(clientOutgoing, out bin);

                if (exception == null)
                    exception = Utils.ProtoBuf.TrySerialize(new MessageContainer(false, bin), out bin);

                if (exception == null)
                    MyAPIGateway.Multiplayer.SendMessageToServer(serverHandlerID, bin);
                else
                    ExceptionHandler.WriteToLogAndConsole($"Unable to serialize server message: {exception}");

                clientOutgoing.Clear();
            }
        }

        /// <summary>
        /// Serializes server replies and sends them to the appropriate clients
        /// </summary>
        private void SendMessagesToClient()
        {
            if (serverOutgoing.Count > 0)
            {
                ExceptionHandler.WriteToLogAndConsole($"Sending {serverOutgoing.Count} message(s) to clients.", true);

                foreach (var clientMessages in serverOutgoing)
                {
                    byte[] bin;
                    KnownException exception = Utils.ProtoBuf.TrySerialize(clientMessages.Item2, out bin);

                    if (exception == null)
                        exception = Utils.ProtoBuf.TrySerialize(new MessageContainer(true, bin), out bin);

                    if (exception == null)
                        MyAPIGateway.Multiplayer.SendMessageTo(serverHandlerID, bin, clientMessages.Item1);
                    else
                        ExceptionHandler.WriteToLogAndConsole($"Unable to serialize client message: {exception}");
                }

                // Reuse reply lists
                foreach (var list in serverOutgoing)
                    replyListPool.Return(list.Item2);

                serverOutgoing.Clear();
            }
        }

        /// <summary>
        /// Parses messages recieved into separate client and server message lists
        /// </summary>
        private void ParseIncommingMessages()
        {
            int errCount = 0;
            receivedServerMessages.Clear();
            receivedClientMessages.Clear();

            // Deserialize client messages and keep a running count of errors
            for (int i = 0; i < incomingMessages.Count; i++)
            {
                MessageContainer container;
                KnownException exception = Utils.ProtoBuf.TryDeserialize(incomingMessages[i].message, out container);

                if (exception != null)
                    errCount++;
                else if (container.isFromServer)
                {
                    ReplyMessage[] serverReplies;
                    exception = Utils.ProtoBuf.TryDeserialize(container.message, out serverReplies);

                    receivedServerMessages.AddRange(serverReplies);
                }
                else
                {
                    ServerMessage[] clientMessages;
                    exception = Utils.ProtoBuf.TryDeserialize(container.message, out clientMessages);

                    receivedClientMessages.EnsureCapacity(receivedClientMessages.Count + clientMessages.Length);

                    for (int j = 0; j < clientMessages.Length; j++)
                        receivedClientMessages.Add(new MyTuple<ulong, ServerMessage>(incomingMessages[i].plyID, clientMessages[j]));
                }

                if (exception != null)
                    errCount++;
            }

            if (incomingMessages.Count > 0)
                ExceptionHandler.WriteToLogAndConsole($"Recieved {incomingMessages.Count} message(s).", true);

            if (errCount > 0)
                ExceptionHandler.WriteToLogAndConsole($"Unable to parse {errCount} of {incomingMessages.Count} message(s).");

            incomingMessages.Clear();
        }

        /// <summary>
        /// Executes block actions specified in the parsed client message list
        /// </summary>
        private void ProcessMessagesFromClient()
        {
            foreach (var message in receivedClientMessages)
            {
                var actionID = (ServerActions)message.Item2.actionID;

                if ((actionID & ServerActions.BlacklistManager) == ServerActions.BlacklistManager)
                {
                    BlacklistMessage blacklistMessage;
                    long id = MyAPIGateway.Players.TryGetIdentityId(message.Item1);

                    ExceptionHandler.WriteToLogAndConsole($"Setting blacklist for {message.Item1}. IdentID: {id}", true);

                    if (id != 0 && Utils.ProtoBuf.TryDeserialize(message.Item2.message, out blacklistMessage) == null)
                    {
                        BlacklistManager.SetBlacklist(id, blacklistMessage.blacklist, blacklistMessage.value);
                    }                    
                }
            }
        }

        /// <summary>
        /// Processes replies from the server
        /// </summary>
        private void ProcessMessagesFromServer()
        {
            callbackManager.InvokeCallbacks(receivedServerMessages);
        }

        /// <summary>
        /// Adds server reply for a given client message
        /// </summary>
        private void AddServerReply<T>(MyTuple<ulong, ServerMessage> message, T dataIn, ref ulong? currentClient)
        {
            ulong clientID = message.Item1;
            ServerMessage clientMessage = message.Item2;
            var actionID = clientMessage.actionID;

            byte[] bin;
            KnownException exception = Utils.ProtoBuf.TrySerialize(dataIn, out bin);

            if (exception == null)
            {
                if (currentClient != clientID || currentClient == null)
                {
                    currentClient = clientID;
                    serverOutgoing.Add(new MyTuple<ulong, List<ReplyMessage>>(clientID, replyListPool.Get()));
                }

                var list = serverOutgoing[serverOutgoing.Count - 1].Item2;
                list.Add(new ReplyMessage(clientMessage.callbackID, bin));
            }
            else
                ExceptionHandler.WriteToLogAndConsole($"Failed to serialize client reply: {exception}");
        }

        private static List<ReplyMessage> GetNewReplyList()
        {
            return new List<ReplyMessage>();
        }

        private static void ResetReplyList(List<ReplyMessage> list)
        {
            list.Clear();
        }
    }
}
