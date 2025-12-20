using ProtoBuf;
using RichHudFramework.Internal;
using RichHudFramework.UI.Server;
using Sandbox.Game;

namespace RichHudFramework.Server
{
    internal class BlacklistManager : ModBase.ModuleBase
    {
        private static BlacklistManager instance;

        public BlacklistManager() : base(true, false, RichHudMaster.Instance)
        { }

        public static void Init()
        {
            if (instance == null && ExceptionHandler.IsServer)
            {
                instance = new BlacklistManager();
            }
        }

        /// <summary>
        /// Release resources
        /// </summary>
        public override void Close()
        {
            instance = null;
        }

        public static void SetBlacklist(long identID, BindRange[] blacklist, bool value)
        {
            instance.SetBlacklistInternal(identID, blacklist, value);
        }

        private void SetBlacklistInternal(long identID, BindRange[] ranges, bool value)
        {
            if (ranges == null) return;

            for (int n = 0; n < ranges.Length; n++)
            {
                BindRange range = ranges[n];
                int end = range.Start + range.Count;

                for (int i = range.Start; i < end; i++)
                {
                    // Direct index access - no search or mapping needed
                    if (i >= 0 && i < BindManager.BuiltInBinds.Length)
                    {
                        string controlName = BindManager.BuiltInBinds[i];
                        MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlName, identID, !value);
                    }
                }
            }
        }
    }

    [ProtoContract]
    internal struct BindRange
    {
        [ProtoMember(1)]
        public int Start; // Starting index in BuiltInBinds

        [ProtoMember(2)]
        public int Count; // Number of contiguous elements

        public BindRange(int start, int count)
        {
            Start = start;
            Count = count;
        }
    }

    [ProtoContract]
    internal struct BlacklistMessage
    {
        [ProtoMember(1)]
        public BindRange[] ranges;

        [ProtoMember(2)]
        public bool value;

        public BlacklistMessage(BindRange[] ranges, bool value)
        {
            this.ranges = ranges;
            this.value = value;
        }
    }
}
