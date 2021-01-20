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
        private DemoPage demoTerminalPage;

        private void InitSettingsMenu()
        {
            RichHudTerminal.Root.Enabled = true;

            demoTerminalPage = new DemoPage()
            {
                Name = "Demo",
                Enabled = false
            };

            RichHudTerminal.Root.AddRange(new TerminalPageBase[] 
            {
                new RebindPage()
                {
                    Name = "Binds",
                    GroupContainer = { { MasterBinds.BindGroup, BindsConfig.DefaultBinds } }
                },
                demoTerminalPage,
            });
        }
    }
}