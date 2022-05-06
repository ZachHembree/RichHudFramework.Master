using RichHudFramework.Internal;
using RichHudFramework.UI;
using Sandbox.Game;
using Sandbox.ModAPI;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using VRage;
using VRage.Input;
using VRage.Utils;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;
using ProtoBuf;
using IMyControllableEntity = VRage.Game.ModAPI.Interfaces.IMyControllableEntity;

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

        public static void SetBlacklist(long identID, string[] blacklist, bool value)
        {
            instance.SetBlacklistInternal(identID, blacklist, value);
        }

        private void SetBlacklistInternal(long identID, string[] blacklist, bool value)
        {
            if (blacklist != null)
            {
                foreach (string control in blacklist)
                    MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(control, identID, !value);
            }
        }
    }

    [ProtoContract]
    internal struct BlacklistMessage 
    {
        [ProtoMember(1)]
        public string[] blacklist;

        [ProtoMember(2)]
        public bool value;

        public BlacklistMessage(string[] blacklist, bool value)
        {
            this.blacklist = blacklist;
            this.value = value;
        }
    }
}
