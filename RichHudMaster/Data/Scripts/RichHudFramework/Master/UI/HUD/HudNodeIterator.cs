using RichHudFramework.Internal;
using RichHudFramework.Server;
using System;
using System.Collections.Generic;
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
			/// Enables or disables automatic UI preloading
			/// </summary>
			public static bool DefaultPreload = false;

			/// <summary>
			/// Version agnostic node data format
			/// </summary>
			public struct TreeNodeData
			{
				public HudNodeDataHandle Node;
				public HudNodeHookData Hooks;
				public HudSpaceOriginFunc GetPosFunc;
				public ushort ZOffset;
				public TreeClient Client;
			}

			public class HudNodeIterator
			{
				private const int maxPreloadDepth = 5;
				private const HudElementStates 
					nodeReadyFlags = HudElementStates.IsLayoutReady | HudElementStates.IsSpaceNodeReady,
					nodeNotReadyFlags = HudElementStates.IsDisjoint;

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

				/// <summary>
				/// Iterates over UI tree beginning at the given root node and writes it into a flattened list in 
				/// depth first order.
				/// </summary>
				public void GetNodeData(HudNodeDataHandle srcRoot, List<TreeNodeData> dst, TreeClient client = null)
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

						bool canPreload = DefaultPreload || (state & HudElementStates.CanPreload) > 0;

						// Track depth and preloading
						if ((state & HudElementStates.IsVisible) == 0 && canPreload)
							stack.preloadDepth++;

						if (stack.preloadDepth < maxPreloadDepth && canPreload)
							state |= HudElementStates.IsVisible;

						// Check visibility
						if ((state & visMask) == visMask)
						{
							var parent = (HudNodeDataHandle)stack.node[0].Item3;

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
								ZOffset = (ushort)layerData[2],
								Client = client
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

				public void UpdateNodeSizing(IReadOnlyList<TreeNodeData> nodes, IReadOnlyList<int> sizingActions)
				{
					foreach (int index in sizingActions)
					{
						HudNodeDataHandle handle = nodes[index].Node;
						bool needsUpdate = true;

						if (handle != null)
						{
							HudNodeStateData stateData = handle[0].Item1;
							HudElementStates state = (HudElementStates)stateData.Item1[0];
							HudElementStates visMask = (HudElementStates)stateData.Item2[0] | nodeReadyFlags;

							// Check visibility
							needsUpdate = (state & visMask) == visMask;
							needsUpdate &= ((state & nodeNotReadyFlags) == 0);
						}

						// Update sizing if needed
						if (needsUpdate)
						{
							try
							{
								Action SizingAction = nodes[index].Hooks.Item4;
								SizingAction?.Invoke();

								if (SizingAction != null)
									RichHudStats.UI.InternalCounters.SizingUpdates++;
							}
							catch (Exception e)
							{
								RichHudMaster.ModClient owner = nodes[index].Client?.ModClient;

								if (owner != null)
									owner.ReportException(e);
								else
									ExceptionHandler.ReportException(e);
							}
						}
					}
				}

				public void UpdateNodeLayout(IReadOnlyList<TreeNodeData> nodes, bool refresh = false)
				{
					foreach (TreeNodeData data in nodes)
					{
						HudNodeDataHandle handle = data.Node;
						HudNodeDataHandle parent = null;
						uint[] state = null;
						bool needsUpdate = true;

						// Try to get handle
						if (handle != null)
						{
							HudNodeStateData stateData = handle[0].Item1;
							parent = (HudNodeDataHandle)handle[0].Item3;
							state = stateData.Item1;

							var visMask = (HudElementStates)stateData.Item2[0];
							needsUpdate = (state[0] & (uint)visMask) == (uint)visMask;

							// If the parent is disjoint, it needs to correct itself before this
							// node can resume updating.
							if (parent != null)
							{
								var parentState = (HudElementStates)parent[0].Item1.Item1[0];
								needsUpdate &= ((parentState & HudElementStates.IsDisjoint) == 0);
							}

							state[0] &= ~(uint)HudElementStates.IsLayoutReady;
						}

						// Update layout if needed
						if (needsUpdate)
						{
							try
							{
								Action<bool> LayoutAction = data.Hooks.Item5;
								LayoutAction?.Invoke(refresh);

								if (LayoutAction != null)
									RichHudStats.UI.InternalCounters.LayoutUpdates++;
							}
							catch (Exception e)
							{
								RichHudMaster.ModClient owner = data.Client?.ModClient;

								if (owner != null)
									owner.ReportException(e);
								else
									ExceptionHandler.ReportException(e);
							}
						}

						// Update flags after layout
						if (handle != null)
						{
							if (needsUpdate)
								state[0] |= (uint)HudElementStates.IsLayoutReady;

							if (parent != null)
							{
								var parentState = (HudElementStates)parent[0].Item1.Item1[0];
								var parentVisMask = (HudElementStates)parent[0].Item1.Item2[0];
								var parentInputMask = (HudElementStates)parent[0].Item1.Item3[0];

								// Parent visibility flags need to propagate in top-down order, meaning they can only be evaluated
								// during Layout/Arrange, but Layout should not run without UpdateSize. They need to be delayed.
								if ((parentState & parentVisMask) == parentVisMask && (parentState & nodeNotReadyFlags) == 0)
									state[0] |= (uint)HudElementStates.WasParentVisible;
								else
									state[0] &= ~(uint)HudElementStates.WasParentVisible;

								if ((parentState & parentInputMask) == parentInputMask && (parentState & nodeNotReadyFlags) == 0)
									state[0] |= (uint)HudElementStates.WasParentInputEnabled;
								else
									state[0] &= ~(uint)HudElementStates.WasParentInputEnabled;
							}
						}
					}
				}

				public void DrawNodes(IReadOnlyList<TreeNodeData> nodes, IReadOnlyList<int> drawActions)
				{
					foreach (int index in drawActions)
					{
						HudNodeDataHandle handle = nodes[index].Node;
						bool needsUpdate = true;

						if (handle != null)
						{
							HudNodeStateData stateData = handle[0].Item1;
							HudElementStates state = (HudElementStates)stateData.Item1[0];
							HudElementStates visMask = (HudElementStates)stateData.Item2[0] | nodeReadyFlags;

							// Check visibility
							needsUpdate = (state & visMask) == visMask;
							// Set false if any not ready flags are set
							needsUpdate &= ((state & nodeNotReadyFlags) == 0);
						}

						// Draw if needed
						if (needsUpdate)
						{
							try
							{
								Action DrawAction = nodes[index].Hooks.Item6;
								DrawAction?.Invoke();

								if (DrawAction != null)
									RichHudStats.UI.InternalCounters.DrawUpdates++;
							}
							catch (Exception e)
							{
								RichHudMaster.ModClient owner = nodes[index].Client?.ModClient;

								if (owner != null)
									owner.ReportException(e);
								else
									ExceptionHandler.ReportException(e);
							}
						}
					}
				}

				public void UpdateNodeInputDepth(IReadOnlyList<TreeNodeData> nodes, IReadOnlyList<int> depthTestActions)
				{
					if (HudMain.InputMode == HudInputMode.NoInput)
						return;

					foreach (int index in depthTestActions)
					{
						HudNodeDataHandle handle = nodes[index].Node;
						bool needsUpdate = true;

						if (handle != null)
						{
							HudNodeStateData stateData = handle[0].Item1;
							uint[] state = stateData.Item1;
							HudElementStates
								visMask = (HudElementStates)stateData.Item2[0] | nodeReadyFlags,
								inputMask = (HudElementStates)stateData.Item3[0];
							bool
								canUseCursor = (state[0] & (uint)HudElementStates.CanUseCursor) > 0,
								isVisible = (state[0] & (uint)visMask) == (uint)visMask,
								isInputEnabled = (state[0] & (uint)inputMask) == (uint)inputMask;

							needsUpdate = isVisible && canUseCursor && isInputEnabled;
							// Set false if any not ready flags are set
							needsUpdate &= ((state[0] & (uint)nodeNotReadyFlags) == 0);

							// Reset mouse bounds check before every update
							state[0] &= ~(uint)HudElementStates.IsMouseInBounds;
						}

						// Perform depth testing if needed
						if (needsUpdate)
						{
							try
							{
								Action DepthTestAction = nodes[index].Hooks.Item2;
								DepthTestAction?.Invoke();

								if (DepthTestAction != null)
									RichHudStats.UI.InternalCounters.InputDepthUpdates++;
							}
							catch (Exception e)
							{
								RichHudMaster.ModClient owner = nodes[index].Client?.ModClient;

								if (owner != null)
									owner.ReportException(e);
								else
									ExceptionHandler.ReportException(e);
							}
						}
					}
				}

				public void UpdateNodeInput(IReadOnlyList<TreeNodeData> nodes, IReadOnlyList<int> inputActions)
				{
					foreach (int index in inputActions)
					{
						HudNodeDataHandle handle = nodes[index].Node;
						bool needsUpdate = true;

						if (handle != null)
						{
							HudNodeStateData stateData = handle[0].Item1;
							uint[] state = stateData.Item1;
							HudElementStates
								visMask = (HudElementStates)stateData.Item2[0] | nodeReadyFlags,
								inputMask = (HudElementStates)stateData.Item3[0];
							bool
								isVisible = (state[0] & (uint)visMask) == (uint)visMask,
								isInputEnabled = (state[0] & (uint)inputMask) == (uint)inputMask;

							needsUpdate = isVisible && isInputEnabled;
							// Set false if any not ready flags are set
							needsUpdate &= ((state[0] & (uint)nodeNotReadyFlags) == 0);

							// Reset mouse over state before every update
							state[0] &= ~(uint)HudElementStates.IsMousedOver;
						}

						// Update input if needed
						if (needsUpdate)
						{
							try
							{
								Action InputAction = nodes[index].Hooks.Item3;
								InputAction?.Invoke();

								if (InputAction != null)
									RichHudStats.UI.InternalCounters.InputUpdates++;
							}
							catch (Exception e)
							{
								RichHudMaster.ModClient owner = nodes[index].Client?.ModClient;

								if (owner != null)
									owner.ReportException(e);
								else
									ExceptionHandler.ReportException(e);
							}
						}
					}
				}
			}
		}
	}
}