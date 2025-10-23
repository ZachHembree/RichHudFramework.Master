using RichHudFramework.Internal;
using System;
using System.Collections.Generic;
using VRage;
using VRageMath;
using ApiMemberAccessor = System.Func<object, int, object>;
using HudSpaceDelegate = System.Func<VRage.MyTuple<bool, float, VRageMath.MatrixD>>;

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

        /// <summary>
        /// Base class for HUD elements to which other elements are parented. Types deriving from this class cannot be
        /// parented to other elements; only types of <see cref="HudNodeBase"/> can be parented.
        /// </summary>
        public abstract partial class HudParentBase : IReadOnlyHudParent
        {
            /// <summary>
            /// Node defining the coordinate space used to render the UI element
            /// </summary>
            public virtual IReadOnlyHudSpaceNode HudSpace { get; protected set; }

            /// <summary>
            /// Returns true if the element can be drawn and/or accept input
            /// </summary>
            public bool Visible
            {
                get { return (State & NodeVisibleMask) == NodeVisibleMask; }
                set
                {
                    if (value)
                        State |= HudElementStates.IsVisible;
                    else
                        State &= ~HudElementStates.IsVisible;
                }
            }

            /// <summary>
            /// Returns true if input is enabled can update
            /// </summary>
            public bool InputEnabled
            {
                get { return (State & NodeInputMask) == NodeInputMask; }
                set
                {
                    if (value)
                        State |= HudElementStates.IsInputEnabled;
                    else
                        State &= ~HudElementStates.IsInputEnabled;
                }
            }

            /// <summary>
            /// Determines whether the UI element will be drawn in the Back, Mid or Foreground
            /// </summary>
            public sbyte ZOffset
            {
                get { return layerData.zOffset; }
                set { layerData.zOffset = value; }
            }

			// Custom Update Hooks - inject custom updates and polling here

			/// <summary>
			/// Used to check whether the cursor is moused over the element and whether its being
			/// obstructed by another element.
			/// </summary>
			protected Action InputDepthCallback;

			/// <summary>
			/// Updates the input of this UI element. Invocation order affected by z-Offset and depth sorting.
			/// Executes last, after Draw.
			/// </summary>
			protected Action<Vector2> HandleInputCallback;

			/// <summary>
			/// Updates the sizing of the element. Executes before layout in bottom-up order, before layout.
			/// </summary>
			protected Action UpdateSizeCallback;

			/// <summary>
			/// Updates the internal layout of the UI element. Executes after sizing in top-down order, before 
			/// input and draw. Not affected by depth or z-Offset sorting.
			/// </summary>
			protected Action LayoutCallback;

			/// <summary>
			/// Used to immediately draw billboards. Invocation order affected by z-Offset and depth sorting.
			/// Executes after Layout and before HandleInput.
			/// </summary>
			protected Action DrawCallback;

			/// <summary>
			/// Internal state tracking flags
			/// </summary>
			public HudElementStates State { get; protected set; }

            /// <summary>
            /// Internal state mask for determining visibility
            /// </summary>
            public HudElementStates NodeVisibleMask { get; protected set; }

            /// <summary>
            /// Internal state mask for determining whether input updates are enabled
            /// </summary>
            public HudElementStates NodeInputMask { get; protected set; }

			// Internal callbacks - DO NOT TOUCH - SHOO - AVERT YOUR EYES
			protected ApiMemberAccessor GetOrSetApiMemberFunc { get; private set; }
			protected Action BeginInputDepthAction { get; private set; }
			protected Action BeginInputAction { get; private set; }
			protected Action<bool> BeginLayoutAction { get; private set; }
			protected Action BeginDrawAction { get; private set; }

			protected HudLayerData layerData;
			protected readonly List<HudNodeBase> children;

			public HudParentBase()
            {
                NodeVisibleMask = HudElementStates.IsVisible;
                NodeInputMask = HudElementStates.IsInputEnabled;
                State = HudElementStates.IsRegistered | HudElementStates.IsInputEnabled | HudElementStates.IsVisible;

                children = new List<HudNodeBase>();
                GetOrSetApiMemberFunc = GetOrSetApiMember;
                BeginInputDepthAction = BeginInputDepth;
                BeginInputAction = BeginInput;
                BeginLayoutAction = BeginLayout;
                BeginDrawAction = BeginDraw;
            }

            /// <summary>
            /// Starts cursor depth check in a try-catch block. Useful for manually updating UI elements.
            /// Exceptions are reported client-side. Do not override this unless you have a good reason for it.
            /// If you need to do cursor depth testing use InputDepthCallback;
            /// </summary>
            public void BeginInputDepth()
            {
                if (!ExceptionHandler.ClientsPaused && InputDepthCallback != null)
                {
                    try
                    {
                        bool canUseCursor = (State & HudElementStates.CanUseCursor) > 0,
                            isVisible = (State & NodeVisibleMask) == NodeVisibleMask,
                            isInputEnabled = (State & NodeInputMask) == NodeInputMask;

                        if (canUseCursor && isVisible && isInputEnabled)
							InputDepthCallback();
                    }
                    catch (Exception e)
                    {
                        ExceptionHandler.ReportException(e);
                    }
                }
            }

            /// <summary>
            /// Starts input update in a try-catch block. Useful for manually updating UI elements.
            /// Exceptions are reported client-side. Do not override this unless you have a good reason for it.
            /// If you need to update input, use HandleInputCallback.
            /// </summary>
            public virtual void BeginInput()
            {
                if (!ExceptionHandler.ClientsPaused && HandleInputCallback != null)
                {
                    try
                    {
                        bool isVisible = (State & NodeVisibleMask) == NodeVisibleMask,
                             isInputEnabled = (State & NodeInputMask) == NodeInputMask;

                        if (isVisible && isInputEnabled)
                        {
                            Vector3 cursorPos = HudSpace.CursorPos;
							HandleInputCallback(new Vector2(cursorPos.X, cursorPos.Y));
                        }
                    }
                    catch (Exception e)
                    {
                        ExceptionHandler.ReportException(e);
                    }
                }
            }

            /// <summary>
            /// Starts layout update in a try-catch block. Useful for manually updating UI elements.
            /// Exceptions are reported client-side. Do not override this unless you have a good reason for it.
            /// If you need to update layout, use LayoutCallback.
            /// </summary>
            public virtual void BeginLayout(bool isArranging)
            {
				if (!ExceptionHandler.ClientsPaused)
                {
					if (isArranging)
					{
						layerData.fullZOffset = ParentUtils.GetFullZOffset(layerData);
					}

                    if (UpdateSizeCallback == null && LayoutCallback == null)
                    {
                        State |= HudElementStates.IsLayoutReady;
                        return;
                    }
                    else
                        State &= ~HudElementStates.IsLayoutReady;

                    try
                    {
                        bool isVisible = (State & NodeVisibleMask) == NodeVisibleMask;

                        if (isVisible)
                        {
                            if (!isArranging)
                                UpdateSizeCallback?.Invoke();
                            else
                            {
                                LayoutCallback?.Invoke();
                                State |= HudElementStates.IsLayoutReady;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        ExceptionHandler.ReportException(e);
                    }
                }
            }

            /// <summary>
            /// Starts UI draw in a try-catch block. Useful for manually updating UI elements.
            /// Exceptions are reported client-side. Do not override this unless you have a good reason for it.
            /// If you need to draw billboards, use DrawCallback.
            /// </summary>
            public void BeginDraw()
            {
                if (!ExceptionHandler.ClientsPaused && DrawCallback != null)
                {
                    try
                    {
                        bool isVisible = (State & NodeVisibleMask) == NodeVisibleMask;

                        if (isVisible && (State & HudElementStates.IsLayoutReady) > 0)
							DrawCallback();
                    }
                    catch (Exception e)
                    {
                        ExceptionHandler.ReportException(e);
                    }
                }
            }

            /// <summary>
            /// Adds update delegates for members in the order dictated by the UI tree
            /// </summary>
            public virtual void GetUpdateAccessors(List<HudUpdateAccessors> UpdateActions, byte preloadDepth)
            {
                if ((State & NodeVisibleMask) == NodeVisibleMask)
                {
                    bool isInputEnabled = (State & NodeInputMask) == NodeInputMask,
                        canUseCursor = isInputEnabled && (State & HudElementStates.CanUseCursor) > 0;

					layerData.fullZOffset = ParentUtils.GetFullZOffset(layerData);
                    var accessors = new HudUpdateAccessors()
                    {
                        Item1 = GetOrSetApiMemberFunc,
                        Item2 = new MyTuple<Func<ushort>, Func<Vector3D>>(() => layerData.fullZOffset, HudSpace.GetNodeOriginFunc),
                        Item3 = (InputDepthCallback != null && canUseCursor) ? BeginInputDepthAction : null,
                        Item4 = HandleInputCallback != null ? BeginInputAction : null,
                        Item5 = BeginLayoutAction,
                        Item6 = DrawCallback != null ? BeginDrawAction : null
					};

					UpdateActions.EnsureCapacity(UpdateActions.Count + children.Count + 1);
					UpdateActions.Add(accessors);

                    for (int n = 0; n < children.Count; n++)
                        children[n].GetUpdateAccessors(UpdateActions, preloadDepth);
                }
            }

            /// <summary>
            /// Registers a child node to the object.
            /// </summary>
            /// <param name="preregister">Adds the element to the update tree without registering.</param>
            public virtual bool RegisterChild(HudNodeBase child)
            {
                if (child.Parent == this && !child.Registered)
                {
                    children.Add(child);
                    return true;
                }
                else if (child.Parent == null)
                    return child.Register(this);
                else
                    return false;
            }

            /// <summary>
            /// Unregisters the specified node from the parent.
            /// </summary>
            /// <param name="fast">Prevents registration from triggering a draw list
            /// update. Meant to be used in conjunction with pooled elements being
            /// unregistered/reregistered to the same parent.</param>
            public virtual bool RemoveChild(HudNodeBase child)
            {
                if (child.Parent == this)
                    return child.Unregister();
                else if (child.Parent == null)
                    return children.Remove(child);
                else
                    return false;
            }

            protected virtual object GetOrSetApiMember(object data, int memberEnum)
            {
                switch ((HudElementAccessors)memberEnum)
                {
                    case HudElementAccessors.GetType:
                        return GetType();
                    case HudElementAccessors.ZOffset:
                        return ZOffset;
                    case HudElementAccessors.FullZOffset:
                        return layerData.fullZOffset;
                    case HudElementAccessors.Position:
                        return Vector2.Zero;
                    case HudElementAccessors.Size:
                        return Vector2.Zero;
                    case HudElementAccessors.GetHudSpaceFunc:
                        return HudSpace?.GetHudSpaceFunc;
                    case HudElementAccessors.ModName:
                        return ExceptionHandler.ModName;
                    case HudElementAccessors.LocalCursorPos:
                        return HudSpace?.CursorPos ?? Vector3.Zero;
                    case HudElementAccessors.PlaneToWorld:
                        return HudSpace?.PlaneToWorldRef[0] ?? default(MatrixD);
                    case HudElementAccessors.IsInFront:
                        return HudSpace?.IsInFront ?? false;
                    case HudElementAccessors.IsFacingCamera:
                        return HudSpace?.IsFacingCamera ?? false;
                    case HudElementAccessors.NodeOrigin:
                        return HudSpace?.PlaneToWorldRef[0].Translation ?? Vector3D.Zero;
                }

                return null;
            }
        }
    }
}