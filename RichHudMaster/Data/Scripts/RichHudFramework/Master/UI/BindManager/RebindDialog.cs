using RichHudFramework.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using VRageMath;

namespace RichHudFramework.UI.Server
{
	using static RichHudFramework.UI.NodeConfigIndices;

	/// <summary>
	/// GUI used to change binds in <see cref="BindManager.Group"/>s
	/// </summary>
	public sealed class RebindDialog : RichHudComponentBase
	{
		public static bool Open { get; private set; }

		private static RebindDialog instance;
		private const long comboConfirmDelayMS = 1000, chatterDelayMS = 50;

		private readonly RebindHud popup;
		private readonly IReadOnlyList<ControlHandle> blacklist;
		private readonly IControl escape, back;

		private readonly List<ControlHandle> pressedControls;
		private readonly List<ControlHandle> lastPressedControls;
		private readonly List<long> lastConPressTime;
		private readonly List<Vector2I> lastConPressCount;
		private readonly Stopwatch comboConfirmTimer;
		private readonly StringBuilder sb;

		private IBind bind;
		private int alias;
		private Action CallbackFunc;

		private RebindDialog() : base(false, true)
		{
			popup = new RebindHud();
			escape = BindManager.GetControl(RichHudControls.Escape);
			back = BindManager.GetControl(RichHudControls.Back);
			blacklist = new List<ControlHandle>
			{
				RichHudControls.Escape,
				RichHudControls.Back,
				RichHudControls.RightStickX,
				RichHudControls.RightStickY,
			};

			sb = new StringBuilder();
			comboConfirmTimer = new Stopwatch();
			pressedControls = new List<ControlHandle>();
			lastPressedControls = new List<ControlHandle>();
			lastConPressTime = new List<long>();
			lastConPressCount = new List<Vector2I>();
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

			comboConfirmTimer.Restart();
			popup.Visible = true;
			Open = true;
		}

		public override void HandleInput()
		{
			if (Open && bind != null)
			{
				BindManager.RequestTempBlacklist(SeBlacklistModes.Full);

				if (escape.IsPressed || back.IsPressed)
				{
					Exit();
				}
				else
				{
					IReadOnlyList<ControlHandle> controls = BindManager.Controls;
					bool didComboChange = false;
					pressedControls.Clear();

					foreach (ControlHandle con in controls)
					{
						if (!blacklist.Contains(con))
						{
							IControl control = con.Control;
							int lastIndex = lastPressedControls.FindIndex(x => (x.id == con.id));
							long currentTimeMS = comboConfirmTimer.ElapsedMilliseconds;
							bool isPressed = control.IsPressed,
								wasPressed = false;

							// Check previously pressed controls
							if (lastIndex != -1)
							{
								long effectiveDelay = chatterDelayMS;
								Vector2I pressReleaseCount = lastConPressCount[lastIndex];

								// Chatter compensation
								if (isPressed)
								{
									lastConPressTime[lastIndex] = currentTimeMS;
									pressReleaseCount.X++;
								}
								else
									pressReleaseCount.Y++;

								lastConPressCount[lastIndex] = pressReleaseCount;

								if (pressReleaseCount.X > 3 && pressReleaseCount.Y > 3)
									effectiveDelay = comboConfirmDelayMS;

								long lastTimeDeltaMS = currentTimeMS - lastConPressTime[lastIndex];

								// Still pressed or delay not elapsed
								if (isPressed || (lastTimeDeltaMS < effectiveDelay))
									wasPressed = true;

								// Release delay elapsed
								if (!wasPressed)
								{
									lastPressedControls.RemoveAt(lastIndex);
									lastConPressTime.RemoveAt(lastIndex);
									lastConPressCount.RemoveAt(lastIndex);

									for (int i = 0; i < lastConPressCount.Count; i++)
										lastConPressCount[i] = new Vector2I(1, 0);

									didComboChange = true;
								}
							}

							// Add currently pressed controls
							if (isPressed || wasPressed)
							{
								pressedControls.Add(con);

								if (isPressed && !wasPressed)
								{
									for (int i = 0; i < lastConPressCount.Count; i++)
										lastConPressCount[i] = new Vector2I(1, 0);

									lastPressedControls.Add(con);
									lastConPressTime.Add(currentTimeMS);
									lastConPressCount.Add(new Vector2I(1, 0));

									didComboChange = true;
								}

								if (pressedControls.Count >= BindManager.MaxBindLength)
									break;
							}
						}
					}

					if (didComboChange || pressedControls.Count == 0)
						comboConfirmTimer.Restart();

					if (comboConfirmTimer.ElapsedMilliseconds > comboConfirmDelayMS)
						Confirm();

					popup.ComboProgress = (float)(comboConfirmTimer.ElapsedMilliseconds / (double)comboConfirmDelayMS);

					if (popup.ComboProgress > .05f)
					{
						sb.Clear();
						sb.Append(pressedControls[0].Control.DisplayName);

						for (int i = 1; i < pressedControls.Count; i++)
						{
							sb.Append(" + ");
							sb.Append(pressedControls[i].Control.DisplayName);
						}

						popup.SetMessage(sb);
					}
					else
						popup.ResetMessage();
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
				popup.ResetMessage();
				lastPressedControls.Clear();
				lastConPressTime.Clear();
				lastConPressCount.Clear();
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

			private readonly TexturedBox background, divider, progressBar;
			private readonly BorderBox border;
			private readonly Label header, subheader;

			public RebindHud() : base(HudMain.HighDpiRoot)
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

				_config[ZOffsetID] = sbyte.MaxValue - 1;
				_config[ZOffsetInnerID] = byte.MaxValue - 1;
				Visible = false;
			}

			public void SetMessage(StringBuilder message)
			{
				subheader.Text = message;
			}

			public void ResetMessage()
			{
				subheader.Text = "Press and hold new combo";
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