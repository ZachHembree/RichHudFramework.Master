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
        private readonly UpdateStats stats;

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
                SubHeaderText = "Update Times and API Usage",
                Enabled = false
            };

            statsText.TextBuilder.BuilderMode = TextBuilderModes.Lined;
            statsBuilder = new RichText();
            updateTimer = new Stopwatch();
            updateTimer.Start();

            stats = new UpdateStats();

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

                stats.Update();

                statsBuilder.Clear();
                statsBuilder += $"Summary:\n";
                statsBuilder += $"\tCursor Visible: {HudMain.Cursor.Visible}\n";
                statsBuilder += $"\tClient Mods: {modClients.Count}\n";

                foreach (RichHudMaster.ModClient client in modClients)
                    statsBuilder += $"\t\t{client.name}\t\t|\tVersion: {client.VersionString}\t\t|\tSubtype: {client.ClientSubtype}\n";

                statsBuilder += $"\n\tHudMain:\n";
                statsBuilder += $"\t\tHUD Spaces Updating: {HudMain.TreeManager.HudSpacesRegistered}\n";
                statsBuilder += $"\t\tElements Updating: {HudMain.TreeManager.ElementRegistered}\n";
                statsBuilder += $"\t\tClients Registered: {HudMain.TreeManager.Clients.Count}\n";

                statsBuilder += $"\t\tUpdate Timers  (IsHighResolution: {Stopwatch.IsHighResolution}):\n";
                AddGrid(statsBuilder, new string[,]
                {
                    { "Name",   "Avg",                          "50th",                     "99th" },
                    { "Tree",   $"{stats.AvgTreeTime:F2}ms",    $"{stats.Tree50th:F2}ms",   $"{stats.Tree99th:F2}ms" },
                    { "Draw",   $"{stats.AvgDrawTime:F2}ms",    $"{stats.Draw50th:F2}ms",   $"{stats.Draw99th:F2}ms" },
                    { "Input",  $"{stats.AvgInputTime:F2}ms",   $"{stats.Input50th:F2}ms",  $"{stats.Input99th:F2}ms" },
                    { "Total",  $"{stats.AvgTotalTime:F2}ms",   $"{stats.Total50th:F2}ms",  $"{stats.Total99th:F2}ms" },
                }, 3, 4);

                statsBuilder += $"\n\tBindManager:\n";
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

            var groupGrid = new string[bindGroups.Count + 1, 2];
            groupGrid[0, 0] = $"Name";
            groupGrid[0, 1] = $"Binds";

            for (int i = 0; i < bindGroups.Count; i++)
            {
                groupGrid[i + 1, 0] = bindGroups[i].Name;
                groupGrid[i + 1, 1] = bindGroups[i].Count.ToString();
            }

            AddGrid(statsBuilder, groupGrid, 4, 5);
        }

        private static void AddGrid(RichText builder, string[,] grid, int leftPadding, int rowSpacing)
        {
            for (int i = 0; i < grid.GetLength(0); i++)
            {
                for (int k = 0; k < leftPadding; k++)
                    builder += "\t";

                for (int j = 0; j < grid.GetLength(1); j++)
                {
                    string entry = grid[i, j];
                    int tabStops = rowSpacing - (entry.Length / 3);

                    builder += $"|\t{entry}";

                    for (int k = 0; k < tabStops; k++)
                        builder += "\t";
                }

                builder += "\n";
            }
        }

        /// <summary>
        /// Used to generate statistics based on HUD update times
        /// </summary>
        private class UpdateStats
        {
            public double AvgTreeTime => treeStats.AvgTime;

            public double Tree50th => treeStats.Pct50th;

            public double Tree99th => treeStats.Pct99th;


            public double AvgDrawTime => drawStats.AvgTime;

            public double Draw50th => drawStats.Pct50th;

            public double Draw99th => drawStats.Pct99th;


            public double AvgInputTime => inputStats.AvgTime;

            public double Input50th => inputStats.Pct50th;

            public double Input99th => inputStats.Pct99th;


            public double AvgTotalTime => treeStats.AvgTime + drawStats.AvgTime + inputStats.AvgTime;

            public double Total50th => treeStats.Pct50th + drawStats.Pct50th + inputStats.Pct50th;

            public double Total99th => treeStats.Pct99th + drawStats.Pct99th + inputStats.Pct99th;

            private readonly TickStats drawStats, inputStats, treeStats;
            private readonly List<long> tickBuffer;

            public UpdateStats()
            {
                tickBuffer = new List<long>();
                drawStats = new TickStats();
                inputStats = new TickStats();
                treeStats = new TickStats();
            }

            public void Update()
            {
                treeStats.Update(HudMain.TreeManager.TreeElapsedTicks, tickBuffer);
                drawStats.Update(HudMain.TreeManager.DrawElapsedTicks, tickBuffer);
                inputStats.Update(HudMain.TreeManager.InputElapsedTicks, tickBuffer);
            }

            public class TickStats
            {
                public double AvgTime { get; private set; }

                public double Pct50th { get; private set; }

                public double Pct99th { get; private set; }

                public void Update(IReadOnlyList<long> ticks, List<long> tickBuffer)
                {
                    double tpms = TimeSpan.TicksPerMillisecond;

                    tickBuffer.Clear();
                    tickBuffer.AddRange(ticks);
                    tickBuffer.Sort();

                    long totalTicks = 0;

                    for (int n = 0; n < tickBuffer.Count; n++)
                        totalTicks += tickBuffer[n];

                    AvgTime = (totalTicks / (double)tickBuffer.Count) / tpms;
                    Pct50th = tickBuffer[(int)(tickBuffer.Count * 0.5d)] / tpms;
                    Pct99th = tickBuffer[(int)(tickBuffer.Count * 0.99d)] / tpms;
                }
            }
        }
    }
}