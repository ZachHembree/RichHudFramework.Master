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
            enableOverlay = ExceptionHandler.DebugLogging;
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

            category.Add(statisticsPage);
            category.Add(CreateSettingsPage());

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

            controlCategory.Add(CreateOverlayControlTile());
            controlCategory.Add(CreateCacheControlTile());

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
                    ControlChangedHandler = (obj, args) => { RichHudStats.UI.Tare(); },
                    ToolTip = "Sets the current update times as the baseline."
                },
                new TerminalButton()
                {
                    Name = "Clear Tare",
                    ControlChangedHandler = (obj, args) => { RichHudStats.UI.ClearTare(); },
                    ToolTip = "Resets timer tare to zero."
                }
            };
        }

        private ControlTile CreateCacheControlTile()
        {
            return new ControlTile
            {
                new TerminalLabel() { Name = "Text Debug" },
                CreateCacheCheckbox("Text Cache", () => RichHudStats.Text.LineTextCache.Enabled,
                    value => RichHudStats.Text.LineTextCache.Enabled = value,
                    "Controls all text-related caching.\nRequired by glyph, typesetting and billboard caches."),
                CreateCacheCheckbox("Glyph Cache", () => RichHudStats.Text.GlyphCache.Enabled,
                    value => RichHudStats.Text.GlyphCache.Enabled = value,
                    "Controls caching of text font data.\nRequires text cache."),
                CreateCacheCheckbox("Typesetting Cache", () => RichHudStats.Text.TypesettingCache.Enabled,
                    value => RichHudStats.Text.TypesettingCache.Enabled = value,
                    "Controls caching for character placement within a UI element.\nRequires glyph and text caches."),
                CreateCacheCheckbox("Billboard Cache", () => RichHudStats.Text.BBCache.Enabled,
                    value => RichHudStats.Text.BBCache.Enabled = value,
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
            if (EnableDebug && (statisticsPage.Element.Visible || enableOverlay) && updateTimer.ElapsedMilliseconds > 100)
            {
                statsBuilder.Clear();
                AppendSummaryStats(statsBuilder);
                AppendHudMainStats(statsBuilder);
                AppendBillboardStats(statsBuilder);
                AppendUpdateTimerStats(statsBuilder);
                AppendNodeUpdateStats(statsBuilder);
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
            statsBuilder.Append($"\t\tHUD Spaces Updating: {RichHudStats.UI.HudSpacesRegistered}\n");
			statsBuilder.Append($"\t\tSubtrees Updating: {RichHudStats.UI.SubtreesRegistered}\n");
			statsBuilder.Append($"\t\tElements Updating: {RichHudStats.UI.ElementsRegistered}\n");
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
            statsBuilder.Append($"\t\tUpdate Timers (IsHighResolution: {Stopwatch.IsHighResolution}):\n");
            AddGrid(statsBuilder, new string[,]
            {
                { "Name", "Avg", "50th", "99th" },
                { "Draw", $"{RichHudStats.UI.DrawAvg:F2}ms", $"{RichHudStats.UI.Draw50th:F2}ms", $"{RichHudStats.UI.Draw99th:F2}ms" },
                { "Input", $"{RichHudStats.UI.InputAvg:F2}ms", $"{RichHudStats.UI.Input50th:F2}ms", $"{RichHudStats.UI.Input99th:F2}ms" },
                { "Total", $"{RichHudStats.UI.TotalAvg:F2}ms", $"{RichHudStats.UI.Total50th:F2}ms", $"{RichHudStats.UI.Total99th:F2}ms" },
                { "Tree*", $"{RichHudStats.UI.TreeAvg:F2}ms", $"{RichHudStats.UI.Tree50th:F2}ms", $"{RichHudStats.UI.Tree99th:F2}ms" }
            }, 3, 4);
        }

        private void AppendTextCachingStats(StringBuilder statsBuilder)
        {
            statsBuilder.Append("\t\tText Caching\n");
            AddGrid(statsBuilder, new string[,]
            {
                { "Cache", "Enabled", "Hit Pct" },
                { "Text", RichHudStats.Text.LineTextCache.Enabled.ToString(), $"{RichHudStats.Text.LineTextCache.GetHitPct():F2}%" },
                { "Glyphs", RichHudStats.Text.GlyphCache.Enabled.ToString(), $"{RichHudStats.Text.GlyphCache.GetHitPct():F2}%" },
                { "Typesetting", RichHudStats.Text.TypesettingCache.Enabled.ToString(), $"{RichHudStats.Text.TypesettingCache.GetHitPct():F2}%" },
                { "Billboards", RichHudStats.Text.BBCache.Enabled.ToString(), $"{RichHudStats.Text.BBCache.GetHitPct():F2}%" }
            }, 3, 4);
        }

		private void AppendNodeUpdateStats(StringBuilder statsBuilder)
		{
			statsBuilder.Append($"\t\tUpdate Counts:\n");
			AddGrid(statsBuilder, new string[,]
			{
				{ "Name", "Avg", "50th", "99th" },
				{ "Callbacks", $"{RichHudStats.UI.AvgNodeUpdateCount:F0}", $"{RichHudStats.UI.NodeUpdate50th:F0}", $"{RichHudStats.UI.NodeUpdate99th:F0}" },
				{ "Subtree Sort\t", $"{RichHudStats.UI.SubtreeSortAvgCount:F0}", $"{RichHudStats.UI.SubtreeSort50th:F0}", $"{RichHudStats.UI.SubtreeSort99th:F0}" },
				{ "Element Sort\t", $"{RichHudStats.UI.ElementSortAvgCount:F0}", $"{RichHudStats.UI.ElementSort50th:F0}", $"{RichHudStats.UI.ElementSort99th:F0}" }
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
            if (client.ElementsUpdating == 0)
                return;

            statsBuilder.Append($"\t\tHudMain:\n");
            statsBuilder.Append($"\t\t\tEnable Cursor: {client.EnableCursor}\n");
			statsBuilder.Append($"\t\t\tSubtrees Updating: {client.SubtreesUpdating}\n");
			statsBuilder.Append($"\t\t\tElements Updating: {client.ElementsUpdating}\n");
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
    }
}