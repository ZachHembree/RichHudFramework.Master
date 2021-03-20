﻿using System;
using VRage;

namespace RichHudFramework
{
    public delegate void EventHandler(object sender, EventArgs e);

    namespace UI
    {
        /// <summary>
        /// Interface for mouse input of a UI element.
        /// </summary>
        public interface IMouseInput
        {
            /// <summary>
            /// Invoked when the cursor enters the element's bounds
            /// </summary>
            event EventHandler CursorEntered;

            /// <summary>
            /// Invoked when the cursor leaves the element's bounds
            /// </summary>
            event EventHandler CursorExited;

            /// <summary>
            /// Invoked when the element is clicked with the left mouse button
            /// </summary>
            event EventHandler LeftClicked;

            /// <summary>
            /// Invoked when the left click is released
            /// </summary>
            event EventHandler LeftReleased;

            /// <summary>
            /// Invoked when the element is clicked with the right mouse button
            /// </summary>
            event EventHandler RightClicked;

            /// <summary>
            /// Invoked when the right click is released
            /// </summary>
            event EventHandler RightReleased;

            /// <summary>
            /// Invoked when taking focus
            /// </summary>
            event EventHandler GainedFocus;

            /// <summary>
            /// Invoked when focus is lost
            /// </summary>
            event EventHandler LostFocus;

            /// <summary>
            /// True if the element is being clicked with the left mouse button
            /// </summary>
            bool IsLeftClicked { get; }

            /// <summary>
            /// True if the element is being clicked with the right mouse button
            /// </summary>
            bool IsRightClicked { get; }

            /// <summary>
            /// True if the element was just with the left mouse button
            /// </summary>
            bool IsNewLeftClicked { get; }

            /// <summary>
            /// True if the element was just with the right mouse button
            /// </summary>
            bool IsNewRightClicked { get; }

            /// <summary>
            /// Indicates whether or not the cursor is currently over this element.
            /// </summary>
            bool HasFocus { get; }

            /// <summary>
            /// Returns true if the element is moused over
            /// </summary>
            bool IsMousedOver { get; }

            /// <summary>
            /// Clears all subscribers to mouse input events.
            /// </summary>
            void ClearSubscribers();
        }

        public interface IClickableElement : IReadOnlyHudElement
        {
            IMouseInput MouseInput { get; }
        }
    }
}