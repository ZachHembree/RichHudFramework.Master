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
        private void InitSettingsMenu()
        {
            RichHudTerminal.Root.Enabled = true;
            RichHudTerminal.Root.AddRange(new TerminalPageBase[] 
            {
                new RebindPage()
                {
                    Name = "Binds",
                    GroupContainer = { { MasterBinds.BindGroup, BindsConfig.DefaultBinds } }
                },
                new ControlPage()
                {
                    Name = "Test Page",
                    CategoryContainer = 
                    {
                        new ControlCategory()
                        {
                            new ControlTile()
                            {
                                new TerminalDropdown<int>()
                                {
                                    List =
                                    {
                                        { "Entry 1", 0 },
                                        { "Entry 2", 0 },
                                        { "Entry 3", 0 },
                                        { "Entry 4", 0 },
                                        { "Entry 5", 0 },
                                        { "Entry 6", 0 },
                                        { "Entry 7", 0 },
                                    }
                                },
                                new TerminalList<int>()
                                {
                                    List =
                                    {
                                        { "Entry 1", 0 },
                                        { "Entry 2", 0 },
                                        { "Entry 3", 0 },
                                        { "Entry 4", 0 },
                                        { "Entry 5", 0 },
                                        { "Entry 6", 0 },
                                        { "Entry 7", 0 },
                                    }
                                }
                            },
                            new ControlTile()
                            {
                                new TerminalColorPicker()
                            }
                        }
                    }
                }
            });
        }
    }
}