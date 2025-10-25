using RichHudFramework.Internal;
using System;
using System.Collections.Generic;
using VRage;
using VRageMath;
using ApiMemberAccessor = System.Func<object, int, object>;
using HudNodeHookData = VRage.MyTuple<
	System.Func<object, int, object>, // 1 -  GetOrSetApiMemberFunc
	System.Action, // 2 - InputDepthAction
	System.Action, // 3 - InputAction
	System.Action, // 4 - SizingAction
	System.Action<bool>, // 5 - LayoutAction
	System.Action // 6 - DrawAction
>;
using HudNodeStateData = VRage.MyTuple<
	uint[], // 1 - State
	uint[], // 2 - NodeVisibleMask
	uint[], // 3 - NodeInputMask
	System.Func<VRageMath.Vector3D>[],  // 4 - GetNodeOriginFunc
	int[] // 5 - { 5.0 - zOffset, 5.1 - zOffsetInner, 5.2 - fullZOffset }
>;
using HudSpaceOriginFunc = System.Func<VRageMath.Vector3D>;

namespace RichHudFramework
{
	using HudNodeData = MyTuple<
		HudNodeStateData, // 1 - { 1.1 - State, 1.2 - NodeVisibleMask, 1.3 - NodeInputMask, 1.4 - GetNodeOriginFunc, 1.5 - ZOffsets }
		HudNodeHookData, // 2 - Main hooks
		object, // 3 - Parent as HudNodeDataHandle
		List<object>, // 4 - Children as IReadOnlyList<HudNodeDataHandle>
		object // 5 - Unused
	>;

	namespace UI
	{
		using Server;
		using Client;
		using Internal;

		// Read-only length-1 array containing raw UI node data
		using HudNodeDataHandle = IReadOnlyList<HudNodeData>;

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
				get { return (State[0] & NodeVisibleMask[0]) == NodeVisibleMask[0]; }
				set
				{
					if (value)
						State[0] |= (uint)HudElementStates.IsVisible;
					else
						State[0] &= ~(uint)HudElementStates.IsVisible;
				}
			}

			/// <summary>
			/// Returns true if input is enabled can update
			/// </summary>
			public bool InputEnabled
			{
				get { return (State[0] & NodeInputMask[0]) == NodeInputMask[0]; }
				set
				{
					if (value)
						State[0] |= (uint)HudElementStates.IsInputEnabled;
					else
						State[0] &= ~(uint)HudElementStates.IsInputEnabled;
				}
			}

			/// <summary>
			/// Determines whether the UI element will be drawn in the Back, Mid or Foreground
			/// </summary>
			public sbyte ZOffset
			{
				get { return (sbyte)layerData[0]; }
				set { layerData[0] = (int)value; }
			}

			// Custom Update Hooks - inject custom updates and polling here
			#region CUSTOM UPDATE HOOKS

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

			#endregion

			// INTERNAL DATA - DO NOT TOUCH
			#region INTERNAL DATA

			/// <summary>
			/// Handle to node data used for registering with the Tree Manager
			/// </summary>
			public HudNodeDataHandle DataHandle { get; }

			/// <summary>
			/// Internal state tracking flags
			/// </summary>
			public uint[] State { get; }

			/// <summary>
			/// Internal state mask for determining visibility
			/// </summary>
			public uint[] NodeVisibleMask { get; }

			/// <summary>
			/// Internal state mask for determining whether input updates are enabled
			/// </summary>
			public uint[] NodeInputMask { get; }

			/// <summary>
			/// Internal layering keys
			/// </summary>
			protected readonly int[] layerData;

			protected readonly HudNodeData[] _dataHandle;
			protected readonly List<object> childHandles;
			protected readonly List<HudNodeBase> children;
			protected readonly HudSpaceOriginFunc[] hudSpaceOriginFunc;

			#endregion

			public HudParentBase()
			{
				// Storage init
				children = new List<HudNodeBase>();
				childHandles = new List<object>();

				State = new uint[1];
				NodeVisibleMask = new uint[1];
				NodeInputMask = new uint[1];
				layerData = new int[3];
				hudSpaceOriginFunc = new HudSpaceOriginFunc[1];

				// Shared data handle
				_dataHandle = new HudNodeData[1];
				// Shared state
				_dataHandle[0].Item1 = new HudNodeStateData(State, NodeVisibleMask, NodeInputMask, hudSpaceOriginFunc, layerData);
				// Hooks
				_dataHandle[0].Item2.Item1 = GetOrSetApiMember;
				_dataHandle[0].Item2.Item2 = BeginInputDepth;
				_dataHandle[0].Item2.Item3 = BeginInput;
				_dataHandle[0].Item2.Item4 = BeginSizing;
				_dataHandle[0].Item2.Item5 = BeginLayout;
				_dataHandle[0].Item2.Item6 = BeginDraw;
				// Parent
				_dataHandle[0].Item3 = null;
				// Child handle list
				_dataHandle[0].Item4 = childHandles;
				DataHandle = _dataHandle;

				// Initial state
				NodeVisibleMask[0] = (uint)HudElementStates.IsVisible;
				NodeInputMask[0] = (uint)HudElementStates.IsInputEnabled;
				State[0] = (uint)(HudElementStates.IsRegistered | HudElementStates.IsInputEnabled | HudElementStates.IsVisible);
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
						bool canUseCursor = (State[0] & (uint)HudElementStates.CanUseCursor) > 0,
							isVisible = (State[0] & NodeVisibleMask[0]) == NodeVisibleMask[0],
							isInputEnabled = (State[0] & NodeInputMask[0]) == NodeInputMask[0];

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
						bool isVisible = (State[0] & NodeVisibleMask[0]) == NodeVisibleMask[0],
							 isInputEnabled = (State[0] & NodeInputMask[0]) == NodeInputMask[0];

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

			public virtual void BeginSizing()
			{
				if (!ExceptionHandler.ClientsPaused)
				{
					try
					{
						bool isVisible = (State[0] & NodeVisibleMask[0]) == NodeVisibleMask[0];

						if (isVisible)
						{
							UpdateSizeCallback?.Invoke();
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
			public virtual void BeginLayout(bool _)
			{
				if (!ExceptionHandler.ClientsPaused)
				{
					layerData[2] = ParentUtils.GetFullZOffset(layerData);

					if (LayoutCallback == null)
					{
						State[0] |= (uint)HudElementStates.IsLayoutReady;
						return;
					}
					else
						State[0] &= ~(uint)HudElementStates.IsLayoutReady;

					try
					{
						bool isVisible = (State[0] & NodeVisibleMask[0]) == NodeVisibleMask[0];

						if (isVisible)
						{
							LayoutCallback?.Invoke();
							State[0] |= (uint)HudElementStates.IsLayoutReady;
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
						bool isVisible = (State[0] & NodeVisibleMask[0]) == NodeVisibleMask[0];

						if (isVisible && (State[0] & (uint)HudElementStates.IsLayoutReady) > 0)
							DrawCallback();
					}
					catch (Exception e)
					{
						ExceptionHandler.ReportException(e);
					}
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
					child._dataHandle[0].Item3 = DataHandle;
					children.Add(child);
					childHandles.Add(child.DataHandle);
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
				{
					child._dataHandle[0].Item3 = null;
					childHandles.Remove(child.DataHandle);
					return children.Remove(child);
				}
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
						return (sbyte)ZOffset;
					case HudElementAccessors.FullZOffset:
						return (ushort)layerData[2];
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