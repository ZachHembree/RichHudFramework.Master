﻿using System;
using System.Collections.Generic;
using VRage;
using VRageMath;
using ApiMemberAccessor = System.Func<object, int, object>;

namespace RichHudFramework
{
    namespace UI
    {
        using HudUpdateAccessors = MyTuple<
            ApiMemberAccessor,
            MyTuple<Func<ushort>, Func<Vector3D>>, // ZOffset + GetOrigin
            Action, // DepthTest
            Action, // HandleInput
            Action<bool>, // BeforeLayout
            Action // BeforeDraw
        >;

        [Flags]
        public enum HudElementStates : ushort
        {
            None = 0x0,
            IsVisible = 1 << 0,
            WasParentVisible = 1 << 1,
            IsRegistered = 1 << 2,
            CanUseCursor = 1 << 3,
            CanShareCursor = 1 << 4,
            IsMousedOver = 1 << 5,
            IsMouseInBounds = 1 << 6,
            CanPreload = 1 << 7,
            IsMasked = 1 << 8,
            IsMasking = 1 << 9,
            IsSelectivelyMasked = 1 << 10,
            CanIgnoreMasking = 1 << 11,
            IsInputEnabled = 1 << 12,
            WasParentInputEnabled = 1 << 13,
            IsInitialized = 1 << 14,
        }

        public struct HudLayerData
        {
            public sbyte zOffset;

            /// <summary>
            /// Additional zOffset range used internally; primarily for determining window draw order.
            /// Don't use this unless you have a good reason for it.
            /// </summary>
            public byte zOffsetInner;

            public ushort fullZOffset;
        }

        public enum HudElementAccessors : int
        {
            /// <summary>
            /// out: string
            /// </summary>
            ModName = 0,

            /// <summary>
            /// out: System.Type
            /// </summary>
            GetType = 1,

            /// <summary>
            /// out: byte
            /// </summary>
            ZOffset = 2,

            /// <summary>
            /// out: ushort
            /// </summary>
            FullZOffset = 3,

            /// <summary>
            /// out: Vector2
            /// </summary>
            Position = 4,

            /// <summary>
            /// out: Vector2
            /// </summary>
            Size = 5,

            /// <summary>
            /// out: Vector3
            /// </summary>
            LocalCursorPos = 6,

            /// <summary>
            /// out: bool
            /// </summary>
            DrawCursorInHudSpace = 7,

            /// <summary>
            /// out: HudSpaceDelegate
            /// </summary>
            GetHudSpaceFunc = 8,

            /// <summary>
            /// out: Vector3D
            /// </summary>
            NodeOrigin = 9,

            /// <summary>
            /// out: MatrixD
            /// </summary>
            PlaneToWorld = 10,

            /// <summary>
            /// out: bool
            /// </summary>
            IsInFront = 11,

            /// <summary>
            /// out: bool
            /// </summary>
            IsFacingCamera = 12,
        }

        /// <summary>
        /// Read-only interface for types capable of serving as parent objects to <see cref="HudNodeBase"/>s.
        /// </summary>
        public interface IReadOnlyHudParent
        {
            /// <summary>
            /// Node defining the coordinate space used to render the UI element
            /// </summary>
            IReadOnlyHudSpaceNode HudSpace { get; }

            /// <summary>
            /// Returns true if the element can be drawn and/or accept input
            /// </summary>
            bool Visible { get; }

            /// <summary>
            /// Returns true if input is enabled can update
            /// </summary>
            bool InputEnabled { get; }

            /// <summary>
            /// Used to change the draw order of the UI element. Lower offsets place the element
            /// further in the background. Higher offsets draw later and on top.
            /// </summary>
            sbyte ZOffset { get; }

            /// <summary>
            /// Adds update delegates for members in the order dictated by the UI tree
            /// </summary>
            void GetUpdateAccessors(List<HudUpdateAccessors> UpdateActions, byte preloadDepth);
        }
    }
}