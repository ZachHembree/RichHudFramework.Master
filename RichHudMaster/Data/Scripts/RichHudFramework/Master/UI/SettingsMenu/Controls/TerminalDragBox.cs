using System;
using RichHudFramework.UI.Rendering;
using VRageMath;

namespace RichHudFramework.UI.Server
{
    using UI;

    public enum DragBoxAccessors : int
    {
        BoxSize = 16,
        AlignToEdge = 17,
    }

    /// <summary>
    /// A terminal control that uses a draggable window to indicate a position on the screen.
    /// </summary>
    public class TerminalDragBox : TerminalValue<Vector2>
    {
        /// <summary>
        /// The name of the control as it appears in the terminal.
        /// </summary>
        public override string Name
        {
            get { return window.Header.ToString(); }
            set
            {
                window.Header.SetText(value);
                openButton.Text = value;
            }
        }

        /// <summary>
        /// Value associated with the control.
        /// </summary>
        public override Vector2 Value { get { return window.AbsolutePosition; } set { window.AbsolutePosition = value; } }

        /// <summary>
        /// Determines whether or not the window will automatically align itself to one side of the screen
        /// or the other.
        /// </summary>
        public bool AlignToEdge { get { return window.AlignToEdge; } set { window.AlignToEdge = value; } }

        /// <summary>
        /// Size of the window spawned by the control.
        /// </summary>
        public Vector2 BoxSize
        {
            get { return HudMain.GetAbsoluteVector(window.Size); }
            set { window.Size = HudMain.GetPixelVector(value); }
        }

        private readonly BorderedButton openButton;
        private readonly DragWindow window;

        public TerminalDragBox()
        {
            openButton = new BorderedButton()
            {
                Text = "NewDragBox",
                DimAlignment = DimAlignments.Width | DimAlignments.IgnorePadding,
                Size = new Vector2(253f, 50f)
            };
            SetElement(openButton);

            window = new DragWindow(Update)
            {
                Size = new Vector2(300f, 250f),
                Visible = false
            };

            openButton.MouseInput.LeftClicked += Open;
            window.OnConfirm += Close;
        }

        private void Open(object sender, EventArgs args)
        {
            RichHudTerminal.OpenMenu();
            window.Visible = true;
            window.GetFocus();
        }

        private void Close()
        {
            window.Visible = false;
            RichHudTerminal.CloseMenu();
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

        /// <summary>
        /// Customized window with a confirm button used to specify a position on the screen.
        /// </summary>
        private class DragWindow : WindowBase
        {
            /// <summary>
            /// Invoked when the window's confirm button is clicked
            /// </summary>
            public event Action OnConfirm;

            /// <summary>
            /// Returns the absolute position of the window in screen space on [-0.5, 0.5]
            /// </summary>
            public Vector2 AbsolutePosition
            {
                get { return _absolutePosition; } 
                set 
                {
                    value = Vector2.Clamp(value, -Vector2.One / 2f, Vector2.One / 2f);
                    _absolutePosition = value; 
                } 
            }

            /// <summary>
            /// If set to true, then the window will align its position to the screen 
            /// quadrant nearest to its position.
            /// </summary>
            public bool AlignToEdge { get; set; }

            private readonly BorderedButton confirmButton;
            private readonly Action DragUpdateAction;
            private Vector2 alignment, _absolutePosition;

            public DragWindow(Action UpdateAction) : base(HudMain.Root)
            {
                DragUpdateAction = UpdateAction;
                MinimumSize = new Vector2(100f);
                AllowResizing = false;

                BodyColor = new Color(41, 54, 62, 150);
                BorderColor = new Color(58, 68, 77);

                Header.Format = TerminalFormatting.ControlFormat.WithAlignment(TextAlignment.Center);
                header.Height = 40f;

                confirmButton = new BorderedButton(this)
                {
                    Text = "Confirm",
                    DimAlignment = DimAlignments.Width | DimAlignments.IgnorePadding,
                };

                confirmButton.MouseInput.LeftClicked += (sender, args) => OnConfirm?.Invoke();
            }

            protected override void Layout()
            {
                LocalScale = HudMain.ResScale;
                Offset = HudMain.GetPixelVector(_absolutePosition) - Origin - alignment;

                base.Layout();

                _absolutePosition = HudMain.GetAbsoluteVector(Position + alignment);
                UpdateAlignment();
            }

            private void UpdateAlignment()
            {
                alignment = new Vector2();

                if (AlignToEdge)
                {
                    if (cachedPosition.X > 0f)
                        alignment.X = Width / 2f;
                    else
                        alignment.X = -Width / 2f;

                    if (cachedPosition.Y > 0f)
                        alignment.Y = Height / 2f;
                    else
                        alignment.Y = -Height / 2f;
                }
            }

            protected override void HandleInput(Vector2 cursorPos)
            {
                base.HandleInput(cursorPos);

                if (SharedBinds.Escape.IsNewPressed)
                    OnConfirm?.Invoke();

                DragUpdateAction();
            }
        }
    }
}