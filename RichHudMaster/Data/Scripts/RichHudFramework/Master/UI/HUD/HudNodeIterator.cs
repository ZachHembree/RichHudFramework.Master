using RichHudFramework.Internal;
using RichHudFramework.Server;
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
using HudSpaceOriginFunc = System.Func<VRageMath.Vector3D>;

namespace RichHudFramework
{
	using HudNodeData = MyTuple<
		uint[], // 1 - Config { 1.0 - State, 1.1 - NodeVisibleMask, 1.2 - NodeInputMask, 1.3 - zOffset, 1.4 - zOffsetInner, 1.5 - fullZOffset }
		Func<Vector3D>[],  // 2 - GetNodeOriginFunc
		HudNodeHookData, // 3 - Main hooks
		object, // 4 - Parent as HudNodeDataHandle
		List<object>, // 5 - Children as IReadOnlyList<HudNodeDataHandle>
		object // 6 - Unused
	>;

	namespace UI.Server
	{
		using static RichHudFramework.UI.NodeConfigIndices;

		// Read-only length-1 array containing raw UI node data
		using HudNodeDataHandle = IReadOnlyList<HudNodeData>;

		public sealed partial class HudMain
		{
			/// <summary>
			/// Enables or disables automatic UI preloading
			/// </summary>
			public static bool DefaultPreload = false;
			public static int MaxPreloadDepth = 3;

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

						uint[] config = stack.node[0].Item1;
						var state = (HudElementStates)config[StateID];
						var visMask = (HudElementStates)config[VisMaskID];
						state |= HudElementStates.WasParentVisible;

						bool canPreload = DefaultPreload || (state & HudElementStates.CanPreload) > 0;

						// Track depth and preloading
						if ((state & HudElementStates.IsVisible) == 0 && canPreload)
							stack.preloadDepth++;

						if (stack.preloadDepth <= MaxPreloadDepth && canPreload)
							state |= HudElementStates.IsVisible;

						// Check visibility
						if ((state & visMask) == visMask)
						{
							var parent = (HudNodeDataHandle)stack.node[0].Item4;

							// Propagate HUD Space Node pos func
							if (parent != null && (state & HudElementStates.IsSpaceNode) == 0)
							{
								stack.node[0].Item2[0] = parent[0].Item2[0];
							}

							// Update combined ZOffset for layer sorting
							{
								byte outerOffset = (byte)(config[ZOffsetID] - sbyte.MinValue);
								ushort innerOffset = (ushort)(config[ZOffsetInnerID] << 8);

								if (parent != null)
								{
									ushort parentFull = (ushort)parent[0].Item1[FullZOffsetID];
									outerOffset += (byte)((parentFull & 0x00FF) + sbyte.MinValue);
									innerOffset += (ushort)(parentFull & 0xFF00);
								}

								config[FullZOffsetID] = (ushort)(innerOffset | outerOffset);
							}

							// Add node
							dst.Add(new TreeNodeData
							{
								Node = stack.node,
								Hooks = stack.node[0].Item3,
								GetPosFunc = stack.node[0].Item2[0],
								ZOffset = (ushort)config[FullZOffsetID],
								Client = client
							});

							// Push children
							var children = stack.node[0].Item5;

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
							HudElementStates state = (HudElementStates)handle[0].Item1[StateID];
							HudElementStates visMask = (HudElementStates)handle[0].Item1[VisMaskID] | nodeReadyFlags;

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
						uint[] config = null;
						bool needsUpdate = true;

						// Try to get handle
						if (handle != null)
						{
							config = handle[0].Item1;
							parent = (HudNodeDataHandle)handle[0].Item4;
							needsUpdate = (config[StateID] & config[VisMaskID]) == config[VisMaskID];

							// If the parent is disjoint, it needs to correct itself before this
							// node can resume updating.
							if (parent != null)
							{
								var parentState = (HudElementStates)parent[0].Item1[StateID];
								needsUpdate &= ((parentState & HudElementStates.IsDisjoint) == 0);
							}

							config[StateID] &= ~(uint)HudElementStates.IsLayoutReady;
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

						// Propagate flags after layout
						if (handle != null)
						{
							if (needsUpdate)
								config[StateID] |= (uint)HudElementStates.IsLayoutReady;

							if (parent != null)
							{
								var parentState = (HudElementStates)parent[0].Item1[StateID];
								var parentVisMask = (HudElementStates)parent[0].Item1[VisMaskID];
								var parentInputMask = (HudElementStates)parent[0].Item1[InputMaskID];

								// Parent visibility flags need to propagate in top-down order, meaning they can only be evaluated
								// during Layout/Arrange, but Layout should not run without UpdateSize. They need to be delayed.
								if ((parentState & parentVisMask) == parentVisMask && (parentState & nodeNotReadyFlags) == 0)
									config[StateID] |= (uint)HudElementStates.WasParentVisible;
								else
									config[StateID] &= ~(uint)HudElementStates.WasParentVisible;

								if ((parentState & parentInputMask) == parentInputMask && (parentState & nodeNotReadyFlags) == 0)
									config[StateID] |= (uint)HudElementStates.WasParentInputEnabled;
								else
									config[StateID] &= ~(uint)HudElementStates.WasParentInputEnabled;
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
							HudElementStates state = (HudElementStates)handle[0].Item1[StateID];
							HudElementStates visMask = (HudElementStates)handle[0].Item1[VisMaskID] | nodeReadyFlags;

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
							uint[] config = handle[0].Item1;
							HudElementStates
								visMask = (HudElementStates)config[VisMaskID] | nodeReadyFlags,
								inputMask = (HudElementStates)config[InputMaskID];
							bool
								canUseCursor = (config[StateID] & (uint)HudElementStates.CanUseCursor) > 0,
								isVisible = (config[StateID] & (uint)visMask) == (uint)visMask,
								isInputEnabled = (config[StateID] & (uint)inputMask) == (uint)inputMask;

							needsUpdate = isVisible && canUseCursor && isInputEnabled;
							// Set false if any not ready flags are set
							needsUpdate &= ((config[StateID] & (uint)nodeNotReadyFlags) == 0);

							// Reset mouse bounds check before every update
							config[StateID] &= ~(uint)HudElementStates.IsMouseInBounds;
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
							uint[] config = handle[0].Item1;
							HudElementStates
								visMask = (HudElementStates)config[VisMaskID] | nodeReadyFlags,
								inputMask = (HudElementStates)config[InputMaskID];
							bool
								isVisible = (config[StateID] & (uint)visMask) == (uint)visMask,
								isInputEnabled = (config[StateID] & (uint)inputMask) == (uint)inputMask;

							needsUpdate = isVisible && isInputEnabled;
							// Set false if any not ready flags are set
							needsUpdate &= ((config[StateID] & (uint)nodeNotReadyFlags) == 0);

							// Reset mouse over state before every update
							config[StateID] &= ~(uint)HudElementStates.IsMousedOver;
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