using RichHudFramework.Internal;
using RichHudFramework.UI;
using RichHudFramework.UI.Server;
using RichHudFramework;
using System;
using System.Collections.Generic;
using System.Text;
using VRage;
using VRageMath;

namespace RichHudFramework.Server
{
    public sealed partial class RichHudMaster
    {
        private CmdGroupInitializer GetChatCommands()
        {
            return new CmdGroupInitializer
            {
                { "resetBinds", x => MasterBinds.Cfg = BindsConfig.Defaults },
                { "save", x => MasterConfig.SaveStart() },
                { "load", x => MasterConfig.LoadStart() },
                { "resetConfig", x => MasterConfig.ResetConfig()},
                { "open", x => RichHudTerminal.Open = true},
                { "close", x => RichHudTerminal.Open = false },
                { "toggleCursor", x => HudMain.EnableCursor = !HudMain.EnableCursor },
                { "crash", x => ThrowException()},
            };
        }

        private static void ThrowException()
        {
            throw new Exception("Crash chat command was called");
        }
    }
}