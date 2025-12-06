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
            get { return window.HeaderBuilder.ToString(); }
            set
            {
                window.HeaderBuilder.SetText(value);
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
            get { return window.Size / HudMain.ScreenDim; }
            set { window.Size = value * HudMain.ScreenDim; }
        }

        private readonly BorderedButton openButton;
        private readonly DragWindow window;

        public TerminalDragBox()
        {
            openButton = new BorderedButton()
            {
                Text = "NewDragBox",
                DimAlignment = DimAlignments.UnpaddedWidth,
                Padding = Vector2.Zero
            };
            SetElement(openButton);

            window = new DragWindow(Update)
            {
                Size = new Vector2(300f, 250f),
                Visible = false
            };

            openButton.MouseInput.LeftClicked += Open;
            window.Confirmed += ConfirmPosition;
        }
        public override void Update()
        {
            base.Update();

            if (ToolTip != null && !HudMain.Cursor.IsToolTipRegistered && openButton.MouseInput.IsMousedOver)
                HudMain.Cursor.RegisterToolTip(ToolTip);
        }

        private void Open(object sender, EventArgs args)
        {
            RichHudTerminal.CloseMenu();
            HudMain.EnableCursor = true;
            window.Visible = true;
            window.GetWindowFocus();
        }

        private void ConfirmPosition()
        {
            window.Visible = false;
            RichHudTerminal.OpenMenu();
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
            public event Action Confirmed;

            /// <summary>
            /// Returns the absolute position of the window in screen space on [-0.5, 0.5]
            /// </summary>
            public Vector2 AbsolutePosition { get; set; }

            /// <summary>
            /// If set to true, then the window will align its position to the screen 
            /// quadrant nearest to its position.
            /// </summary>
            public bool AlignToEdge { get; set; }

            private readonly BorderedButton confirmButton;
            private readonly Action DragUpdateAction;
            private Vector2 alignment;

            public DragWindow(Action UpdateAction) : base(HudMain.HighDpiRoot)
            {
                DragUpdateAction = UpdateAction;
                MinimumSize = new Vector2(100f);
                AllowResizing = false;

                BodyColor = new Color(41, 54, 62, 150);
                BorderColor = new Color(58, 68, 77);

                HeaderBuilder.Format = TerminalFormatting.ControlFormat.WithAlignment(TextAlignment.Center);
                header.Height = 40f;

                confirmButton = new BorderedButton(this)
                {
                    Text = "Confirm",
                    DimAlignment = DimAlignments.Width | DimAlignments.IgnorePadding,
                };

                confirmButton.MouseInput.LeftClicked += (sender, args) => Confirmed?.Invoke();
            }

			protected override void Layout()
			{
				base.Layout();

				// Recalculate offset from last normalized position
				Offset = (AbsolutePosition * HudMain.ScreenDimHighDPI) - Origin - alignment;
			}

            protected override void HandleInput(Vector2 cursorPos)
            {
				base.HandleInput(cursorPos);

				// Clamp to screen edges
				Vector2 halfScreen = 0.5f * HudMain.ScreenDimHighDPI,
					halfSize = 0.5f * CachedSize;
				Offset = Vector2.Clamp(Offset, -halfScreen + halfSize, halfScreen - halfSize);

				if (canMoveWindow)
                {
					UpdateAlignment();

					// Calculate next normalized position from last position
					Vector2 nextPos = ((Position + alignment) / HudMain.ScreenDim) * HudMain.ResScale;
					nextPos = new Vector2((float)Math.Round(nextPos.X, 4), (float)Math.Round(nextPos.Y, 4));

					AbsolutePosition = nextPos;
					DragUpdateAction();
				}

				if (SharedBinds.Escape.IsNewPressed)
					Confirmed?.Invoke();
			}

			private void UpdateAlignment()
			{
				const float epsilon = 1E-2f;
				Vector2 pos = Position;
				pos.X = (float)Math.Round(pos.X, 4);
				pos.Y = (float)Math.Round(pos.Y, 4);
				alignment = Vector2.Zero;

				if (AlignToEdge)
				{
					if (pos.X > epsilon)
						alignment.X = Width * .5f;
					else if (pos.X < -epsilon)
						alignment.X = -Width * .5f;

					if (pos.Y > epsilon)
						alignment.Y = Height * .5f;
					else if (pos.Y < -epsilon)
						alignment.Y = -Height * .5f;
				}

				alignment.X = (float)Math.Round(alignment.X, 4);
				alignment.Y = (float)Math.Round(alignment.Y, 4);
			}
        }
    }
}