using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices.ComTypes;
using VRage;
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

	namespace UI.Server
	{
		// Read-only length-1 array containing raw UI node data
		using HudNodeDataHandle = IReadOnlyList<HudNodeData>;

		public sealed partial class HudMain
		{
			/// <summary>
			/// Version agnostic node data format
			/// </summary>
			public struct TreeNodeData
			{
				public HudNodeDataHandle Node;
				public HudNodeHookData Hooks;
				public HudSpaceOriginFunc GetPosFunc;
				public ushort ZOffset;
			}

			public class HudNodeIterator
			{
				private const int maxPreloadDepth = 5;

				struct NodeStackData
				{
					/// <summary>
					/// Handle to UI node data
					/// </summary>
					public HudNodeDataHandle node;

					/// <summary>
					/// Recursive depth of the node in the tree
					/// </summary>
					public int depth;

					/// <summary>
					/// Recursive iteration count since the first preloaded, but 
					/// invisible parent.
					/// </summary>
					public byte preloadDepth;
				}

				private readonly List<NodeStackData> nodeStack;

				public HudNodeIterator()
				{
					nodeStack = new List<NodeStackData>(100);
				}

				public void Clear()
				{
					nodeStack.Clear();
				}

				public void GetNodeData(HudNodeDataHandle srcRoot, List<TreeNodeData> dst)
				{
					Clear();

					// Push root onto the stack
					nodeStack.Add(new NodeStackData
					{
						node = srcRoot,
						depth = 0,
						preloadDepth = 0
					});

					// Depth-first iteration
					int lastDepth = 0;

					while (nodeStack.Count > 0)
					{
						var stack = nodeStack.Pop();
						lastDepth = stack.depth;

						HudNodeStateData stateData = stack.node[0].Item1;

						HudElementStates state = (HudElementStates)stateData.Item1[0];
						HudElementStates visMask = (HudElementStates)stateData.Item2[0];
						state |= HudElementStates.WasParentVisible;

						// Set temporary preload flags and track depth
						if ((state & HudElementStates.IsVisible) == 0 && (state & HudElementStates.CanPreload) > 0)
							stack.preloadDepth++;

						if (stack.preloadDepth < maxPreloadDepth && (state & HudElementStates.CanPreload) > 0)
							state |= HudElementStates.IsVisible;

						// Check visibility
						if ((state & visMask) == visMask)
						{
							HudNodeDataHandle parent = stack.node[0].Item3 as HudNodeDataHandle;

							// Propagate HUD Space Node pos func
							if (parent != null && (state & HudElementStates.IsSpaceNode) == 0)
							{
								stateData.Item4[0] = parent[0].Item1.Item4[0];
							}

							// Update combined ZOffset for layer sorting
							int[] layerData = stateData.Item5;

							{
								byte outerOffset = (byte)(layerData[0] - sbyte.MinValue);
								ushort innerOffset = (ushort)(layerData[1] << 8);

								if (parent != null)
								{
									ushort parentFull = (ushort)parent[0].Item1.Item5[2];

									outerOffset += (byte)((parentFull & 0x00FF) + sbyte.MinValue);
									innerOffset += (ushort)(parentFull & 0xFF00);
								}

								layerData[2] = (ushort)(innerOffset | outerOffset);
							}

							// Add node
							dst.Add(new TreeNodeData
							{
								Node = stack.node,
								Hooks = stack.node[0].Item2,
								GetPosFunc = stateData.Item4[0],
								ZOffset = (ushort)layerData[2]
							});

							// Push children
							var children = stack.node[0].Item4;

							for (int i = children.Count - 1; i >= 0; i--)
							{
								nodeStack.Add(new NodeStackData
								{
									node = (HudNodeDataHandle)children[i],
									depth = lastDepth + 1,
									preloadDepth = stack.preloadDepth
								});
							}
						}
					}
				}

				public void UpdateNodeSizing(IReadOnlyList<TreeNodeData> clients, IReadOnlyList<uint> sizingActions)
				{
					foreach (uint index in sizingActions)
					{
						HudNodeDataHandle node = clients[(int)index].Node;
						Action SizingAction = clients[(int)index].Hooks.Item4;

						SizingAction?.Invoke();
					}
				}

				public void UpdateNodeLayout(IReadOnlyList<TreeNodeData> clients, IReadOnlyList<uint> layoutActions, bool refresh = false)
				{
					foreach (uint index in layoutActions)
					{
						HudNodeDataHandle node = clients[(int)index].Node;
						Action<bool> LayoutAction = clients[(int)index].Hooks.Item5;

						LayoutAction?.Invoke(refresh);
					}
				}

				public void DrawNodes(IReadOnlyList<TreeNodeData> clients, IReadOnlyList<uint> drawActions)
				{
					foreach (uint index in drawActions)
					{
						HudNodeDataHandle node = clients[(int)index].Node;
						Action DrawAction = clients[(int)index].Hooks.Item6;

						DrawAction?.Invoke();
					}
				}

				public void UpdateNodeInputDepth(IReadOnlyList<TreeNodeData> clients, IReadOnlyList<uint> depthTestActions)
				{
					foreach (uint index in depthTestActions)
					{
						HudNodeDataHandle node = clients[(int)index].Node;
						Action DepthTestAction = clients[(int)index].Hooks.Item2;

						DepthTestAction?.Invoke();
					}
				}

				public void UpdateNodeInput(IReadOnlyList<TreeNodeData> clients, IReadOnlyList<uint> inputActions)
				{
					foreach (uint index in inputActions)
					{
						HudNodeDataHandle node = clients[(int)index].Node;
						Action InputAction = clients[(int)index].Hooks.Item3;

						InputAction?.Invoke();
					}
				}
			}
		}
	}
}