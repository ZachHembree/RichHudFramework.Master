using System;
using RichHudFramework.UI.Rendering;
using VRageMath;

namespace RichHudFramework.UI.Server
{
    using UI;

    internal enum DragBoxAccessors : int
    {
        BoxSize = 16,
        AlignToEdge = 17,
    }

    public class DragBox : TerminalValue<Vector2, DragBox>
    {
        public override event Action OnControlChanged;

        public override RichText Name
        {
            get { return window.Header.GetText(); }
            set
            {
                window.Header.SetText(value);
                openButton.Name = value;
            }
        }

        public override Vector2 Value { get { return window.Value; } set { window.Value = value; } }
        public override Func<Vector2> CustomValueGetter { get { return window.CustomValueGetter; } set { window.CustomValueGetter = value; } }
        public override Action<Vector2> CustomValueSetter { get { return window.CustomValueSetter; } set { window.CustomValueSetter = value; } }
        public bool AlignToEdge { get { return window.AlignToEdge; } set { window.AlignToEdge = value; } }

        public Vector2 BoxSize
        {
            get { return HudMain.GetRelativeVector(window.Size); }
            set { window.Size = HudMain.GetPixelVector(value); }
        }

        private readonly TerminalButton openButton;
        private readonly DragWindow window;
        private Vector2 lastValue;

        public DragBox(IHudParent parent = null) : base(parent)
        {
            window = new DragWindow()
            {
                Size = new Vector2(300f, 250f),
                Visible = false
            };

            openButton = new TerminalButton(this)
            {
                DimAlignment = DimAlignments.Width | DimAlignments.IgnorePadding,
            };

            openButton.MouseInput.OnLeftClick += Open;
            window.OnConfirm += Close;

            Name = "NewDragBox";
            Size = new Vector2(253f, 50f);
        }

        private void Open()
        {
            RichHudTerminal.Open = false;
            window.Visible = true;
            window.GetFocus();
        }

        private void Close()
        {
            window.Visible = false;
            RichHudTerminal.Open = true;
        }

        protected override void Draw()
        {
            if (Value != lastValue)
            {
                lastValue = Value;
                OnControlChanged?.Invoke();
                CustomValueSetter?.Invoke(Value);
            }

            if (CustomValueGetter != null && Value != CustomValueGetter())
                Value = CustomValueGetter();
        }

        protected override object GetOrSetMember(object data, int memberEnum)
        {
            if (memberEnum < 16)
                return base.GetOrSetMember(data, memberEnum);
            else
            {
                switch((DragBoxAccessors)memberEnum)
                {
                    case DragBoxAccessors.BoxSize:
                        {
                            if (data == null)
                                return BoxSize;
                            else
                                BoxSize = (Vector2)data;

                            break;
                        }
                    case DragBoxAccessors.AlignToEdge:
                        {
                            if (data == null)
                                return AlignToEdge;
                            else
                                AlignToEdge = (bool)data;

                            break;
                        }
                }

                return null;
            }
        }

        private class DragWindow : WindowBase
        {
            public event Action OnConfirm;
            public Vector2 Value
            {
                get { return HudMain.GetRelativeVector(base.Offset); }
                set { base.Offset = HudMain.GetPixelVector(value); }
            }

            public override Vector2 Offset 
            {
                get { return base.Offset + alignment; }
            }

            public Func<Vector2> CustomValueGetter { get; set; }
            public Action<Vector2> CustomValueSetter { get; set; }
            public bool AlignToEdge { get; set; }

            private readonly TerminalButton confirm;
            private Vector2 lastValue, alignment;

            public DragWindow() : base(HudMain.Root)
            {
                MinimumSize = new Vector2(100f);
                AllowResizing = false;

                BodyColor = new Color(41, 54, 62, 150);
                BorderColor = new Color(58, 68, 77);

                Header.Format = RichHudTerminal.ControlFormat.WithAlignment(TextAlignment.Center);
                header.Height = 40f;

                confirm = new TerminalButton(this)
                {
                    Name = "Confirm",
                    DimAlignment = DimAlignments.Width,
                };

                confirm.button.MouseInput.OnLeftClick += () => OnConfirm?.Invoke();
            }

            protected override void BeforeDraw()
            {
                Scale = HudMain.ResScale;

                base.BeforeDraw();

                if (canMoveWindow)
                    Offset = HudMain.Cursor.Origin + cursorOffset - Origin - alignment;
            }

            protected override void Draw()
            {
                base.Draw();

                if (Value != lastValue)
                {
                    lastValue = Value;
                    CustomValueSetter?.Invoke(Value);
                }

                if (CustomValueGetter != null && Value != CustomValueGetter())
                    Value = CustomValueGetter();

                if (AlignToEdge)
                {
                    if (base.Offset.X < 0)
                        alignment.X = Width / 2f;
                    else
                        alignment.X = -Width / 2f;

                    if (base.Offset.Y < 0)
                        alignment.Y = Height / 2f;
                    else
                        alignment.Y = -Height / 2f;
                }
                else
                    alignment = Vector2.Zero;
            }

            protected override void HandleInput()
            {
                base.HandleInput();

                if (SharedBinds.Escape.IsNewPressed)
                    OnConfirm?.Invoke();
            }
        }
    }
}