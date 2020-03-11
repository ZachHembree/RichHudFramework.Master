using RichHudFramework.Game;
using System.Collections.Generic;
using VRageMath;
using System;

namespace RichHudFramework.UI.Server
{
    /// <summary>
    /// GUI used to change binds in <see cref="BindManager.Group"/>s.
    /// </summary>
    internal sealed class RebindDialog : RichHudComponentBase
    {
        private static RebindDialog Instance
        {
            get { Init(); return instance; }
            set { instance = value; }
        }
        private static RebindDialog instance;
        private const long inputWaitTime = 200;

        private readonly List<int> blacklist;
        private readonly RebindHud menu;
        private readonly Utils.Stopwatch stopwatch;

        private IBind bind;
        private IList<IControl> combo;
        private IControl newControl;

        private Action CallbackFunc;
        private int controlIndex;
        private bool open;

        private RebindDialog() : base(false, true)
        {
            stopwatch = new Utils.Stopwatch();
            menu = new RebindHud();

            blacklist = new List<int>
            {
                BindManager.GetControl("escape").Index,
            };

            SharedBinds.Escape.OnNewPress += Exit;
        }

        private static void Init()
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
            Instance.UpdateBindInternal(bind, bindPos, CallbackFunc);

        /// <summary>
        /// Opens the rebind dialog for the control at the specified position.
        /// </summary>
        private void UpdateBindInternal(IBind bind, int bindPos, Action CallbackFunc = null)
        {
            stopwatch.Start();
            open = true;
            newControl = null;

            this.bind = bind;
            this.CallbackFunc = CallbackFunc;

            combo = bind.GetCombo();
            controlIndex = MathHelper.Clamp(bindPos, 0, combo.Count);
        }

        public override void Draw()
        {
            if (open)
            {
                menu.BeforeDrawStart();
                menu.DrawStart();
            }
        }

        public override void HandleInput()
        {
            if (stopwatch.Enabled && stopwatch.ElapsedMilliseconds > inputWaitTime)
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
            if (open && newControl != null)
            {
                IControl[] newCombo = new IControl[Math.Max(combo.Count, controlIndex + 1)];

                for (int n = 0; n < combo.Count; n++)
                    newCombo[n] = combo[n];

                newCombo[controlIndex] = newControl;
                bind.TrySetCombo(newCombo, false);
                Exit();
            }
        }

        /// <summary>
        /// Closes and resets the dialog.
        /// </summary>
        private void Exit()
        {
            if (open)
            {
                open = false;
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
            public override bool Visible => Instance.open;

            public readonly TexturedBox background;
            public readonly BorderBox border;
            public readonly Label header, subheader;
            private readonly HudChain<HudElementBase> layout;

            public RebindHud() : base(null)
            {
                background = new TexturedBox(this)
                {
                    Color = new Color(37, 46, 53),
                    DimAlignment = DimAlignments.Both | DimAlignments.IgnorePadding,
                };

                border = new BorderBox(this)
                {
                    Thickness = 1f,
                    Color = new Color(53, 66, 75),
                    DimAlignment = DimAlignments.Both | DimAlignments.IgnorePadding,
                };

                header = new Label()
                {
                    Padding = new Vector2(0f, 27f),
                    Format = GlyphFormat.White.WithSize(1.25f),
                    Text = "SELECT CONTROL",
                };

                var divider = new TexturedBox()
                {
                    Color = new Color(84, 98, 111),
                    Padding = new Vector2(376f, 0f),
                    Height = 1f,
                };

                subheader = new Label()
                {
                    Padding = new Vector2(0f, 80f),
                    Format = GlyphFormat.Blueish.WithSize(1.25f),
                    Text = "Please press a key",
                };

                layout = new HudChain<HudElementBase>(this)
                {
                    AlignVertical = true,
                    AutoResize = true,
                    Offset = new Vector2(0f, 36f),
                    DimAlignment = DimAlignments.Both | DimAlignments.IgnorePadding,
                    ChildContainer = { header, divider, subheader }
                };

                Size = new Vector2(1210f, 288f);
            }

            protected override void Draw()
            {
                Scale = HudMain.ResScale;
                background.Color = background.Color.SetAlphaPct(HudMain.UiBkOpacity * .95f);
            }
        }
    }
}