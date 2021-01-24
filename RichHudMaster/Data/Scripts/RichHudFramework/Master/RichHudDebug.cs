using System;
using System.Collections.Generic;
using System.Diagnostics;
using VRage;

namespace RichHudFramework.Server
{
    using Internal;
    using UI;
    using UI.Rendering.Server;
    using UI.Server;
    using UI.Rendering;
    using ApiMemberAccessor = System.Func<object, int, object>;
    using ClientData = MyTuple<string, Action<int, object>, Action, int>;
    using ServerData = MyTuple<Action, Func<int, object>, int>;

    public sealed class RichHudDebug : RichHudComponentBase
    {
        public static bool EnableDebug { get; set; }

        private static RichHudDebug instance;
        private readonly DemoPage demoPage;
        private readonly TextPage statsText;
        private RichText statsBuilder;

        private readonly Stopwatch updateTimer;

        private RichHudDebug() : base(false, true)
        {
            if (instance == null)
                instance = this;
            else
                throw new Exception($"Only one instance of {GetType().Name} can exist at any given time.");

            demoPage = new DemoPage()
            {
                Name = "Demo",
                Enabled = false
            };

            statsText = new TextPage()
            {
                Name = "Statistics",
                HeaderText = "Debug Statistics",
                SubHeaderText = "",
                Enabled = false
            };

            statsText.TextBuilder.BuilderMode = TextBuilderModes.Lined;
            statsBuilder = new RichText();
            updateTimer = new Stopwatch();
            updateTimer.Start();

            RichHudTerminal.Root.Add(demoPage);
            RichHudTerminal.Root.Add(statsText);

            EnableDebug = false;
        }

        public static void Init()
        {
            if (instance == null)
                new RichHudDebug();
        }

        public override void Close()
        {
            instance = null;
        }

        public override void Draw()
        {
            demoPage.Enabled = EnableDebug;
            statsText.Enabled = EnableDebug;

            if (EnableDebug && updateTimer.ElapsedMilliseconds > 100)
            {
                IReadOnlyList<RichHudMaster.ModClient> modClients = RichHudMaster.Clients;
                IReadOnlyList<IFont> fonts = FontManager.Fonts;
                HudMain.TreeClient masterHud = HudMain.TreeManager.MainClient;
                BindManager.Client masterInput = BindManager.MainClient;

                statsBuilder.Clear();
                statsBuilder += $"Summary:\n";
                statsBuilder += $"\tCursor Visible: {HudMain.Cursor.Visible}\n";
                statsBuilder += $"\tMod Clients Registered: {modClients.Count}\n";
                statsBuilder += $"\tHudMain:\n";
                statsBuilder += $"\t\tHUD Spaces Registered: {HudMain.TreeManager.HudSpacesRegistered}\n";
                statsBuilder += $"\t\tElements Registered: {HudMain.TreeManager.ElementRegistered}\n";
                statsBuilder += $"\t\tClients Registered: {HudMain.TreeManager.Clients.Count}\n";

                long avgDrawTicks = HudMain.TreeManager.AvgDrawElapsedTicks,
                    avgInputTicks = HudMain.TreeManager.AvgInputElapsedTicks,
                    lastRebuildTicks = HudMain.TreeManager.RebuildElapsedTicks, 
                    timeSinceRebuildTicks = HudMain.TreeManager.TicksSinceLastRebuild;
                double avgDrawTime = avgDrawTicks / (double)TimeSpan.TicksPerMillisecond,
                    avgInputTime = avgInputTicks / (double)TimeSpan.TicksPerMillisecond,
                    lastRebuildTime = lastRebuildTicks / (double)TimeSpan.TicksPerMillisecond,
                    timeSinceRebuild = (timeSinceRebuildTicks / (double)TimeSpan.TicksPerMillisecond) / 1000d;

                statsBuilder += $"\t\tTimers (IsHighResolution: {Stopwatch.IsHighResolution}):\n";
                statsBuilder += $"\t\t\tAvgDraw:\t\t\t{avgDrawTime:G3}ms\n";
                statsBuilder += $"\t\t\tAvgInput:\t\t\t{avgInputTime:G3}ms\n";
                statsBuilder += $"\t\t\tTotal:\t\t\t\t{(avgDrawTime + avgInputTime):G3}ms\n\n";
                statsBuilder += $"\t\t\tLast Rebuild:\t\t{lastRebuildTime:G3}ms\t\t\n";
                statsBuilder += $"\t\t\tSince Rebuild:\t{timeSinceRebuild:G3}s\t\t\n\n";

                statsBuilder += $"\tBindManager:\n";
                statsBuilder += $"\t\tControls Registered: {BindManager.Controls.Count}\n";
                statsBuilder += $"\t\tClients Registered: {BindManager.Clients.Count}\n\n";
                statsBuilder += $"\tFontManager:\n";
                statsBuilder += $"\t\tFonts Registered: {fonts.Count}\n\n";

                foreach (IFont font in fonts)
                {
                    FontStyles supportedStyles = FontStyles.Italic | FontStyles.Underline;

                    if (font.IsStyleDefined(FontStyles.Bold))
                        supportedStyles |= FontStyles.Bold;

                    statsBuilder += $"\t\t{font.Name}\n";
                    statsBuilder += $"\t\t\tAtlas PtSize: {font.PtSize}\n";
                    statsBuilder += $"\t\t\tStyles: Regular, {supportedStyles}\n\n";
                }

                statsBuilder += $"Details:\n";
                statsBuilder += $"\tMaster:\n";

                GetHudStats(masterHud, statsBuilder);
                GetBindStats(masterInput, statsBuilder);

                foreach (RichHudMaster.ModClient modClient in modClients)
                {
                    statsBuilder += $"\n\t{modClient.name}:\n";
                    GetHudStats(modClient.hudClient, statsBuilder);
                    GetBindStats(modClient.bindClient, statsBuilder);
                }

                statsText.Text = statsBuilder;
                updateTimer.Restart();
            }
        }

        private static void GetHudStats(HudMain.TreeClient client, RichText statsBuilder)
        {
            statsBuilder += $"\t\tHudMain:\n";
            statsBuilder += $"\t\t\tEnable Cursor: {client.enableCursor}\n";
            statsBuilder += $"\t\t\tElements Registered: {client.UpdateAccessors.Count}\n\n";
        }

        private static void GetBindStats(BindManager.Client client, RichText statsBuilder)
        {
            IReadOnlyList<IBindGroup> bindGroups = client.Groups;

            statsBuilder += $"\t\tBindManager:\n";
            statsBuilder += $"\t\t\tGroups: {client.Groups.Count}\n";
            statsBuilder += $"\t\t\t|\tName\t\t\t\t\t|\tBinds\n";

            for (int i = 0; i < bindGroups.Count; i++)
            {
                statsBuilder += $"\t\t\t|\t{bindGroups[i].Name}";
                int tabCount = 5 - (bindGroups[i].Name.Length / 5);

                for (int j = 0; j < tabCount; j++)
                    statsBuilder.Add("\t");

                statsBuilder += $"|\t{bindGroups[i].Count}\n";
            }
        }
    }
}