﻿using System;
using VRage;
using VRageMath;
using HudSpaceDelegate = System.Func<VRage.MyTuple<bool, float, VRageMath.MatrixD>>;

namespace RichHudFramework.UI
{
    using Client;
    using Server;
    using Internal;

    /// <summary>
    /// A clickable box. Doesn't render any textures or text. Must be used in conjunction with other elements.
    /// Events return the parent object.
    /// </summary>
    public class MouseInputElement : HudElementBase, IMouseInput
    {
        /// <summary>
        /// Invoked when the cursor enters the element's bounds
        /// </summary>
        public event EventHandler CursorEntered;

        /// <summary>
        /// Invoked when the cursor leaves the element's bounds
        /// </summary>
        public event EventHandler CursorExited;

        /// <summary>
        /// Invoked when the element is clicked with the left mouse button
        /// </summary>
        public event EventHandler LeftClicked;

        /// <summary>
        /// Invoked when the left click is released
        /// </summary>
        public event EventHandler LeftReleased;

        /// <summary>
        /// Invoked when the element is clicked with the right mouse button
        /// </summary>
        public event EventHandler RightClicked;

        /// <summary>
        /// Invoked when the right click is released
        /// </summary>
        public event EventHandler RightReleased;

        /// <summary>
        /// Invoked when taking focus
        /// </summary>
        public event EventHandler GainedFocus;

        /// <summary>
        /// Invoked when focus is lost
        /// </summary>
        public event EventHandler LostFocus;

        /// <summary>
        /// Indicates whether or not the element has input focus.
        /// </summary>
        public bool HasFocus { get { return hasFocus && Visible; } private set { hasFocus = value; } }

        /// <summary>
        /// True if the element is being clicked with the left mouse button
        /// </summary>
        public bool IsLeftClicked { get; private set; }

        /// <summary>
        /// True if the element is being clicked with the right mouse button
        /// </summary>
        public bool IsRightClicked { get; private set; }

        /// <summary>
        /// True if the element was just with the left mouse button
        /// </summary>
        public bool IsNewLeftClicked { get; private set; }

        /// <summary>
        /// True if the element was just with the right mouse button
        /// </summary>
        public bool IsNewRightClicked { get; private set; }

        private bool mouseCursorEntered;
        private bool hasFocus;
        protected readonly Action LoseFocusCallback;

        public MouseInputElement(HudParentBase parent) : base(parent)
        {
            UseCursor = true;
            ShareCursor = true;
            HasFocus = false;
            DimAlignment = DimAlignments.Both | DimAlignments.IgnorePadding;

            LoseFocusCallback = LoseFocus;
        }

        public MouseInputElement() : this(null)
        { }

        /// <summary>
        /// Clears all subscribers to mouse input events.
        /// </summary>
        public void ClearSubscribers()
        {
            CursorEntered = null;
            CursorExited = null;
            LeftClicked = null;
            LeftReleased = null;
            RightClicked = null;
            RightReleased = null;
        }

        protected override void InputDepth()
        {
            State &= ~HudElementStates.IsMouseInBounds;

            if (UseCursor && Visible && (HudSpace?.IsFacingCamera ?? false))
            {
                Vector3 cursorPos = HudSpace.CursorPos;
                Vector2 offset = Vector2.Max(cachedSize, new Vector2(minMouseBounds)) / 2f;
                BoundingBox2 box = new BoundingBox2(cachedPosition - offset, cachedPosition + offset);
                bool mouseInBounds = box.Contains(new Vector2(cursorPos.X, cursorPos.Y)) == ContainmentType.Contains
                        || (IsLeftClicked || IsRightClicked);

                if (mouseInBounds)
                {
                    State |= HudElementStates.IsMouseInBounds;
                    HudMain.Cursor.TryCaptureHudSpace(cursorPos.Z, HudSpace.GetHudSpaceFunc);
                }
            }
        }

        protected override void HandleInput(Vector2 cursorPos)
        {
            if (IsMousedOver)
            {
                if (!mouseCursorEntered)
                {
                    mouseCursorEntered = true;
                    CursorEntered?.Invoke(_parent, EventArgs.Empty);
                }

                if (SharedBinds.LeftButton.IsNewPressed)
                {
                    LeftClicked?.Invoke(_parent, EventArgs.Empty);
                    IsLeftClicked = true;
                    IsNewLeftClicked = true;
                }
                else
                    IsNewLeftClicked = false;

                if (SharedBinds.RightButton.IsNewPressed)
                {
                    RightClicked?.Invoke(_parent, EventArgs.Empty);
                    IsRightClicked = true;
                    IsNewRightClicked = true;
                }
                else
                    IsNewRightClicked = false;

                if (!HasFocus && IsNewRightClicked || IsNewLeftClicked)
                {
                    HasFocus = true;
                    HudMain.GetInputFocus(LoseFocusCallback);
                    GainedFocus?.Invoke(_parent, EventArgs.Empty);
                }
            }
            else
            {
                if (mouseCursorEntered)
                {
                    mouseCursorEntered = false;
                    CursorExited?.Invoke(_parent, EventArgs.Empty);
                }

                if (HasFocus && (SharedBinds.LeftButton.IsNewPressed || SharedBinds.RightButton.IsNewPressed))
                    LoseFocus();

                IsNewLeftClicked = false;
                IsNewRightClicked = false;
            }

            if (!SharedBinds.LeftButton.IsPressed && IsLeftClicked)
            {
                LeftReleased?.Invoke(_parent, EventArgs.Empty);
                IsLeftClicked = false;
            }

            if (!SharedBinds.RightButton.IsPressed && IsRightClicked)
            {
                RightReleased?.Invoke(_parent, EventArgs.Empty);
                IsRightClicked = false;
            }
        }

        protected virtual void LoseFocus()
        {
            HasFocus = false;
            LostFocus?.Invoke(_parent, EventArgs.Empty);
            HudMain.LoseInputFocus(LoseFocusCallback);
        }
    }
}