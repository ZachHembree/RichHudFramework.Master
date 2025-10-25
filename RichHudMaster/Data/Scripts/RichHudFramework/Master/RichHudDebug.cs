using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using VRage;
using VRageMath;

namespace RichHudFramework.Server
{
    using Internal;
    using UI;
    using UI.Rendering;
    using UI.Rendering.Server;
    using UI.Server;

    /// <summary>
    /// Manages debug information display and configuration for the Rich HUD Framework.
    /// </summary>
    public sealed class RichHudDebug : RichHudComponentBase
    {
        public static bool EnableDebug { get; set; }

        private static RichHudDebug instance;
        private readonly Stopwatch updateTimer;
        private readonly UpdateStats updateStats;

        private readonly TextPage statisticsPage;
        private readonly TerminalPageCategory debugCategory;

        private readonly TextBoard overlay;
        private readonly StringBuilder statsBuilder;
        private bool enableOverlay;
        private Vector2 overlayPos;

        private RichHudDebug() : base(false, true)
        {
            if (instance == null)
                instance = this;
            else
                throw new Exception($"Only one instance of {GetType().Name} can exist at any given time.");

            EnableDebug = false;
            updateStats = new UpdateStats();
            updateTimer = new Stopwatch();
            updateTimer.Start();

            statisticsPage = new TextPage()
            {
                Name = "Statistics",
                HeaderText = "Debug Statistics",
                SubHeaderText = "Update Times and API Usage",
            };
            statisticsPage.TextBuilder.BuilderMode = TextBuilderModes.Lined;
            debugCategory = CreateDebugCategory();

            overlay = new TextBoard()
            {
                AutoResize = true,
                BuilderMode = TextBuilderModes.Lined,
                Scale = 0.8f,
                Format = new GlyphFormat(new Color(255, 191, 0))
            };
            overlayPos = new Vector2(0.5f, 0.5f);
            enableOverlay = false;
            statsBuilder = new StringBuilder();
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

        public static void UpdateDisplay()
        {
            instance?.UpdateDisplayInternal();
        }

        public static TerminalPageCategory GetDebugCategory()
        {
            return instance?.debugCategory;
        }

        private TerminalPageCategory CreateDebugCategory()
        {
            var category = new TerminalPageCategory
            {
                Name = "Debug",
                Enabled = false
            };

            category.PageContainer.Add(statisticsPage);
            category.PageContainer.Add(CreateSettingsPage());
            return category;
        }

        private ControlPage CreateSettingsPage()
        {
            var settingsPage = new ControlPage
            {
                Name = "Settings"
            };

            var controlCategory = new ControlCategory
            {
                HeaderText = "Debug Settings",
                SubheaderText = ""
            };

            controlCategory.TileContainer.Add(CreateOverlayControlTile());
            controlCategory.TileContainer.Add(CreateCacheControlTile());
            settingsPage.CategoryContainer.Add(controlCategory);
            return settingsPage;
        }

        private ControlTile CreateOverlayControlTile()
        {
            return new ControlTile
            {
                new TerminalCheckbox
                {
                    Name = "Enable Overlay",
                    Value = enableOverlay,
                    ControlChangedHandler = (obj, args) =>
                    {
                        var checkbox = obj as TerminalCheckbox;
                        enableOverlay = checkbox.Value;
                    }
                },
                new TerminalDragBox
                {
                    Name = "Set Overlay Pos",
                    Value = overlayPos,
                    AlignToEdge = true,
                    ControlChangedHandler = (obj, args) =>
                    {
                        var dragBox = obj as TerminalDragBox;
                        overlayPos = dragBox.Value;
                    }
                },
                new TerminalButton()
                {
                    Name = "Tare Timers",
                    ControlChangedHandler = (obj, args) => { updateStats.Tare(); },
                    ToolTip = "Sets the current update times as the baseline."
                },
                new TerminalButton()
                {
                    Name = "Clear Tare",
                    ControlChangedHandler = (obj, args) => { updateStats.ClearTare(); },
                    ToolTip = "Resets timer tare to zero."
                }
            };
        }

        private ControlTile CreateCacheControlTile()
        {
            return new ControlTile
            {
                CreateCacheCheckbox("Text Cache", () => TextDiagnostics.LineTextCache.Enabled,
                    value => TextDiagnostics.LineTextCache.Enabled = value,
                    "Controls all text-related caching.\nRequired by glyph, typesetting and billboard caches."),
                CreateCacheCheckbox("Glyph Cache", () => TextDiagnostics.GlyphCache.Enabled,
                    value => TextDiagnostics.GlyphCache.Enabled = value,
                    "Controls caching of text font data.\nRequires text cache."),
                CreateCacheCheckbox("Typesetting Cache", () => TextDiagnostics.TypesettingCache.Enabled,
                    value => TextDiagnostics.TypesettingCache.Enabled = value,
                    "Controls caching for character placement within a UI element.\nRequires glyph and text caches."),
                CreateCacheCheckbox("Billboard Cache", () => TextDiagnostics.BBCache.Enabled,
                    value => TextDiagnostics.BBCache.Enabled = value,
                    "Controls caching of finalized billboard data used for text rendering.\nRequires typesetting, glyph and text caches.")
            };
        }

        private TerminalCheckbox CreateCacheCheckbox(string name, Func<bool> getValue, Action<bool> setValue, string toolTip)
        {
            return new TerminalCheckbox
            {
                Name = name,
                Value = getValue(),
                ControlChangedHandler = (obj, args) =>
                {
                    var checkbox = obj as TerminalCheckbox;
                    setValue(checkbox.Value);
                },
                ToolTip = toolTip
            };
        }

        private void UpdateDisplayInternal()
        {
            TextDiagnostics.Update();

            if (EnableDebug && (statisticsPage.Element.Visible || enableOverlay) && updateTimer.ElapsedMilliseconds > 100)
            {
                statsBuilder.Clear();
                AppendSummaryStats(statsBuilder);
                AppendHudMainStats(statsBuilder);
                AppendBillboardStats(statsBuilder);
                AppendUpdateTimerStats(statsBuilder);
                AppendTextCachingStats(statsBuilder);
                overlay.SetText(statsBuilder);

                if (statisticsPage.Element.Visible)
                {
                    AppendCursorStats(statsBuilder);
                    AppendBindManagerStats(statsBuilder);
                    AppendFontManagerStats(statsBuilder);
                    AppendDetailedStats(statsBuilder);
                    statisticsPage.Text = statsBuilder;
                }

                updateTimer.Restart();
            }

            if (EnableDebug && enableOverlay)
            {
                var offset = HudMain.GetPixelVector(overlayPos) / HudMain.ResScale;

                if (offset.X < 0f)
                    offset.X += overlay.Size.X * .5f;
                else
                    offset.X -= overlay.Size.X * .5f;

                if (offset.Y < 0f)
                    offset.Y += overlay.Size.Y * .5f;
                else
                    offset.Y -= overlay.Size.Y * .5f;

                overlay.Draw(offset, MatrixD.CreateScale(HudMain.ResScale, HudMain.ResScale, 1d) * HudMain.PixelToWorld);
            }
        }

        private void AppendSummaryStats(StringBuilder statsBuilder)
        {
            var vID = RichHudMaster.versionID;
            statsBuilder.Append($"Rich HUD Master (v {vID.X}.{vID.Y}.{vID.Z}.{vID.W})\n");
            statsBuilder.Append("Summary:\n");
            statsBuilder.Append($"\tSE Input Blacklist: {BindManager.CurrentBlacklistMode}\n");
            statsBuilder.Append($"\tInput Mode: {HudMain.InputMode}\n");
            statsBuilder.Append($"\tCursor Visible: {HudMain.Cursor.Visible}\n");
            statsBuilder.Append($"\tChat Open: {BindManager.IsChatOpen}\n");
            statsBuilder.Append($"\tClient Mods: {RichHudMaster.Clients.Count}\n");

            foreach (var client in RichHudMaster.Clients)
                statsBuilder.Append($"\t\t{client.name}\t\t|\tVersion: {client.VersionString}\t\t|\tSubtype: {client.ClientSubtype}\n");
        }

        private void AppendHudMainStats(StringBuilder statsBuilder)
        {
            statsBuilder.Append("\n\tHudMain:\n");
            statsBuilder.Append($"\t\tHUD Spaces Updating: {HudMain.TreeManager.HudSpacesRegistered}\n");
            statsBuilder.Append($"\t\tElements Updating: {HudMain.TreeManager.ElementRegistered}\n");
            statsBuilder.Append($"\t\tClients Registered: {HudMain.TreeManager.Clients.Count}\n");
        }

        private void AppendBillboardStats(StringBuilder statsBuilder)
        {
            statsBuilder.Append("\t\tBillboard Stats\n");
            AddGrid(statsBuilder, new string[,]
            {
                { "Name", "30th", "50th", "99th" },
                { "BB Use", BillBoardUtils.GetUsagePercentile(.30f).ToString(), BillBoardUtils.GetUsagePercentile(.50f).ToString(), BillBoardUtils.GetUsagePercentile(.99f).ToString() },
                { "BB Alloc", BillBoardUtils.GetAllocPercentile(.30f).ToString(), BillBoardUtils.GetAllocPercentile(.50f).ToString(), BillBoardUtils.GetAllocPercentile(.99f).ToString() },
                { "Matrices", BillBoardUtils.GetMatrixUsagePercentile(.30f).ToString(), BillBoardUtils.GetMatrixUsagePercentile(.50f).ToString(), BillBoardUtils.GetMatrixUsagePercentile(.99f).ToString() }
            }, 3, 4);
        }

        private void AppendUpdateTimerStats(StringBuilder statsBuilder)
        {
            updateStats.Update();
            statsBuilder.Append($"\t\tUpdate Timers (IsHighResolution: {Stopwatch.IsHighResolution}):\n");
            AddGrid(statsBuilder, new string[,]
            {
                { "Name", "Avg", "50th", "99th" },
                { "Draw", $"{updateStats.AvgDrawTime:F2}ms", $"{updateStats.Draw50th:F2}ms", $"{updateStats.Draw99th:F2}ms" },
                { "Input", $"{updateStats.AvgInputTime:F2}ms", $"{updateStats.Input50th:F2}ms", $"{updateStats.Input99th:F2}ms" },
                { "Total", $"{updateStats.AvgTotalTime:F2}ms", $"{updateStats.Total50th:F2}ms", $"{updateStats.Total99th:F2}ms" },
                { "Tree*", $"{updateStats.AvgTreeTime:F2}ms", $"{updateStats.Tree50th:F2}ms", $"{updateStats.Tree99th:F2}ms" }
            }, 3, 4);
        }

        private void AppendTextCachingStats(StringBuilder statsBuilder)
        {
            statsBuilder.Append("\t\tText Caching\n");
            AddGrid(statsBuilder, new string[,]
            {
                { "Cache", "Enabled", "Hit Pct" },
                { "Text", TextDiagnostics.LineTextCache.Enabled.ToString(), $"{TextDiagnostics.LineTextCache.GetHitPct():F2}%" },
                { "Glyphs", TextDiagnostics.GlyphCache.Enabled.ToString(), $"{TextDiagnostics.GlyphCache.GetHitPct():F2}%" },
                { "Typesetting", TextDiagnostics.TypesettingCache.Enabled.ToString(), $"{TextDiagnostics.TypesettingCache.GetHitPct():F2}%" },
                { "Billboards", TextDiagnostics.BBCache.Enabled.ToString(), $"{TextDiagnostics.BBCache.GetHitPct():F2}%" }
            }, 3, 4);
        }

        private void AppendCursorStats(StringBuilder statsBuilder)
        {
            var cursor = HudMain.Cursor as HudMain.HudCursor;
            statsBuilder.Append("\n\tCursor:\n");
            statsBuilder.Append($"\t\tVisible: {cursor.Visible}\n");
            statsBuilder.Append($"\t\tCaptured: {cursor.IsCaptured}\n");

            if (cursor.IsCaptured)
            {
                statsBuilder.Append($"\t\tPosition: {cursor.ScreenPos}\n");
                var modName = cursor.CapturedElement(null, (int)HudElementAccessors.ModName) as string ?? "None";
                var type = cursor.CapturedElement(null, (int)HudElementAccessors.GetType) as Type;
                var zOffset = (sbyte)cursor.CapturedElement(null, (int)HudElementAccessors.ZOffset);
                var fullZOffset = (ushort)cursor.CapturedElement(null, (int)HudElementAccessors.FullZOffset);
                var pos = (Vector2)cursor.CapturedElement(null, (int)HudElementAccessors.Position);
                var size = (Vector2)cursor.CapturedElement(null, (int)HudElementAccessors.Size);

                statsBuilder.Append($"\t\t\tMod: {modName}\n");
                statsBuilder.Append($"\t\t\tType: {type}\n");
                statsBuilder.Append($"\t\t\tZOffset: {zOffset}\n");
                statsBuilder.Append($"\t\t\tFullZOffset: {fullZOffset}\n");
                statsBuilder.Append($"\t\t\tPosition: {pos}\n");
                statsBuilder.Append($"\t\t\tSize: {size}\n");
            }
        }

        private void AppendBindManagerStats(StringBuilder statsBuilder)
        {
            statsBuilder.Append("\n\tBindManager:\n");
            statsBuilder.Append($"\t\tControls Registered: {BindManager.Controls.Count}\n");
            statsBuilder.Append($"\t\tClients Registered: {BindManager.Clients.Count}\n");
        }

        private void AppendFontManagerStats(StringBuilder statsBuilder)
        {
            statsBuilder.Append("\n\n\tFontManager:\n");
            statsBuilder.Append($"\t\tFonts Registered: {FontManager.Fonts.Count}\n\n");

            foreach (var font in FontManager.Fonts)
            {
                FontStyles supportedStyles = FontStyles.Italic | FontStyles.Underline;
                if (font.IsStyleDefined(FontStyles.Bold))
                    supportedStyles |= FontStyles.Bold;

                statsBuilder.Append($"\t\t{font.Name}\n");
                statsBuilder.Append($"\t\t\tAtlas PtSize: {font.PtSize}\n");
                statsBuilder.Append($"\t\t\tStyles: Regular, {supportedStyles}\n\n");
            }
        }

        private void AppendDetailedStats(StringBuilder statsBuilder)
        {
            statsBuilder.Append("Details:\n");
            statsBuilder.Append("\tMaster:\n");
            GetHudStats(HudMain.TreeManager.MainClient, statsBuilder);
            GetBindStats(BindManager.MainClient, statsBuilder);

            foreach (var modClient in RichHudMaster.Clients)
            {
                statsBuilder.Append($"\n\t{modClient.name}:\n");
                GetHudStats(modClient.hudClient, statsBuilder);
                GetBindStats(modClient.bindClient, statsBuilder);
            }
        }

        private static void GetHudStats(HudMain.TreeClient client, StringBuilder statsBuilder)
        {
            if (client.InactiveNodeData.Count == 0)
                return;

            statsBuilder.Append($"\t\tHudMain:\n");
            statsBuilder.Append($"\t\t\tEnable Cursor: {client.EnableCursor}\n");
            statsBuilder.Append($"\t\t\tElements Updating: {client.InactiveNodeData.Count}\n\n");
        }

        private static void GetBindStats(BindManager.Client client, StringBuilder statsBuilder)
        {
            IReadOnlyList<IBindGroup> bindGroups = client.Groups;

            if (bindGroups.Count == 0)
                return;

            statsBuilder.Append($"\t\tBindManager:\n");
            statsBuilder.Append($"\t\t\tBlacklist Mode: {client.RequestBlacklistMode}\n");
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
        /// Collects and processes update statistics for HUD components.
        /// </summary>
        internal class UpdateStats
        {
            public double AvgTreeTime => _treeStats.AvgTime - _treeStats.TareTime;
            public double Tree50th => _treeStats.Pct50th - _treeStats.TareTime;
            public double Tree99th => _treeStats.Pct99th - _treeStats.TareTime;

            public double AvgDrawTime => _drawStats.AvgTime - _drawStats.TareTime;
            public double Draw50th => _drawStats.Pct50th - _drawStats.TareTime;
            public double Draw99th => _drawStats.Pct99th - _drawStats.TareTime;

            public double AvgInputTime => _inputStats.AvgTime - _inputStats.TareTime;
            public double Input50th => _inputStats.Pct50th - _inputStats.TareTime;
            public double Input99th => _inputStats.Pct99th - _inputStats.TareTime;

            public double AvgTotalTime => AvgDrawTime + AvgInputTime;
            public double Total50th => Draw50th + Input50th;
            public double Total99th => Draw99th + Input99th;

            private readonly TickStats _drawStats;
            private readonly TickStats _inputStats;
            private readonly TickStats _treeStats;
            private readonly List<long> _tickBuffer;

            public UpdateStats()
            {
                _tickBuffer = new List<long>();
                _drawStats = new TickStats();
                _inputStats = new TickStats();
                _treeStats = new TickStats();
            }

            public void Tare()
            {
                _treeStats.Tare();
                _drawStats.Tare();
                _inputStats.Tare();
            }

            public void ClearTare()
            {
                _treeStats.ClearTare();
                _drawStats.ClearTare();
                _inputStats.ClearTare();
            }

            public void Update()
            {
                _treeStats.Update(HudMain.TreeManager.TreeElapsedTicks, _tickBuffer);
                _drawStats.Update(HudMain.TreeManager.DrawElapsedTicks, _tickBuffer);
                _inputStats.Update(HudMain.TreeManager.InputElapsedTicks, _tickBuffer);
            }

            internal class TickStats
            {
                public double AvgTime { get; private set; }
                public double Pct50th { get; private set; }
                public double Pct99th { get; private set; }
                public double TareTime { get; private set; }

                public void Tare()
                {
                    TareTime = AvgTime;
                }

                public void ClearTare()
                {
                    TareTime = 0d;
                }

                public void Update(IReadOnlyList<long> ticks, List<long> tickBuffer)
                {
                    double tpms = TimeSpan.TicksPerMillisecond;
                    tickBuffer.Clear();
                    tickBuffer.AddRange(ticks);
                    tickBuffer.Sort();

                    long totalTicks = 0;
                    for (int n = 0; n < tickBuffer.Count; n++)
                        totalTicks += tickBuffer[n];

                    TareTime = Math.Min(TareTime, Math.Min(Pct50th, AvgTime));
                    AvgTime = (totalTicks / (double)tickBuffer.Count) / tpms;
                    Pct50th = tickBuffer[(int)(tickBuffer.Count * 0.5d)] / tpms;
                    Pct99th = tickBuffer[(int)(tickBuffer.Count * 0.99d)] / tpms;
                }
            }
        }
    }
}