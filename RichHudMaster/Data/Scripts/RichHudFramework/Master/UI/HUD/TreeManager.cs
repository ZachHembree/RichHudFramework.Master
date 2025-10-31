using RichHudFramework.UI.Rendering;
using System;
using System.Collections.Generic;
using VRageMath;

namespace RichHudFramework
{
	using RichHudFramework.Internal;
	using Server;

	namespace UI.Server
	{
		using static NodeConfigIndices;

		public sealed partial class HudMain
		{
			public sealed class TreeManager
			{
				/// <summary>
				/// Limits polling rate of legacy client UI trees. Higher values result in less
				/// frequent polling.
				/// </summary>
				public const uint LegacyTreeRefreshRate = 5;

				/// <summary>
				/// Inactive node thresholds for subtree pruning. Both thresholds must be met for pruning.
				/// </summary>
				private const uint SubtreeMinPruneThreshold = 10;
				private const float SubtreeInactivePrunePct = 20f;

				/// <summary>
				/// Sets the maximum frequency at which subtrees can be rebuilt for node pruning.
				/// Higher values result in less frequent polling.
				/// </summary>
				private const uint SubtreePruningRate = 10;

				/// <summary>
				/// Read-only list of registered tree clients
				/// </summary>
				public static IReadOnlyList<TreeClient> Clients => treeManager.clients;

				/// <summary>
				/// Tree client used by RHM
				/// </summary>
				public static TreeClient MainClient => treeManager.clients[0];

				/// <summary>
				/// Used to indicate whether older clients are requesting tree updates. 
				/// Deprecated.
				/// </summary>
				public static bool RefreshRequested;

				// Client data
				private readonly List<TreeClient> clients;
				// Cursor subtree
				private readonly List<FlatSubtree> lateUpdateBuffer;

				private readonly HudNodeIterator nodeIterator;
				private readonly List<FlatSubtree> activeSubtrees;
				private readonly ObjectPool<FlatSubtree> subtreePool;

				// Sorting buffers
				private readonly List<FlatSubtree> subtreeBuffer;
				private readonly Dictionary<Func<Vector3D>, ushort> distMap;
				private readonly HashSet<Func<Vector3D>> originFuncSet;
				private readonly List<Func<Vector3D>> originFunctions;
				private readonly List<ulong> indexBuffer;

				private int activeCount;
				private float lastResScale;

				private TreeManager()
				{
					HudMain.Init();

					if (treeManager == null)
						treeManager = this;
					else
						throw new Exception($"Only one instance of {nameof(TreeManager)} can exist at any given time.");

					distMap = new Dictionary<Func<Vector3D>, ushort>(50);
					originFuncSet = new HashSet<Func<Vector3D>>();
					originFunctions = new List<Func<Vector3D>>();
					indexBuffer = new List<ulong>(200);

					clients = new List<TreeClient>();
					var mainClient = new TreeClient() { RootNodeHandle = instance._root.DataHandle };

					lateUpdateBuffer = new List<FlatSubtree>();
					activeSubtrees = new List<FlatSubtree>();
					subtreeBuffer = new List<FlatSubtree>();

					subtreePool = new ObjectPool<FlatSubtree>(() => new FlatSubtree(), x => x.Clear());
					nodeIterator = new HudNodeIterator();
				}


				public static void Init()
				{
					if (treeManager == null)
						new TreeManager();
				}

				public static bool RegisterClient(TreeClient client)
				{
					Init();
					bool success = false;

					if (!treeManager.clients.Contains(client))
					{
						treeManager.clients.Add(client);
						success = true;
					}

					return success;
				}

				public static bool UnregisterClient(TreeClient client)
				{
					bool success = false;
					success = treeManager.clients.Remove(client);
					return success;
				}

				/// <summary>
				/// Updates UI tree and invokes sizing, layout and draw callbacks
				/// </summary>
				public void Draw()
				{
					UpdateTree();
					DrawUI();
				}

				private void UpdateTree()
				{
					RichHudStats.UI.Tree.BeginTick();

					// Update clients
					for (int n = 0; n < clients.Count; n++)
					{
						uint clientTick = HudMain.FrameNumber + (uint)(n % LegacyTreeRefreshRate);
						clients[n].Update(nodeIterator, subtreePool, clientTick);
					}

					// Manually append cursor update list
					nodeIterator.GetNodeData(instance._cursor.DataHandle, lateUpdateBuffer, subtreePool, clients[0]);

					// Prepare active subtrees
					SortSubtrees();

					// Check if pruning is required
					foreach (FlatSubtree subtree in activeSubtrees)
					{
						int total = Math.Max(1, subtree.ActiveCount + subtree.InactiveCount - subtree.InactiveTare);
						int effectiveInactive = Math.Max(0, subtree.InactiveCount - subtree.InactiveTare);
						float inactivePct = (100f * effectiveInactive) / total;

						if (effectiveInactive > SubtreeMinPruneThreshold && inactivePct > SubtreeInactivePrunePct)
						{
							// Failsafe tick limiter - if the threshold heuristic breaks it wont be a disaster
							if (subtree.PruneTick > SubtreePruningRate)
							{
								uint[] rootConfig = subtree.Owner.RootNodeHandle[0].Item1;
								rootConfig[StateID] |= (uint)HudElementStates.IsStructureStale;
								subtree.PruneTick = 0;
							}
							else
								subtree.PruneTick++;
						}
						else
							subtree.PruneTick = 0;
					}

					RichHudStats.UI.Tree.EndTick();
				}

				private void DrawUI()
				{
					BillBoardUtils.BeginDraw();
					RichHudStats.UI.InternalCounters.SubtreesRegistered = activeSubtrees.Count;

					float resScale = ResScale;

					clients[0].EnableCursor = EnableCursor;
					instance._cursor.DrawCursor = false;

					for (int n = 0; n < clients.Count; n++)
					{
						if (clients[n].EnableCursor)
							instance._cursor.DrawCursor = true;
					}

					RichHudStats.UI.Draw.BeginTick();

					// Invoke before draw callbacks on clients
					for (int n = 0; n < clients.Count; n++)
						clients[n].BeforeDrawCallback?.Invoke();

					foreach (FlatSubtree subtree in activeSubtrees)
						subtree.ResetCounters();

					// Sizing - vID 13+ only
					nodeIterator.UpdateNodeSizing(activeSubtrees);

					// Older clients (1.0.3-) node spaces require layout refreshes to function
					// Flag no longer used for vID 13+ (1.3+).
					bool rebuildLists = RefreshRequested && (HudMain.FrameNumber % LegacyTreeRefreshRate) == 0,
						refreshLayout = lastResScale != resScale || rebuildLists;

					// Arrange/layout
					nodeIterator.UpdateNodeLayout(activeSubtrees, refreshLayout);

					// Draw UI elements
					nodeIterator.DrawNodes(activeSubtrees);

					// Try to exclude stats render time
					RichHudStats.UI.Draw.PauseTick();
					RichHudDebug.UpdateDisplay();
					RichHudStats.UI.Draw.ResumeTick();

					// Invoke after draw callbacks on clients
					for (int n = 0; n < clients.Count; n++)
						clients[n].AfterDrawCallback?.Invoke();

					BillBoardUtils.FinishDraw();
					RichHudStats.UI.Draw.EndTick();

					if (rebuildLists)
						RefreshRequested = false;

					lastResScale = resScale;
				}

				/// <summary>
				/// Updates input for UI elements
				/// </summary>
				public void HandleInput()
				{
					RichHudStats.UI.Input.BeginTick();

					// Invoke before input callbacks on clients
					for (int n = 0; n < clients.Count; n++)
						clients[n].BeforeInputCallback?.Invoke();

					nodeIterator.UpdateNodeInputDepth(activeSubtrees);
					nodeIterator.UpdateNodeInput(activeSubtrees);

					// Invoke after input callbacks on clients
					for (int n = 0; n < clients.Count; n++)
						clients[n].AfterInputCallback?.Invoke();

					RichHudStats.UI.Input.EndTick();
				}

				private void SortSubtrees()
				{
					GatherSubtrees();
					UpdateOriginDistanceMap();

					// Build sorted active subtree list
					indexBuffer.Clear();

					// Create sorting keys using distance ranks
					for (int i = 0; i < subtreeBuffer.Count; i++)
					{
						ulong zOffset = subtreeBuffer[i].InnerZLayer,
							distance = distMap[subtreeBuffer[i].GetOriginFunc];

						indexBuffer.Add((distance << 48) | (zOffset << 32) | (uint)i);
					}

					if (indexBuffer.Capacity > indexBuffer.Count * 3 && indexBuffer.Count > 200)
						indexBuffer.TrimExcess();

					// Sort in ascending order
					indexBuffer.Sort();
					activeSubtrees.Clear();

					for (int i = 0; i < indexBuffer.Count; i++)
					{
						int index = (int)indexBuffer[i];
						activeSubtrees.Add(subtreeBuffer[index]);
					}

					RichHudStats.UI.InternalCounters.SubtreeSortingUpdates += activeSubtrees.Count;
					SortSubtreeMembers();
				}

				private void GatherSubtrees()
				{
					subtreeBuffer.Clear();

					int nodeCount = 0;

					for (int i = 0; i < clients.Count; i++)
					{
						if (!clients[i].IsPaused) // Exclude paused clients
							subtreeBuffer.AddRange(clients[i].Subtrees);
					}

					// Manually append last elements after all clients
					subtreeBuffer.AddRange(lateUpdateBuffer);

					activeCount = nodeCount;
				}

				private void UpdateOriginDistanceMap()
				{
					// Clear update lists and rebuild accessor lists from HUD tree
					originFuncSet.Clear();

					// Build distance func HashSet
					for (int i = 0; i < subtreeBuffer.Count; i++)
					{
						originFuncSet.Add(subtreeBuffer[i].GetOriginFunc);
						activeCount += subtreeBuffer[i].Inactive.OuterOffsets.Count;
					}

					RichHudStats.UI.InternalCounters.ElementsRegistered = activeCount;

					originFunctions.Clear();
					originFunctions.AddRange(originFuncSet);

					// Calculate origin distances and encode them in the index buffer as 32bit integers
					// for sorting into distance ranks.
					indexBuffer.Clear();
					Vector3D camPos = PixelToWorldRef[0].Translation;

					for (int i = 0; i < originFunctions.Count; i++)
					{
						Func<Vector3D> OriginFunc = originFunctions[i];

						if (OriginFunc == null)
							throw new Exception("HUD Node origin cannot be null.");

						Vector3D nodeOrigin = OriginFunc();
						double dist = (float)Math.Round(Vector3D.Distance(nodeOrigin, camPos), 6);
						ulong index = (ulong)i,
							// 32-bit converters unavailable
							distBits = (ulong)BitConverter.DoubleToInt64Bits(dist),
							// Extract 11-bit exponent and clamp to 8-bits
							exponent = (ulong)MathHelper.Clamp((int)(distBits >> 52) - 1023, -126, 127) + 127,
							// Extract and truncate 52-bit mantissa
							mantissa = (distBits & 0xFFFFFFFFFFFFF) >> (52 - 23);

						// Reconstitute and encode fp32 distance in sort buffer
						distBits = ((exponent << 23) | mantissa);
						indexBuffer.Add((distBits << 32) | index);
					}

					// Build distance map
					indexBuffer.Sort();
					distMap.Clear();

					int distID = ushort.MaxValue;
					uint lastDist = 0;

					for (int i = 0; i < indexBuffer.Count; i++)
					{
						ulong data = indexBuffer[i];
						int index = (int)data;
						uint dist = (uint)(data >> 32);

						if (dist > lastDist && distID > 0)
						{
							distID--;
							lastDist = dist;
						}

						distMap.Add(originFunctions[index], (ushort)distID);
					}

					RichHudStats.UI.InternalCounters.HudSpacesRegistered = distMap.Count;
				}

				/// <summary>
				/// Sorts members within the subtrees according to their outer ZOffset and update order
				/// </summary>
				private void SortSubtreeMembers()
				{
					foreach (FlatSubtree subtree in activeSubtrees)
					{
						if (subtree.IsActiveStale)
						{
							RichHudStats.UI.InternalCounters.ElementSortingUpdates += subtree.Inactive.OuterOffsets.Count;

							indexBuffer.Clear();
							IReadOnlyList<byte> outerOffsets = subtree.Inactive.OuterOffsets;

							for (int i = 0; i < outerOffsets.Count; i++)
							{
								ulong zOffset = outerOffsets[i];
								indexBuffer.Add((zOffset << 32) | (uint)i);
							}

							indexBuffer.Sort();
							subtree.Active.Clear();

							// Build sorted depth test list in forward, back to front order
							for (int i = 0; i < indexBuffer.Count; i++)
							{
								int index = (int)indexBuffer[i];
								Action depthTestAction = subtree.Inactive.HookData[index].Item2;

								if (depthTestAction != null)
								{
									subtree.Active.Hooks.InputDepthActions.Add(new NodeHook
									{
										Callback = depthTestAction,
										NodeID = index
									});
								}
							}

							// Build sorted input list in reverse, front to back order
							for (int i = indexBuffer.Count - 1; i >= 0; i--)
							{
								int index = (int)indexBuffer[i];
								Action inputAction = subtree.Inactive.HookData[index].Item3;

								if (inputAction != null)
								{
									subtree.Active.Hooks.InputActions.Add(new NodeHook
									{
										Callback = inputAction,
										NodeID = index
									});
								}
							}

							// Build sorted draw list in forward, back to front order
							for (int i = 0; i < indexBuffer.Count; i++)
							{
								int index = (int)indexBuffer[i];
								Action drawAction = subtree.Inactive.HookData[index].Item6;

								if (drawAction != null)
								{
									subtree.Active.Hooks.DrawActions.Add(new NodeHook
									{
										Callback = drawAction,
										NodeID = index
									});
								}
							}

							// Build sizing list (without sorting) in reverse, bottom-up order
							for (int i = indexBuffer.Count - 1; i >= 0; i--)
							{
								Action sizingAction = subtree.Inactive.HookData[i].Item4;

								if (sizingAction != null)
								{
									subtree.Active.Hooks.SizingActions.Add(new NodeHook
									{
										Callback = sizingAction,
										NodeID = i
									});
								}
							}

							// Build layout list (without sorting) in forward, top-down order
							for (int i = 0; i < indexBuffer.Count; i++)
							{
								Action<bool> layoutAction = subtree.Inactive.HookData[i].Item5;

								if (layoutAction != null)
								{
									subtree.Active.Hooks.LayoutActions.Add(new NodeHook<bool>
									{
										Callback = layoutAction,
										NodeID = i
									});
								}
							}

							subtree.Active.StateData.AddRange(subtree.Inactive.StateData);
							subtree.IsActiveStale = false;
						}
					}
				}
			}
		}
	}
}