using RichHudFramework.Internal;
using System.Collections.Generic;
using VRageMath;
using System;

namespace RichHudFramework.UI.Server
{
    /// <summary>
    /// GUI used to change binds in <see cref="BindManager.Group"/>s.
    /// </summary>
    public sealed class RebindDialog : RichHudComponentBase
    {
        private static RebindDialog Instance
        {
            get { Init(); return instance; }
            set { instance = value; }
        }

        private bool Open 
        { 
            get { return menu.Visible; } 
            set { menu.Visible = value; } 
        }

        private static RebindDialog instance;
        private const long inputWaitTime = 200;

        private readonly List<int> blacklist;
        private readonly RebindHud menu;
        private readonly Utils.Stopwatch stopwatch;

        private IBind bind;
        private List<IControl> combo;
        private IControl newControl;

        private Action CallbackFunc;
        private int controlIndex;

        private RebindDialog() : base(false, true)
        {
            stopwatch = new Utils.Stopwatch();
            menu = new RebindHud(HudMain.Root);

            blacklist = new List<int>
            {
                BindManager.GetControl("escape").Index,
            };

            SharedBinds.Escape.NewPressed += Exit;
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
            Open = true;
            newControl = null;

            this.bind = bind;
            this.CallbackFunc = CallbackFunc;

            combo = bind.GetCombo();
            controlIndex = MathHelper.Clamp(bindPos, 0, combo.Count);
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
            if (Open && newControl != null)
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
            if (Open)
            {
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
                    DimAlignment = DimAlignments.Both,
                };

                border = new BorderBox(this)
                {
                    Thickness = 1f,
                    Color = new Color(53, 66, 75),
                    DimAlignment = DimAlignments.Both,
                };

                header = new Label(this)
                {
                    AutoResize = false, 
                    DimAlignment = DimAlignments.Width | DimAlignments.IgnorePadding,
                    Height = 24f,
                    ParentAlignment = ParentAlignments.Top | ParentAlignments.InnerV | ParentAlignments.UsePadding,
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
                zOffsetInner = byte.MaxValue - 1;
            }

            protected override void Layout()
            {
                LocalScale = HudMain.ResScale;
                background.Color = background.Color.SetAlphaPct(HudMain.UiBkOpacity * .95f);
            }
        }
    }
}