using RichHudFramework.Internal;
using System.Collections.Generic;
using System.Diagnostics;
using VRageMath;
using System;

namespace RichHudFramework.UI.Server
{
    /// <summary>
    /// GUI used to change binds in <see cref="BindManager.Group"/>s.
    /// </summary>
    public sealed class RebindDialog : RichHudComponentBase
    {
        public static bool Open 
        { 
            get { return instance?.open ?? false; } 
            private set 
            { 
                instance.menu.Visible = value;
                instance.open = value;
            } 
        }

        private static RebindDialog instance;
        private const long inputWaitTime = 200;

        private readonly List<int> blacklist;
        private readonly RebindHud menu;
        private readonly Stopwatch stopwatch;

        private IBind bind;
        private List<IControl> combo;
        private IControl newControl;

        private Action CallbackFunc;
        private int controlIndex;
        private bool open;

        private RebindDialog() : base(false, true)
        {
            stopwatch = new Stopwatch();
            menu = new RebindHud(HudMain.HighDpiRoot) { Visible = false };

            blacklist = new List<int>
            {
                BindManager.GetControl("escape").Index,
            };

            SharedBinds.Escape.NewPressed += (sender, args) => Exit();
        }

        public static void Init()
        {
            if (instance == null)
                instance = new RebindDialog();
        }

        public override void Close()
        {
            Exit();
            instance = null;
        }

        /// <summary>
        /// Opens the rebind dialog for the control at the specified position.
        /// </summary>
        public static void UpdateBind(IBind bind, int bindPos, Action CallbackFunc = null) =>
            instance.UpdateBindInternal(bind, bindPos, CallbackFunc);

        /// <summary>
        /// Opens the rebind dialog for the control at the specified position.
        /// </summary>
        private void UpdateBindInternal(IBind bind, int bindPos, Action CallbackFunc = null)
        {
            BindManager.BlacklistMode = SeBlacklistModes.AllKeys;
            HudMain.EnableCursor = true;

            stopwatch.Restart();
            Open = true;
            newControl = null;

            this.bind = bind;
            this.CallbackFunc = CallbackFunc;

            combo = bind.GetCombo();
            controlIndex = MathHelper.Clamp(bindPos, 0, combo.Count);
        }

        public override void HandleInput()
        {
            if (stopwatch.IsRunning && stopwatch.ElapsedMilliseconds > inputWaitTime)
                stopwatch.Stop();

            if ((stopwatch.ElapsedMilliseconds > inputWaitTime) && bind != null)
            {
                for (int n = 0; (n < BindManager.Controls.Count && newControl == null); n++)
                {
                    if (BindManager.Controls[n] != null && BindManager.Controls[n].IsPressed)
                    {
                        if (!blacklist.Contains(BindManager.Controls[n].Index))
                        {
                            newControl = BindManager.Controls[n];
                            Confirm();
                        }
                        else if (combo.Contains(BindManager.Controls[n]))
                            Exit();
                    }
                }
            }
        }

        /// <summary>
        /// Adds the new control at the index given, applies the new combo and closes the dialog.
        /// </summary>
        private void Confirm()
        {
            if (Open && newControl != null)
            {
                if (controlIndex < combo.Count)
                    combo[controlIndex] = newControl;
                else
                    combo.Add(newControl);

                bind.TrySetCombo(combo, 0, false);
                Exit();
            }
        }

        /// <summary>
        /// Closes and resets the dialog.
        /// </summary>
        private void Exit()
        {
            if (Open)
            {
                BindManager.BlacklistMode = SeBlacklistModes.None;
                HudMain.EnableCursor = false;
                Open = false;
                CallbackFunc?.Invoke();
                CallbackFunc = null;
                bind = null;
            }
        }

        /// <summary>
        /// Generates the UI for the <see cref="RebindDialog"/>
        /// </summary>
        private class RebindHud : HudElementBase
        {
            public readonly TexturedBox background;
            public readonly BorderBox border;
            public readonly Label header, subheader;

            public RebindHud(HudParentBase parent) : base(parent)
            {
                background = new TexturedBox(this)
                {
                    Color = new Color(37, 46, 53),
                    DimAlignment = DimAlignments.Size,
                };

                border = new BorderBox(this)
                {
                    Thickness = 1f,
                    Color = new Color(53, 66, 75),
                    DimAlignment = DimAlignments.Size,
                };

                header = new Label(this)
                {
                    AutoResize = false, 
                    DimAlignment = DimAlignments.UnpaddedWidth,
                    Height = 24f,
                    ParentAlignment = ParentAlignments.PaddedInnerTop,
                    Offset = new Vector2(0f, -42f),
                    Format = new GlyphFormat(Color.White, TextAlignment.Center, 1.25f),
                    Text = "SELECT CONTROL",
                };

                var divider = new TexturedBox(header)
                {
                    DimAlignment = DimAlignments.Width,
                    Height = 1f,
                    ParentAlignment = ParentAlignments.Bottom,
                    Offset = new Vector2(0f, -10f),
                    Color = new Color(84, 98, 111),
                };
                
                subheader = new Label(divider)
                {
                    AutoResize = false,
                    DimAlignment = DimAlignments.Width,
                    Height = 24f,
                    ParentAlignment = ParentAlignments.Bottom,
                    Offset = new Vector2(0f, -41f),
                    Format = new GlyphFormat(GlyphFormat.Blueish.Color, TextAlignment.Center, 1.25f),
                    Text = "Please press a key",
                };

                Padding = new Vector2(372f, 0f);
                Size = new Vector2(1210f, 288f);

                ZOffset = sbyte.MaxValue - 1;
                layerData.zOffsetInner = byte.MaxValue - 1;
            }

            protected override void Layout()
            {
                background.Color = background.Color.SetAlphaPct(HudMain.UiBkOpacity * .95f);
            }
        }
    }
}