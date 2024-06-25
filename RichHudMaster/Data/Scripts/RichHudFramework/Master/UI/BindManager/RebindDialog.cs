using RichHudFramework.Internal;
using System.Collections.Generic;
using System.Diagnostics;
using VRageMath;
using System;
using VRage.Utils;
using System.Linq;

namespace RichHudFramework.UI.Server
{
    /// <summary>
    /// GUI used to change binds in <see cref="BindManager.Group"/>s
    /// </summary>
    public sealed class RebindDialog : RichHudComponentBase
    {
        public static bool Open { get; private set; }

        private static RebindDialog instance;
        private const long comboConfirmDelayMS = 500;

        private readonly RebindHud popup;
        private readonly IReadOnlyList<ControlHandle> blacklist;
        private readonly IControl escape;

        private List<ControlHandle> pressedControls, lastPressedControls;
        private bool didComboChange;
        private readonly Stopwatch comboConfirmTimer;

        private IBind bind;
        private int alias;
        private Action CallbackFunc;

        private RebindDialog() : base(false, true)
        {
            popup = new RebindHud(HudMain.HighDpiRoot) { Visible = false };
            escape = BindManager.GetControl(RichHudControls.Escape);
            blacklist = new List<ControlHandle>
            {
                RichHudControls.Escape
            };

            comboConfirmTimer = new Stopwatch();
            pressedControls = new List<ControlHandle>();
            lastPressedControls = new List<ControlHandle>();
            didComboChange = false;
            Open = false;
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
        public static void UpdateBind(IBind bind, int alias, Action CallbackFunc = null) =>
            instance.UpdateBindInternal(bind, alias, CallbackFunc);

        /// <summary>
        /// Opens the rebind dialog for the control at the specified position.
        /// </summary>
        private void UpdateBindInternal(IBind bind, int alias, Action CallbackFunc = null)
        {
            this.bind = bind;
            this.alias = alias;
            this.CallbackFunc = CallbackFunc;

            BindManager.BlacklistMode = SeBlacklistModes.Full;
            comboConfirmTimer.Restart();
            popup.Visible = true;
            Open = true;
        }

        public override void HandleInput()
        {
            if (Open && bind != null)
            {
                if (escape.IsPressed)
                {
                    Exit();
                }
                else
                {
                    IReadOnlyList<ControlHandle> controls = BindManager.Controls;
                    pressedControls.Clear();

                    foreach (ControlHandle con in controls)
                    {
                        if (con.Control.IsPressed)
                        {
                            if (!blacklist.Contains(con))
                                pressedControls.Add(con);
                        }
                    }

                    didComboChange = !pressedControls.SequenceEqual(lastPressedControls);
                    MyUtils.Swap(ref pressedControls, ref lastPressedControls);

                    if (!didComboChange && pressedControls.Count > 0)
                    {
                        if (comboConfirmTimer.ElapsedMilliseconds > comboConfirmDelayMS)
                        {
                            Confirm();
                        }
                    }
                    else
                    {
                        comboConfirmTimer.Restart();
                    }

                    popup.ComboProgress = (float)(comboConfirmTimer.ElapsedMilliseconds / (double)comboConfirmDelayMS);
                }
            }
        }

        /// <summary>
        /// Adds the new control at the index given, applies the new combo and closes the dialog.
        /// </summary>
        private void Confirm()
        {
            if (Open)
            {
                bind.TrySetCombo(pressedControls, alias, false);
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
                popup.Visible = false;
                Open = false;
                CallbackFunc?.Invoke();
                CallbackFunc = null;
                bind = null;
            }
        }

        /// <summary>
        /// Popup for the <see cref="RebindDialog"/>
        /// </summary>
        private class RebindHud : HudElementBase
        {
            public float ComboProgress { get; set; }

            public readonly TexturedBox background, divider, progressBar;
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
                    Thickness = 2f,
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
                    Text = "REBIND COMBO",
                };

                divider = new TexturedBox(header)
                {
                    DimAlignment = DimAlignments.Width,
                    Height = 1f,
                    ParentAlignment = ParentAlignments.Bottom,
                    Offset = new Vector2(0f, -10f),
                    Color = new Color(84, 98, 111),
                };

                progressBar = new TexturedBox(divider)
                {
                    Height = 3f,
                    Width = 0f,
                    Color = TerminalFormatting.Mint,
                };

                subheader = new Label(divider)
                {
                    AutoResize = false,
                    DimAlignment = DimAlignments.Width,
                    Height = 24f,
                    ParentAlignment = ParentAlignments.Bottom,
                    Offset = new Vector2(0f, -41f),
                    Format = new GlyphFormat(GlyphFormat.Blueish.Color, TextAlignment.Center, 1.25f),
                    Text = "Press and hold new combo",
                };

                Padding = new Vector2(372f, 0f);
                Size = new Vector2(1210f, 288f);

                ZOffset = sbyte.MaxValue - 1;
                layerData.zOffsetInner = byte.MaxValue - 1;
            }

            protected override void Layout()
            {
                background.Color = background.Color.SetAlphaPct(HudMain.UiBkOpacity);
                ComboProgress = MathHelper.Clamp(ComboProgress, 0f, 1f);
                progressBar.Width = ComboProgress * divider.Width;
            }
        }
    }
}