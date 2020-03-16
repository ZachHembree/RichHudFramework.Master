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
        private List<CmdManager.Command> GetChatCommands()
        {
            return new List<CmdManager.Command>
            {
                new CmdManager.Command("resetBinds",
                    () => MasterBinds.Cfg = BindsConfig.Defaults),
                new CmdManager.Command ("save",
                    () => MasterConfig.SaveStart()),
                new CmdManager.Command ("load",
                    () => MasterConfig.LoadStart()),
                new CmdManager.Command("resetConfig",
                    () => MasterConfig.ResetConfig()),
                new CmdManager.Command ("open",
                    () => RichHudTerminal.Open = true),
                new CmdManager.Command ("close",
                    () => RichHudTerminal.Open = false),
                new CmdManager.Command ("crash",
                    ThrowException),
            };
        }

        private static void ThrowException()
        {
            throw new Exception("Crash chat command was called");
        }
    }
}