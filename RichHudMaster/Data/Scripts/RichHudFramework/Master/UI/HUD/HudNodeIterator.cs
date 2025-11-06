using ProtoBuf.Meta;
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
					nodeReadyFlags = HudElementStates.IsSpaceNodeReady,
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
				public int GetNodeData(HudNodeDataHandle srcRoot, List<FlatSubtree> dst,
					ObjectPool<FlatSubtree> bufferPool, TreeClient owner)
				{
					Clear();

					FlatSubtree subtree = null;
					int nodeCount = 0;

					// Subtree detection
					Func<Vector3D> lastGetOriginFunc = null;
					byte lastInnerOffset = 0;
					// Subtree position
					int subtreeCount = 0;
					int subtreePos = 0;
					// Set true if the contents of the subtree match the current node structure
					bool canBeEqual = false;

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

						var parent = (HudNodeDataHandle)stack.node[0].Item4;

						// Propagate HUD Space Node pos func
						if (parent != null && (state & HudElementStates.IsSpaceNode) == 0)
						{
							stack.node[0].Item2[0] = parent[0].Item2[0];
						}

						// Update combined ZOffset for layer sorting
						{
							byte outerOffset = (byte)((sbyte)config[ZOffsetID] - sbyte.MinValue);
							ushort innerOffset = (ushort)(config[ZOffsetInnerID] << 8);

							// Combine local node inner and outer offsets with parent and pack into
							// full ZOffset
							if (parent != null)
							{
								ushort parentFull = (ushort)parent[0].Item1[FullZOffsetID];
								byte parentOuter = (byte)((parentFull & 0x00FF) + sbyte.MinValue);
								ushort parentInner = (ushort)(parentFull & 0xFF00);

								outerOffset = (byte)Math.Min(outerOffset + parentOuter, byte.MaxValue);
								innerOffset = (ushort)Math.Min(innerOffset + parentInner, 0xFF00);
							}

							config[FullZOffsetID] = (ushort)(innerOffset | outerOffset);
						}

						// Check if a new subtree needs to be started
						{
							byte innerOffset = (byte)(config[FullZOffsetID] >> 8);
							Func<Vector3D> GetOriginFunc = stack.node[0].Item2[0];

							// Start new subtree on new layer or coordinate space
							if (innerOffset != lastInnerOffset || lastGetOriginFunc != GetOriginFunc)
							{
								// Finalize previous subtree
								if (subtree != null)
								{
									if (subtree.StateData.Count > subtreePos)
									{
										subtree.TruncateInactive(subtreePos);
										canBeEqual = false;
									}

									if (!canBeEqual)
										subtree.IsActiveStale = true;
								}

								// Use an existing buffer if available
								if (subtreeCount < dst.Count)
									subtree = dst[subtreeCount];
								else
								{
									subtree = bufferPool.Get();
									dst.Add(subtree);
								}

								// If the subtree was already using these values, it may be unchanged
								canBeEqual = (config == subtree.RootConfig && GetOriginFunc == subtree.GetOriginFunc);

								subtree.Owner = owner;
								subtree.RootConfig = config;
								subtree.GetOriginFunc = GetOriginFunc;
								subtree.InactiveTare = 0;

								lastInnerOffset = innerOffset;
								lastGetOriginFunc = GetOriginFunc;
								subtreePos = 0;
								subtreeCount++;
							}
						}

						var children = stack.node[0].Item5;
						int nodeID = subtree.StateData.Count;

						// Add node
						if (subtreePos < subtree.StateData.Count)
						{
							if (canBeEqual)
							{
								byte lastOuterOffset = subtree.Inactive.OuterOffsets[subtreePos];
								byte outerOffset = (byte)config[FullZOffsetID];

								// If the config reference matches, the node is the same
								canBeEqual &= subtree.StateData[subtreePos].Config == config;
								// If the outer/public sorting has changed, the members will need to be resorted
								canBeEqual &= outerOffset == lastOuterOffset;
							}

							// If the tree members are unchanged and don't require resorting, this is 
							// unnecessary.
							if (!canBeEqual)
							{
								// Confg/state
								subtree.StateData[subtreePos] = new NodeState
								{
									Config = config,
									ParentConfig = parent?[0].Item1
								};
								// Sorting info
								subtree.Inactive.OuterOffsets[subtreePos] = (byte)config[FullZOffsetID];
								// Callbacks
								subtree.Inactive.HookData[subtreePos] = stack.node[0].Item3;
							}
						}
						else
						{
							// If there were new additions, it can't be equal
							canBeEqual = false;

							// Confg/state
							subtree.StateData.Add(new NodeState
							{
								Config = config,
								ParentConfig = parent?[0].Item1
							});
							// Sorting info
							subtree.Inactive.OuterOffsets.Add((byte)config[FullZOffsetID]);
							// Callbacks
							subtree.Inactive.HookData.Add(stack.node[0].Item3);
						}

						subtreePos++;
						nodeCount++;

						// Check visibility
						if ((state & visMask) == visMask)
						{
							config[StateID] &= ~(uint)HudElementStates.IsInactiveLeaf;

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
						// Track inactive leaf nodes for visibility transitions
						else
							config[StateID] |= (uint)HudElementStates.IsInactiveLeaf;
					}

					// Finalize trailing subtree
					if (subtree != null)
					{
						if (subtree.StateData.Count > subtreePos)
						{
							subtree.TruncateInactive(subtreePos);
							canBeEqual = false;
						}

						if (!canBeEqual)
							subtree.IsActiveStale = true;
					}

					// Trim and return unused buffers
					if (subtreeCount < dst.Count)
					{
						int start = subtreeCount, length = dst.Count - start;
						bufferPool.ReturnRange(dst, start, length);
						dst.RemoveRange(start, length);
					}

					return nodeCount;
				}

				public void UpdateNodeSizing(List<FlatSubtree> subtrees)
				{
					for (int i = subtrees.Count - 1; i >= 0; i--)
					{
						FlatSubtree subtree = subtrees[i];
						var active = subtree.Active;

						foreach (NodeHook sizingUpdate in active.Hooks.SizingActions)
						{
							NodeState state = (subtree.StateData.Count != 0) ? subtree.StateData[sizingUpdate.NodeID] : default(NodeState);
							uint[] config = state.Config;
							bool needsUpdate = true;

							if (config != null)
							{
								var flags = (HudElementStates)config[StateID];
								var visMask = (HudElementStates)config[VisMaskID] | nodeReadyFlags;

								// Check visibility
								needsUpdate = (flags & visMask) == visMask;
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
									subtrees[i].Owner.ReportExceptionFunc(e);
									subtrees.RemoveAt(i); i++;
									break;
								}
							}
						}
					}
				}

				public void UpdateNodeLayout(List<FlatSubtree> subtrees, bool refresh = false)
				{
					for (int i = 0; i < subtrees.Count; i++)
					{
						FlatSubtree subtree = subtrees[i];
						var active = subtrees[i].Active;

						for (int j = 0; j < active.Hooks.LayoutActions.Count; j++)
						{
							NodeState state = (subtree.StateData.Count != 0) ? subtree.StateData[j] : default(NodeState);
							IReadOnlyList<uint> parentConfig = state.ParentConfig;
							uint[] config = state.Config;
							bool needsUpdate = true;

							if (config != null)
							{
								config[FrameNumberID] = (uint)HudMain.FrameNumber;
								needsUpdate = (config[StateID] & config[VisMaskID]) == config[VisMaskID];

								// If invisible nodes are encountered, they need to be pruned at some point
								if (!needsUpdate)
									subtree.InactiveCount++;
								else
									subtree.ActiveCount++;

								// Signal visibility change affecting structure and remove inactive flag
								if (needsUpdate && (config[StateID] & (uint)HudElementStates.IsInactiveLeaf) > 0)
								{
									uint[] rootConfig = subtree.Owner.RootNodeHandle[0].Item1;
									rootConfig[StateID] |= (uint)HudElementStates.IsStructureStale;
									config[StateID] &= ~(uint)HudElementStates.IsInactiveLeaf;
								}
							}

							// Update layout if needed
							if (needsUpdate)
							{
								try
								{
									Action<bool> LayoutUpdate = active.Hooks.LayoutActions[j].Callback;

									if (LayoutUpdate != null)
									{
										LayoutUpdate(refresh);
										RichHudStats.UI.InternalCounters.LayoutUpdates++;
									}
								}
								catch (Exception e)
								{
									subtree.Owner.ReportExceptionFunc(e);
									subtrees.RemoveAt(i); i--;
									break;
								}
							}

							// Propagate flags after layout
							if (config != null)
							{
								if (parentConfig != null)
								{
									var parentState = (HudElementStates)parentConfig[StateID];
									var parentVisMask = (HudElementStates)parentConfig[VisMaskID];
									var parentInputMask = (HudElementStates)parentConfig[InputMaskID];

									// Parent visibility flags need to propagate in top-down order, meaning they can only be evaluated
									// during Layout/Arrange, but Layout should not run without UpdateSize. They need to be delayed.
									if ((parentState & parentVisMask) == parentVisMask)
										config[StateID] |= (uint)HudElementStates.WasParentVisible;
									else
										config[StateID] &= ~(uint)HudElementStates.WasParentVisible;

									if ((parentState & parentInputMask) == parentInputMask)
										config[StateID] |= (uint)HudElementStates.WasParentInputEnabled;
									else
										config[StateID] &= ~(uint)HudElementStates.WasParentInputEnabled;
								}
							}
						}
					}
				}

				public void DrawNodes(IReadOnlyList<FlatSubtree> subtrees, List<int> indices)
				{
					for (int i = 0; i < indices.Count; i++)
					{
						int index = indices[i];
						var subtree = subtrees[index];
						var active = subtree.Active;

						foreach (NodeHook drawUpdate in active.Hooks.DrawActions)
						{
							NodeState state = (subtree.StateData.Count != 0) ? subtree.StateData[drawUpdate.NodeID] : default(NodeState);
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
									subtree.Owner.ReportExceptionFunc(e);
									indices.RemoveAt(i); i--;
									break;
								}
							}
						}
					}
				}

				public void UpdateNodeInputDepth(IReadOnlyList<FlatSubtree> subtrees, List<int> indices)
				{
					if (HudMain.InputMode == HudInputMode.NoInput)
						return;

					for (int i = 0; i < indices.Count; i++)
					{
						int index = indices[i];
						var subtree = subtrees[index];
						var active = subtree.Active;

						foreach (NodeHook depthTest in active.Hooks.InputDepthActions)
						{
							NodeState state = (subtree.StateData.Count != 0) ? subtree.StateData[depthTest.NodeID] : default(NodeState);
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
									subtree.Owner.ReportExceptionFunc(e);
									indices.RemoveAt(i); i--;
									break;
								}
							}
						}
					}
				}

				public void UpdateNodeInput(List<FlatSubtree> subtrees, List<int> indices)
				{
					for (int i = indices.Count - 1; i >= 0; i--)
					{
						int index = indices[i];
						var subtree = subtrees[index];
						var active = subtree.Active;

						foreach (NodeHook inputUpdate in active.Hooks.InputActions)
						{
							NodeState state = (subtree.StateData.Count != 0) ? subtree.StateData[inputUpdate.NodeID] : default(NodeState);
							uint[] config = state.Config;
							bool needsUpdate = true;

							if (config != null)
							{
								var visMask = (HudElementStates)config[VisMaskID] | nodeReadyFlags;
								bool isVisible = (config[StateID] & (uint)visMask) == (uint)visMask,
									isInputEnabled = (config[StateID] & config[InputMaskID]) == config[InputMaskID];

								needsUpdate = isVisible && isInputEnabled;
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
									subtrees[index].Owner.ReportExceptionFunc(e);
									indices.RemoveAt(i); i++;
									break;
								}
							}
						}
					}
				}
			}
		}
	}
}