using System;
using System.Collections.Generic;
using VRage;
using VRageMath;
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

		/// <summary>
		/// Base class for hud elements that can be parented to other elements.
		/// </summary>
		public abstract partial class HudNodeBase : HudParentBase, IReadOnlyHudNode
		{
			protected const uint
				nodeVisible = (uint)(HudElementStates.IsVisible | HudElementStates.WasParentVisible),
				nodeInputEnabled = (uint)(HudElementStates.IsInputEnabled | HudElementStates.WasParentInputEnabled);
			protected const int maxPreloadDepth = 5;

			/// <summary>
			/// Read-only parent object of the node.
			/// </summary>
			IReadOnlyHudParent IReadOnlyHudNode.Parent => _parent;

			/// <summary>
			/// Parent object of the node.
			/// </summary>
			public virtual HudParentBase Parent { get { return _parent; } protected set { _parent = value; } }

			/// <summary>
			/// Indicates whether or not the element has been registered to a parent.
			/// </summary>
			public bool Registered => (State[0] & (uint)HudElementStates.IsRegistered) > 0;

			protected HudParentBase _parent;

			public HudNodeBase(HudParentBase parent)
			{
				NodeVisibleMask[0] = nodeVisible;
				NodeInputMask[0] = nodeInputEnabled;
				State[0] = (uint)(HudElementStates.WasParentVisible | HudElementStates.IsInputEnabled | HudElementStates.IsVisible);

				Register(parent);
			}

			/// <summary>
			/// Starts input update in a try-catch block. Useful for manually updating UI elements.
			/// Exceptions are reported client-side. Do not override this unless you have a good reason for it.
			/// If you need to update input, use HandleInputCallback.
			/// </summary>
			public override void BeginInput()
			{
				if (!ExceptionHandler.ClientsPaused)
				{
					try
					{
						if (_parent != null && (_parent.State[0] & _parent.NodeInputMask[0]) == _parent.NodeInputMask[0])
							State[0] |= (uint)HudElementStates.WasParentInputEnabled;
						else
							State[0] &= ~(uint)HudElementStates.WasParentInputEnabled;

						bool isVisible = (State[0] & NodeVisibleMask[0]) == NodeVisibleMask[0],
							 isInputEnabled = (State[0] & NodeInputMask[0]) == NodeInputMask[0];

						if (HandleInputCallback != null && isVisible && isInputEnabled)
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
			/// Updates layout for the element and its children. Overriding this method is rarely necessary. 
			/// If you need to update layout, use LayoutCallback.
			/// </summary>
			public override void BeginLayout(bool _)
			{
				if (!ExceptionHandler.ClientsPaused)
				{
					try
					{
						bool isVisible = (State[0] & NodeVisibleMask[0]) == NodeVisibleMask[0];
						State[0] &= ~(uint)HudElementStates.IsLayoutReady;

						if (isVisible)
						{
							LayoutCallback?.Invoke();
							State[0] |= (uint)HudElementStates.IsLayoutReady;
						}

						// Parent visibility flags need to propagate in top-down order, meaning they can only be evaluated
						// during Layout/Arrange, but Layout should not run before UpdateSize. They need to be delayed.
						HudSpace = _parent?.HudSpace;

						if (_parent != null && (_parent.State[0] & _parent.NodeVisibleMask[0]) == _parent.NodeVisibleMask[0])
							State[0] |= (uint)HudElementStates.WasParentVisible;
						else
							State[0] &= ~(uint)HudElementStates.WasParentVisible;

						layerData[2] = ParentUtils.GetFullZOffset(layerData, _parent);
					}
					catch (Exception e)
					{
						ExceptionHandler.ReportException(e);
					}
				}
			}

			/// <summary>
			/// Registers the element to the given parent object.
			/// </summary>
			/// <param name="canPreload">Indicates whether or not the element's accessors can be loaded into the update tree
			/// before the element is visible. Useful for preventing flicker in scrolling lists.</param>
			public virtual bool Register(HudParentBase newParent, bool canPreload = false)
			{
				if (newParent == this)
					throw new Exception("Types of HudNodeBase cannot be parented to themselves!");

				if (newParent != null)
				{
					Parent = newParent;

					if (_parent.RegisterChild(this))
						State[0] |= (uint)HudElementStates.IsRegistered;
					else
						State[0] &= ~(uint)HudElementStates.IsRegistered;
				}

				if ((State[0] & (uint)HudElementStates.IsRegistered) > 0)
				{
					State[0] &= ~(uint)HudElementStates.WasParentVisible;

					if (canPreload)
						State[0] |= (uint)HudElementStates.CanPreload;
					else
						State[0] &= ~(uint)HudElementStates.CanPreload;

					return true;
				}
				else
					return false;
			}

			/// <summary>
			/// Unregisters the element from its parent, if it has one.
			/// </summary>
			public virtual bool Unregister()
			{
				if (Parent != null)
				{
					HudParentBase lastParent = Parent;
					Parent = null;

					lastParent.RemoveChild(this);
					State[0] &= (uint)~(HudElementStates.IsRegistered | HudElementStates.WasParentVisible);
				}

				return !((State[0] & (uint)HudElementStates.IsRegistered) > 0);
			}
		}
	}
}