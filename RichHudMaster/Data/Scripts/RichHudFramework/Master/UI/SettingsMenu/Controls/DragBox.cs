using System;
using RichHudFramework.UI.Rendering;
using VRageMath;

namespace RichHudFramework.UI.Server
{
    using UI;

    internal enum DragBoxAccessors : int
    {
        BoxSize = 16,
    }

    public class DragBox : TerminalValue<Vector2, DragBox>
    {
        public override event Action OnControlChanged;

        public override RichText Name
        {
            get { return window.Title.GetText(); }
            set
            {
                window.Title.SetText(value);
                openButton.Name = value;
            }
        }

        public override Vector2 Value { get { return window.Value; } set { window.Value = value; } }
        public override Func<Vector2> CustomValueGetter { get { return window.CustomValueGetter; } set { window.CustomValueGetter = value; } }
        public override Action<Vector2> CustomValueSetter { get { return window.CustomValueSetter; } set { window.CustomValueSetter = value; } }

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
            ModMenu.Open = false;
            window.Visible = true;            
        }

        private void Close()
        {
            window.Visible = false;
            ModMenu.Open = true;
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
                }

                return null;
            }
        }

        private class DragWindow : WindowBase
        {
            public event Action OnConfirm;
            public Vector2 Value
            {
                get { return HudMain.GetRelativeVector(Offset); }
                set { Offset = HudMain.GetPixelVector(value); }
            }
            public Func<Vector2> CustomValueGetter { get; set; }
            public Action<Vector2> CustomValueSetter { get; set; }

            private readonly TerminalButton confirm;
            private Vector2 lastValue;

            public DragWindow() : base(HudMain.Root)
            {
                MinimumSize = new Vector2(100f);
                AllowResizing = false;

                BodyColor = new Color(41, 54, 62, 230);
                BorderColor = new Color(58, 68, 77);

                Title.Format = ModMenu.ControlText.WithAlignment(TextAlignment.Center);
                header.Height = 40f;

                confirm = new TerminalButton(this)
                {
                    Name = "Confirm",
                    DimAlignment = DimAlignments.Width,
                };

                confirm.button.MouseInput.OnLeftClick += () => OnConfirm?.Invoke();
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