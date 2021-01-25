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
                { "open", x => RichHudTerminal.OpenMenu()},
                { "close", x => RichHudTerminal.CloseMenu() },
                { "toggleCursor", x => HudMain.EnableCursor = !HudMain.EnableCursor },
                { "crash", x => ThrowException()},
                { "refreshDrawList", x => HudMain.RefreshDrawList = true },
                { "toggleDebug", x => RichHudDebug.EnableDebug = !RichHudDebug.EnableDebug }
            };
        }

        private static void ThrowException()
        {
            throw new Exception("Crash chat command was called");
        }
    }
}