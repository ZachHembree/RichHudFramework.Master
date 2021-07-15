using System;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

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
        private readonly TerminalPageCategory pageCategory;
        private readonly TextPage statsText;
        private StringBuilder statsBuilder;

        private readonly Stopwatch updateTimer;
        private readonly UpdateStats stats;
        private readonly TextBoard overlay;
        private bool enableOverlay;
        private Vector2 overlayPos;

        private RichHudDebug() : base(false, true)
        {
            if (instance == null)
                instance = this;
            else
                throw new Exception($"Only one instance of {GetType().Name} can exist at any given time.");

            statsText = new TextPage()
            {
                Name = "Statistics",
                HeaderText = "Debug Statistics",
                SubHeaderText = "Update Times and API Usage",
            };

            statsText.TextBuilder.BuilderMode = TextBuilderModes.Lined;
            statsBuilder = new StringBuilder();
            updateTimer = new Stopwatch();
            updateTimer.Start();

            stats = new UpdateStats();

            overlayPos = new Vector2(-0.5f, 0.5f);
            enableOverlay = false;
            EnableDebug = false;

            overlay = new TextBoard() 
            {
                AutoResize = true,
                BuilderMode = TextBuilderModes.Lined,
                Scale = 0.8f,
                Format = new GlyphFormat(new Color(255, 191, 0))
            };

            pageCategory = new TerminalPageCategory() 
            {
                Name = "Debug",
                Enabled = false,
                PageContainer = 
                {
                    new DemoPage()
                    {
                        Name = "Demo",
                    }, 

                    statsText,

                    new ControlPage()
                    {
                        Name = "Settings",
                        CategoryContainer = 
                        {
                            new ControlCategory()
                            {
                                HeaderText = "Debug Settings",
                                SubheaderText = "",
                                TileContainer = 
                                {
                                    new ControlTile()
                                    {
                                        new TerminalCheckbox()
                                        {
                                            Name = "Enable Overlay",
                                            Value = enableOverlay,
                                            ControlChangedHandler = (obj, args) =>
                                            {
                                                var element = obj as TerminalCheckbox;
                                                enableOverlay = element.Value;
                                            }
                                        },
                                        new TerminalDragBox()
                                        {
                                            Name = "Set Overlay Pos",
                                            Value = overlayPos,
                                            AlignToEdge = true,
                                            ControlChangedHandler = (obj, args) =>
                                            {
                                                var element = obj as TerminalDragBox;
                                                overlayPos = element.Value;
                                            }
                                        }
                                    },
                                    new ControlTile()
                                    {
                                        new TerminalCheckbox()
                                        {
                                            Name = "Blacklist All Input",
                                            Value = BindManager.SeMouseControlsBlacklisted,
                                            CustomValueGetter = () => BindManager.SeControlsBlacklisted,
                                            ControlChangedHandler = (obj, args) =>
                                            {
                                                var element = obj as TerminalCheckbox;
                                                BindManager.SeControlsBlacklisted = element.Value;
                                            }
                                        },
                                        new TerminalCheckbox()
                                        {
                                            Name = "Blacklist Mouse Input",
                                            Value = BindManager.SeMouseControlsBlacklisted,
                                            CustomValueGetter = () => BindManager.SeMouseControlsBlacklisted,
                                            ControlChangedHandler = (obj, args) =>
                                            {
                                                var element = obj as TerminalCheckbox;
                                                BindManager.SeMouseControlsBlacklisted = element.Value;
                                            }
                                        },
                                    }
                                }
                            }
                        }
                    }
                }
            };

            RichHudTerminal.Root.Add(pageCategory);
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
            pageCategory.Enabled = EnableDebug;

            if (EnableDebug && updateTimer.ElapsedMilliseconds > 100)
            {
                IReadOnlyList<RichHudMaster.ModClient> modClients = RichHudMaster.Clients;
                IReadOnlyList<IFont> fonts = FontManager.Fonts;
                HudMain.TreeClient masterHud = HudMain.TreeManager.MainClient;
                BindManager.Client masterInput = BindManager.MainClient;

                stats.Update();
                statsBuilder.Clear();

                var vID = RichHudMaster.versionID;
                statsBuilder.Append($"Rich HUD Master (v{vID.X}.{vID.Y}.{vID.Z}.{vID.W})\n");
                statsBuilder.Append($"Summary:\n");
                statsBuilder.Append($"\tCursor Visible: {HudMain.Cursor.Visible}\n");
                statsBuilder.Append($"\tClient Mods: {modClients.Count}\n");

                foreach (RichHudMaster.ModClient client in modClients)
                    statsBuilder.Append($"\t\t{client.name}\t\t|\tVersion: {client.VersionString}\t\t|\tSubtype: {client.ClientSubtype}\n");

                statsBuilder.Append($"\n\tHudMain:\n");
                statsBuilder.Append($"\t\tHUD Spaces Updating: {HudMain.TreeManager.HudSpacesRegistered}\n");
                statsBuilder.Append($"\t\tElements Updating: {HudMain.TreeManager.ElementRegistered}\n");
                statsBuilder.Append($"\t\tClients Registered: {HudMain.TreeManager.Clients.Count}\n");

                statsBuilder.Append($"\t\tUpdate Timers  (IsHighResolution: {Stopwatch.IsHighResolution}):\n");
                AddGrid(statsBuilder, new string[,]
                {
                    { "Name",   "Avg",                          "50th",                     "99th" },
                    { "Draw",   $"{stats.AvgDrawTime:F2}ms",    $"{stats.Draw50th:F2}ms",   $"{stats.Draw99th:F2}ms" },
                    { "Input",  $"{stats.AvgInputTime:F2}ms",   $"{stats.Input50th:F2}ms",  $"{stats.Input99th:F2}ms" },
                    { "Total",  $"{stats.AvgTotalTime:F2}ms",   $"{stats.Total50th:F2}ms",  $"{stats.Total99th:F2}ms" },
                    { "Tree*",   $"{stats.AvgTreeTime:F2}ms",    $"{stats.Tree50th:F2}ms",   $"{stats.Tree99th:F2}ms" },
                }, 3, 4);

                overlay.SetText(statsBuilder);
                
                var cursor = HudMain.Cursor as HudMain.HudCursor;

                statsBuilder.Append($"\n\tCursor:\n");
                statsBuilder.Append($"\t\tVisible: {cursor.Visible}\n");
                statsBuilder.Append($"\t\tCaptured: {cursor.IsCaptured}\n");

                if (cursor.IsCaptured)
                {
                    statsBuilder.Append($"\t\tPosition: {cursor.ScreenPos}\n");

                    var modName = cursor.CapturedElement(null, (int)HudElementAccessors.ModName) as string ?? "None";
                    var type = cursor.CapturedElement(null, (int)HudElementAccessors.GetType) as Type;
                    var ZOffset = (sbyte)cursor.CapturedElement(null, (int)HudElementAccessors.ZOffset);
                    var fullZOffset = (ushort)cursor.CapturedElement(null, (int)HudElementAccessors.FullZOffset);
                    var pos = (Vector2)cursor.CapturedElement(null, (int)HudElementAccessors.Position);
                    var size = (Vector2)cursor.CapturedElement(null, (int)HudElementAccessors.Size);

                    statsBuilder.Append($"\t\t\tMod: {modName}\n");
                    statsBuilder.Append($"\t\t\tType: {type}\n");
                    statsBuilder.Append($"\t\t\tZOffset: {ZOffset}\n");
                    statsBuilder.Append($"\t\t\tFullZOffset: {fullZOffset}\n");
                    statsBuilder.Append($"\t\t\tPosition: {pos}\n");
                    statsBuilder.Append($"\t\t\tSize: {size}\n");
                }

                statsBuilder.Append($"\n\tBindManager:\n");
                statsBuilder.Append($"\t\tControls Registered: {BindManager.Controls.Count}\n");
                statsBuilder.Append($"\t\tClients Registered: {BindManager.Clients.Count}\n\n");

                statsBuilder.Append($"\tFontManager:\n");
                statsBuilder.Append($"\t\tFonts Registered: {fonts.Count}\n\n");

                foreach (IFont font in fonts)
                {
                    FontStyles supportedStyles = FontStyles.Italic | FontStyles.Underline;

                    if (font.IsStyleDefined(FontStyles.Bold))
                        supportedStyles |= FontStyles.Bold;

                    statsBuilder.Append($"\t\t{font.Name}\n");
                    statsBuilder.Append($"\t\t\tAtlas PtSize: {font.PtSize}\n");
                    statsBuilder.Append($"\t\t\tStyles: Regular, {supportedStyles}\n\n");
                }

                statsBuilder.Append($"Details:\n");
                statsBuilder.Append($"\tMaster:\n");

                GetHudStats(masterHud, statsBuilder);
                GetBindStats(masterInput, statsBuilder);

                foreach (RichHudMaster.ModClient modClient in modClients)
                {
                    statsBuilder.Append($"\n\t{modClient.name}:\n");
                    GetHudStats(modClient.hudClient, statsBuilder);
                    GetBindStats(modClient.bindClient, statsBuilder);
                }

                statsText.Text = statsBuilder;
                updateTimer.Restart();
            }

            if (EnableDebug && enableOverlay)
            {
                var screenRes = new Vector2(HudMain.ScreenWidth, HudMain.ScreenHeight);
                var offset = HudMain.GetPixelVector(overlayPos);

                if (offset.X < 0f)
                    offset.X += overlay.Size.X / 2f;
                else
                    offset.X -= overlay.Size.X / 2f;

                if (offset.Y < 0f)
                    offset.Y += overlay.Size.Y / 2f;
                else
                    offset.Y -= overlay.Size.Y / 2f;

                overlay.Scale = 0.8f * HudMain.ResScale;
                overlay.Draw(offset, HudMain.PixelToWorld);
            }
        }

        private static void GetHudStats(HudMain.TreeClient client, StringBuilder statsBuilder)
        {
            statsBuilder.Append($"\t\tHudMain:\n");
            statsBuilder.Append($"\t\t\tEnable Cursor: {client.enableCursor}\n");
            statsBuilder.Append($"\t\t\tElements Updating: {client.UpdateAccessors.Count}\n\n");
        }

        private static void GetBindStats(BindManager.Client client, StringBuilder statsBuilder)
        {
            IReadOnlyList<IBindGroup> bindGroups = client.Groups;

            statsBuilder.Append($"\t\tBindManager:\n");
            statsBuilder.Append($"\t\t\tGroups: {client.Groups.Count}\n");

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

        private static void AddGrid(StringBuilder builder, string[,] grid, int leftPadding, int rowSpacing)
        {
            for (int i = 0; i < grid.GetLength(0); i++)
            {
                for (int k = 0; k < leftPadding; k++)
                    builder.Append("\t");

                for (int j = 0; j < grid.GetLength(1); j++)
                {
                    string entry = grid[i, j];
                    int tabStops = rowSpacing - (entry.Length / 3);

                    builder.Append($"|\t{entry}");

                    for (int k = 0; k < tabStops; k++)
                        builder.Append("\t");
                }

                builder.Append("\n");
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


            public double AvgTotalTime => drawStats.AvgTime + inputStats.AvgTime;

            public double Total50th => drawStats.Pct50th + inputStats.Pct50th;

            public double Total99th => drawStats.Pct99th + inputStats.Pct99th;

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