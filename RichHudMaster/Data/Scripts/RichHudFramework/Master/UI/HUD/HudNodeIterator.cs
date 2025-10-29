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

namespace RichHudFramework
{
	using HudNodeData = MyTuple<
		uint[], // 1 - Config { 1.0 - State, 1.1 - NodeVisibleMask, 1.2 - NodeInputMask, 1.3 - zOffset, 1.4 - zOffsetInner, 1.5 - fullZOffset }
		Func<Vector3D>[],  // 2 - GetNodeOriginFunc
		HudNodeHookData, // 3 - Main hooks
		object, // 4 - Parent as HudNodeDataHandle
		List<object>, // 5 - Children as HudNodeDataHandles
		object // 6 - Unused
	>;

	namespace UI.Server
	{
		using static RichHudFramework.UI.NodeConfigIndices;

		// Read-only length-1 array containing raw UI node data
		using HudNodeDataHandle = IReadOnlyList<HudNodeData>;

		public sealed partial class HudMain
		{
			public class HudNodeIterator
			{
				private const HudElementStates
					nodeReadyFlags = HudElementStates.IsLayoutReady | HudElementStates.IsSpaceNodeReady,
					nodeNotReadyFlags = HudElementStates.IsDisjoint;

				private struct NodeStackData
				{
					/// <summary>
					/// Handle to UI node data
					/// </summary>
					public HudNodeDataHandle node;

					/// <summary>
					/// Recursive depth of the node in the tree
					/// </summary>
					public int depth;
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
				/// depth first order. Returns the number of nodes added.
				/// </summary>
				public int GetNodeData(HudNodeDataHandle srcRoot, List<FlatSubtree> subtreeList, 
					ObjectPool<FlatSubtree> bufferPool, int clientID = 0)
				{
					Clear();

					FlatSubtree subtree = null;
					byte lastInnerOffset = 0;
					Func<Vector3D> lastOriginFunc = null;
					int count = 0;

					// Push root onto the stack
					nodeStack.Add(new NodeStackData
					{
						node = srcRoot,
						depth = 0
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

							// Get subtree buffer
							{
								byte innerOffset = (byte)(config[FullZOffsetID] >> 8);
								Func<Vector3D> originFunc = stack.node[0].Item2[0];

								// Start new subtree on new layer or coordinate space
								if (innerOffset != lastInnerOffset || lastOriginFunc != originFunc)
								{
									subtree = bufferPool.Get();
									subtree.BaseZOffset = innerOffset;
									subtree.OriginFunc = originFunc;
									subtreeList.Add(subtree);

									lastInnerOffset = innerOffset;
									lastOriginFunc = originFunc;
								}
							}

							var children = stack.node[0].Item5;
							int nodeID = subtree.Inactive.StateData.Count;

							// Add node
							// Confg/state
							subtree.Inactive.StateData.Add(new NodeState
							{
								Config = config,
								ParentConfig = parent?[0].Item1,
								ClientID = clientID
							});
							// Sorting info
							subtree.Inactive.DepthData.Add(new NodeDepthData
							{
								GetPosFunc = stack.node[0].Item2[0],
								ZOffset = (ushort)config[FullZOffsetID]
							});
							// Callbacks
							subtree.Inactive.HookData.Add(stack.node[0].Item3);
							count++;

							// Push children
							for (int i = children.Count - 1; i >= 0; i--)
							{
								nodeStack.Add(new NodeStackData
								{
									node = (HudNodeDataHandle)children[i],
									depth = lastDepth + 1
								});
							}
						}
					}

					return count;
				}

				public void UpdateNodeSizing(IReadOnlyList<TreeClient> clients, IReadOnlyList<FlatSubtree> subtrees)
				{
					foreach (FlatSubtree subtree in subtrees) 
					{
						var active = subtree.Active;

						foreach (NodeHook sizingUpdate in active.Hooks.SizingActions)
						{
							uint[] config = active.StateData[sizingUpdate.NodeID].Config;
							bool needsUpdate = true;

							if (config != null)
							{
								var state = (HudElementStates)config[StateID];
								var visMask = (HudElementStates)config[VisMaskID] | nodeReadyFlags;

								// Check visibility
								needsUpdate = (state & visMask) == visMask;
								needsUpdate &= ((state & nodeNotReadyFlags) == 0);
							}

							// Update sizing if needed
							if (needsUpdate)
							{
								try
								{
									if (sizingUpdate.Callback != null)
									{
										sizingUpdate.Callback();
										RichHudStats.UI.InternalCounters.SizingUpdates++;
									}
								}
								catch (Exception e)
								{
									int clientID = active.StateData[sizingUpdate.NodeID].ClientID;
									clients[clientID].ReportExceptionFunc(e);
								}
							}
						}
					}
				}

				public void UpdateNodeLayout(IReadOnlyList<TreeClient> clients, IReadOnlyList<FlatSubtree> subtrees, bool refresh = false)
				{
					foreach (FlatSubtree subtree in subtrees)
					{
						var active = subtree.Active;

						for (int i = 0; i < active.StateData.Count; i++)
						{
							NodeState state = active.StateData[i];
							IReadOnlyList<uint> parentConfig = state.ParentConfig;
							uint[] config = state.Config;
							bool needsUpdate = true;

							if (config != null)
							{
								needsUpdate = (config[StateID] & config[VisMaskID]) == config[VisMaskID];

								// If the parent is disjoint, it needs to correct itself before this
								// node can resume updating.
								if (parentConfig != null)
								{
									var parentState = (HudElementStates)parentConfig[StateID];
									needsUpdate &= ((parentState & HudElementStates.IsDisjoint) == 0);
								}

								config[StateID] &= ~(uint)HudElementStates.IsLayoutReady;
							}

							// Update layout if needed
							if (needsUpdate)
							{
								try
								{
									Action<bool> LayoutUpdate = active.Hooks.LayoutActions[i].Callback;

									if (LayoutUpdate != null)
									{
										LayoutUpdate(refresh);
										RichHudStats.UI.InternalCounters.LayoutUpdates++;
									}
								}
								catch (Exception e)
								{
									int clientID = state.ClientID;
									clients[clientID].ReportExceptionFunc(e);
								}
							}

							// Propagate flags after layout
							if (config != null)
							{
								if (needsUpdate)
									config[StateID] |= (uint)HudElementStates.IsLayoutReady;

								if (parentConfig != null)
								{
									var parentState = (HudElementStates)parentConfig[StateID];
									var parentVisMask = (HudElementStates)parentConfig[VisMaskID];
									var parentInputMask = (HudElementStates)parentConfig[InputMaskID];

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
				}

				public void DrawNodes(IReadOnlyList<TreeClient> clients, IReadOnlyList<FlatSubtree> subtrees)
				{
					foreach (FlatSubtree subtree in subtrees)
					{
						var active = subtree.Active;

						foreach (NodeHook drawUpdate in active.Hooks.DrawActions)
						{
							NodeState state = active.StateData[drawUpdate.NodeID];
							uint[] config = state.Config;
							bool needsUpdate = true;

							if (config != null)
							{
								var visMask = config[VisMaskID] | (uint)nodeReadyFlags;
								// Check visibility
								needsUpdate = (config[StateID] & visMask) == visMask;
								// Set false if any not ready flags are set
								needsUpdate &= ((config[StateID] & (uint)nodeNotReadyFlags) == 0);
							}

							// Draw if needed
							if (needsUpdate)
							{
								try
								{
									if (drawUpdate.Callback != null)
									{
										drawUpdate.Callback();
										RichHudStats.UI.InternalCounters.DrawUpdates++;
									}
								}
								catch (Exception e)
								{
									int clientID = state.ClientID;
									clients[clientID].ReportExceptionFunc(e);
								}
							}
						}
					}	
				}

				public void UpdateNodeInputDepth(IReadOnlyList<TreeClient> clients, IReadOnlyList<FlatSubtree> subtrees)
				{
					if (HudMain.InputMode == HudInputMode.NoInput)
						return;

					foreach (FlatSubtree subtree in subtrees)
					{
						var active = subtree.Active;

						foreach (NodeHook depthTest in active.Hooks.InputDepthActions)
						{
							NodeState state = active.StateData[depthTest.NodeID];
							uint[] config = state.Config;
							bool needsUpdate = true;

							if (config != null)
							{
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
									if (depthTest.Callback != null)
									{
										depthTest.Callback();
										RichHudStats.UI.InternalCounters.InputDepthUpdates++;
									}
								}
								catch (Exception e)
								{
									int clientID = state.ClientID;
									clients[clientID].ReportExceptionFunc(e);
								}
							}
						}
					}
				}

				public void UpdateNodeInput(IReadOnlyList<TreeClient> clients, IReadOnlyList<FlatSubtree> subtrees)
				{
					foreach (FlatSubtree subtree in subtrees)
					{
						var active = subtree.Active;

						foreach (NodeHook inputUpdate in active.Hooks.InputActions)
						{
							NodeState state = active.StateData[inputUpdate.NodeID];
							uint[] config = state.Config;
							bool needsUpdate = true;

							if (config != null)
							{
								var visMask = (HudElementStates)config[VisMaskID] | nodeReadyFlags;
								bool isVisible = (config[StateID] & (uint)visMask) == (uint)visMask,
									isInputEnabled = (config[StateID] & config[InputMaskID]) == config[InputMaskID];

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
									if (inputUpdate.Callback != null)
									{
										inputUpdate.Callback();
										RichHudStats.UI.InternalCounters.InputUpdates++;
									}
								}
								catch (Exception e)
								{
									int clientID = state.ClientID;
									clients[clientID].ReportExceptionFunc(e);
								}
							}
						}
					}
				}
			}
		}
	}
}