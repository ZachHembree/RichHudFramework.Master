using System;
using ProtoBuf;

namespace RichHudFramework.Server
{
    [Flags]
    public enum ServerActions : ulong
    {
        None =                      0x00000000,
        Action =                    0x00000001,

        SetBlacklist =              0x00010000 | Action | BlacklistManager,

        BlacklistManager =          0x02000000,
        GetOrSendData =             0x04000000,
        RequireReply =              0x08000000  | GetOrSendData,
    }

    public sealed partial class RhServer
    {
        [ProtoContract]
        private struct SecureMessage
        {
            [ProtoMember(1)]
            public bool sentFromServer;

            [ProtoMember(2)]
            public ushort id;

            [ProtoMember(3)]
            public ulong plyID;

            [ProtoMember(4)]
            public byte[] message;

            public SecureMessage(bool sentFromServer, ushort id, ulong plyID, byte[] message)
            {
                this.sentFromServer = sentFromServer;
                this.id = id;
                this.plyID = plyID;
                this.message = message;
            }
        }

        /// <summary>
        /// Shared message type for servers and clients
        /// </summary>
        [ProtoContract]
        private struct MessageContainer
        {
            [ProtoMember(1)]
            public bool isFromServer;

            [ProtoMember(2)]
            public byte[] message;

            public MessageContainer(bool isFromServer, byte[] message)
            {
                this.isFromServer = isFromServer;
                this.message = message;
            }
        }

        /// <summary>
        /// Messages sent from clients to the server
        /// </summary>
        [ProtoContract]
        private struct ServerMessage
        {
            [ProtoMember(1)]
            public int callbackID;

            [ProtoMember(2)]
            public ulong actionID;
            
            [ProtoMember(3)]
            public byte[] message;

            public ServerMessage(ServerActions actionID, byte[] message, int callbackID)
            {
                this.actionID = (ulong)actionID;
                this.callbackID = callbackID;
                this.message = message;
            }
        }

        /// <summary>
        /// Replies sent from servers to clients
        /// </summary>
        [ProtoContract]
        private struct ReplyMessage
        {
            [ProtoMember(1)]
            public int callbackID;

            [ProtoMember(2)]
            public byte[] message;

            public ReplyMessage(int callbackID, byte[] data)
            {
                this.callbackID = callbackID;
                this.message = data;
            }
        }
    }
}
