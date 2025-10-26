using System;
using VRageMath;

namespace RichHudFramework
{
	namespace UI
	{
		using static RichHudFramework.UI.NodeConfigIndices;
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
			IReadOnlyHudParent IReadOnlyHudNode.Parent => Parent;

			/// <summary>
			/// Parent object of the node.
			/// </summary>
			public HudParentBase Parent { get; set; }

			/// <summary>
			/// Indicates whether or not the element has been registered to a parent.
			/// </summary>
			public bool Registered => (Config[StateID] & (uint)HudElementStates.IsRegistered) > 0;

			public HudNodeBase(HudParentBase parent)
			{
				Config[VisMaskID] = nodeVisible;
				Config[InputMaskID] = nodeInputEnabled;
				Config[StateID] = (uint)(HudElementStates.IsInputEnabled | HudElementStates.IsVisible);

				Register(parent);
			}

			/// <summary>
			/// Starts input update in a try-catch block. Useful for manually updating UI elements.
			/// Exceptions are reported client-side. Do not override this unless you have a good reason for it.
			/// If you need to update input, use HandleInputCallback.
			/// </summary>
			public override void BeginInput()
			{
				Vector3 cursorPos = HudSpace.CursorPos;
				_handleInputCallback?.Invoke(new Vector2(cursorPos.X, cursorPos.Y));
			}

			/// <summary>
			/// Updates layout for the element and its children. Overriding this method is rarely necessary. 
			/// If you need to update layout, use LayoutCallback.
			/// </summary>
			public override void BeginLayout(bool _)
			{
				if ((Config[StateID] & (uint)HudElementStates.IsSpaceNode) == 0)
					HudSpace = Parent?.HudSpace;

				if (HudSpace != null)
					Config[StateID] |= (uint)HudElementStates.IsSpaceNodeReady;
				else
					Config[StateID] &= ~(uint)HudElementStates.IsSpaceNodeReady;

				LayoutCallback?.Invoke();
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

					if (Parent.RegisterChild(this))
						Config[StateID] |= (uint)HudElementStates.IsRegistered;
					else
						Config[StateID] &= ~(uint)HudElementStates.IsRegistered;
				}

				if ((Config[StateID] & (uint)HudElementStates.IsRegistered) > 0)
				{
					Config[StateID] &= ~(uint)HudElementStates.WasParentVisible;

					if (canPreload)
						Config[StateID] |= (uint)HudElementStates.CanPreload;
					else
						Config[StateID] &= ~(uint)HudElementStates.CanPreload;

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
					Config[StateID] &= (uint)~(HudElementStates.IsRegistered | HudElementStates.WasParentVisible);
				}

				return !((Config[StateID] & (uint)HudElementStates.IsRegistered) > 0);
			}
		}
	}
}