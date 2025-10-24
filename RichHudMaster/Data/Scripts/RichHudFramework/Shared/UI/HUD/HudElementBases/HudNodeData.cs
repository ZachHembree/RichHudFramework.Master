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
	uint[] // 3 - NodeInputMask
>;
using HudSpaceFunc = System.Func<VRageMath.Vector3D>;

namespace RichHudFramework
{
	using HudNodeData = MyTuple<
		HudNodeStateData, // 1 - { 1.1 - State, 1.2 - NodeVisibleMask, 1.3 - NodeInputMask }
		HudSpaceFunc, // 2 - GetNodeOriginFunc
		int[], // 3 - { 3.1 - zOffset, 3.2 - zOffsetInner, 3.3 - fullZOffset }
		HudNodeHookData, // 4 - Main hooks
		object, // 5 - Parent as HudNodeDataRef
		List<object> // 6 - Children as IReadOnlyList<HudNodeDataRef>
	>;
	
	namespace UI
	{
		// Read-only length-1 array containing raw UI node data
		using HudNodeDataRef = IReadOnlyList<HudNodeData>;

		/// <summary>
		/// Wrapper around a shared reference to a UI element's shared tree data
		/// </summary>
		public struct LinkedHudNode
		{
			/// <summary>
			/// Parent object of the node
			/// </summary>
			public LinkedHudNode Parent => new LinkedHudNode { dataRef = (dataRef[0].Item5 as HudNodeDataRef) };

			/// <summary>
			/// Internal state tracking flags
			/// </summary>
			public HudElementStates State => (HudElementStates)dataRef[0].Item1.Item1[0];

			/// <summary>
			/// Internal state mask for determining visibility
			/// </summary>
			public HudElementStates NodeVisibleMask => (HudElementStates)dataRef[0].Item1.Item2[0];

			/// <summary>
			/// Internal state mask for determining whether input updates are enabled
			/// </summary>
			public HudElementStates NodeInputMask => (HudElementStates)dataRef[0].Item1.Item3[0];

			/// <summary>
			/// Determines whether the UI element will be drawn in the Back, Mid or Foreground
			/// </summary>
			public sbyte ZOffset => (sbyte)dataRef[0].Item3[0];

			/// <summary>
			/// Used for input focus and window sorting
			/// </summary>
			public byte ZOffsetInner => (byte)dataRef[0].Item3[1];

			/// <summary>
			/// Combined offset used for final sorting
			/// </summary>
			public ushort FullZOffset => (ushort)dataRef[0].Item3[2];

			/// <summary>
			/// Used to check whether the cursor is moused over the element and whether its being
			/// obstructed by another element.
			/// </summary>
			public Action InputDepthCallback => dataRef[0].Item4.Item2;

			/// <summary>
			/// Updates the input of this UI element. Invocation order affected by z-Offset and depth sorting.
			/// Executes last, after Draw.
			/// </summary>
			public Action HandleInputCallback => dataRef[0].Item4.Item3;

			/// <summary>
			/// Updates the sizing of the element. Executes before layout in bottom-up order, before layout.
			/// </summary>
			public Action UpdateSizeCallback => dataRef[0].Item4.Item4;

			/// <summary>
			/// Updates the internal layout of the UI element. Executes after sizing in top-down order, before 
			/// input and draw. Not affected by depth or z-Offset sorting.
			/// </summary>
			public Action<bool> LayoutCallback => dataRef[0].Item4.Item5;

			/// <summary>
			/// Used to immediately draw billboards. Invocation order affected by z-Offset and depth sorting.
			/// Executes after Layout and before HandleInput.
			/// </summary>
			public Action DrawCallback => dataRef[0].Item4.Item6;

			/// <summary>
			/// Delegate for getting HUD space translation in world space
			/// </summary>
			public HudSpaceFunc GetHudNodeOriginFunc => dataRef[0].Item2;

			/// <summary>
			/// Debugging info delegate
			/// </summary>
			public ApiMemberAccessor GetOrSetMemberFunc => dataRef[0].Item4.Item1;

			/// <summary>
			/// Raw RHF API data
			/// </summary>
			public HudNodeDataRef dataRef;
		}
	}
}