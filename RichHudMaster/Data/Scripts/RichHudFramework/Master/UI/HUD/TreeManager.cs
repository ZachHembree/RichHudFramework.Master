using RichHudFramework.UI.Rendering;
using System;
using System.Collections.Generic;
using VRage;
using VRage.Utils;
using VRageMath;

namespace RichHudFramework
{
	using Internal;
	using Server;
	using System.Reflection;

	namespace UI.Server
	{
		public sealed partial class HudMain
		{
			public sealed class TreeManager
			{
				/// <summary>
				/// Read-only list of registered tree clients
				/// </summary>
				public static IReadOnlyList<TreeClient> Clients => treeManager.clients;

				/// <summary>
				/// Tree client used by RHM
				/// </summary>
				public static TreeClient MainClient => treeManager.mainClient;

				/// <summary>
				/// Used to indicate whether older clients are requesting tree updates. 
				/// Deprecated.
				/// </summary>
				public static bool RefreshRequested;

				// Active tree node data and next node set
				private SortedTreeData activeTree, inactiveTree;

				// Subtree search and data gathering
				private readonly HudNodeIterator nodeIterator;
				private readonly FlatTreeData nodeBuffer;

				// Client data
				private readonly List<TreeClient> clients;
				private readonly TreeClient mainClient;
				// Cursor subtree
				private readonly FlatTreeData lateUpdateBuffer;

				// Sorting buffers
				private readonly Dictionary<Func<Vector3D>, ushort> distMap;
				private readonly HashSet<Func<Vector3D>> originFuncSet;
				private readonly List<Func<Vector3D>> originFunctions;
				private readonly List<ulong> indexBuffer;

				// Sync
				private readonly FastResourceLock clientRegLock, updateSwapLock;
				private volatile bool isUpdatingTree, isImmediateUpdateReq;
				private volatile int sortTick, activeCount;

				private float lastResScale;

				private TreeManager()
				{
					HudMain.Init();

					if (treeManager == null)
						treeManager = this;
					else
						throw new Exception($"Only one instance of {nameof(TreeManager)} can exist at any given time.");

					clientRegLock = new FastResourceLock();
					updateSwapLock = new FastResourceLock();

					distMap = new Dictionary<Func<Vector3D>, ushort>(50);
					originFuncSet = new HashSet<Func<Vector3D>>();
					originFunctions = new List<Func<Vector3D>>();
					indexBuffer = new List<ulong>(200);

					clients = new List<TreeClient>();
					mainClient = new TreeClient() { RootNodeHandle = instance._root.DataHandle };
					lateUpdateBuffer = new FlatTreeData(20);

					nodeIterator = new HudNodeIterator();
					nodeBuffer = new FlatTreeData(200);

					activeTree = new SortedTreeData(200);
					inactiveTree = new SortedTreeData(200);
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

					treeManager.clientRegLock.AcquireExclusive();

					try
					{
						if (!treeManager.clients.Contains(client))
						{
							treeManager.clients.Add(client);
							success = true;
						}
					}
					catch (Exception e)
					{
						ExceptionHandler.ReportException(e);
					}
					finally
					{
						treeManager.clientRegLock.ReleaseExclusive();
					}

					return success;
				}

				public static bool UnregisterClient(TreeClient client)
				{
					bool success = false;

					if (treeManager != null)
					{
						treeManager.clientRegLock.AcquireExclusive();

						try
						{
							success = treeManager.clients.Remove(client);

							if (success)
								treeManager.isImmediateUpdateReq = true;
						}
						catch (Exception e)
						{
							ExceptionHandler.ReportException(e);
						}
						finally
						{
							treeManager.clientRegLock.ReleaseExclusive();
						}
					}

					return success;
				}

				/// <summary>
				/// Updates UI tree and invokes sizing, layout and draw callbacks
				/// </summary>
				public void Draw()
				{
					clientRegLock.AcquireShared();
					updateSwapLock.AcquireShared();

					try
					{
						// If the parallel tree update was scheduled especially late or ran
						// long, skip and recheck on the next frame. Sorting cannot run 
						// concurrently with client subtree searches.
						if (!isUpdatingTree)
							UpdateTree(isImmediateUpdateReq);

						if (!isImmediateUpdateReq)
							DrawUI();
					}
					catch (Exception e)
					{
						ExceptionHandler.ReportException(e);
					}
					finally
					{
						updateSwapLock.ReleaseShared();
						clientRegLock.ReleaseShared();
					}
				}

				/// <summary>
				/// Updates client subtree polling then starts the next tree rebuild step in 
				/// parallel.
				/// </summary>
				private void UpdateTree(bool isSynchronous)
				{
					int drawTick = instance.drawTick;

					// Spread out client tree updates
					for (int n = 0; n < clients.Count; n++)
					{
						int clientTick = drawTick + (n % treeRefreshRate);
						clients[n].Update(nodeIterator, clientTick, n);
					}

					if (sortTick == 0)
					{
						// Manually append cursor update list
						lateUpdateBuffer.Clear();
						nodeIterator.GetNodeData(instance._cursor.DataHandle, lateUpdateBuffer);
					}

					if (isSynchronous)
						UpdateAccessorListsImmediate();
					else
						BeginAccessorListsUpdate();
				}

				private void DrawUI()
				{
					BillBoardUtils.BeginDraw();
					RichHudStats.UI.InternalCounters.ElementsRegistered = activeTree.Hooks.LayoutActions.Count;

					float resScale = ResScale;
					int drawTick = instance.drawTick;

					mainClient.EnableCursor = EnableCursor;
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

					// Sizing - vID 13+ only
					nodeIterator.UpdateNodeSizing(clients, activeTree);

					// Older clients (1.0.3-) node spaces require layout refreshes to function
					// Flag no longer used for vID 13+ (1.3+).
					bool rebuildLists = RefreshRequested && (drawTick % treeRefreshRate) == 0,
						refreshLayout = lastResScale != resScale || rebuildLists;

					// Arrange/layout
					nodeIterator.UpdateNodeLayout(clients, activeTree, refreshLayout);

					// Draw UI elements
					nodeIterator.DrawNodes(clients, activeTree);

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
					if (isImmediateUpdateReq)
						return;

					clientRegLock.AcquireShared();
					updateSwapLock.AcquireShared();

					try
					{
						RichHudStats.UI.Input.BeginTick();

						// Invoke after draw callbacks on clients
						for (int n = 0; n < clients.Count; n++)
							clients[n].BeforeInputCallback?.Invoke();

						nodeIterator.UpdateNodeInputDepth(clients, activeTree);

						nodeIterator.UpdateNodeInput(clients, activeTree);

						// Invoke after draw callbacks on clients
						for (int n = 0; n < clients.Count; n++)
							clients[n].AfterInputCallback?.Invoke();

						RichHudStats.UI.Input.EndTick();
					}
					catch (Exception e)
					{
						ExceptionHandler.ReportException(e);
					}
					finally
					{
						updateSwapLock.ReleaseShared();
						clientRegLock.ReleaseShared();
					}
				}

				/// <summary>
				/// Dispatches parallel tree update divided over 3 ticks to run between frames, 
				/// semi-concurrent with draw, but not with client subtree searches.
				/// </summary>
				private void BeginAccessorListsUpdate()
				{
					if (sortTick == 0)
						instance.EnqueueAction(RichHudStats.UI.Tree.BeginTick);

					isUpdatingTree = true;

					instance.EnqueueTask(() =>
					{
						UpdateAccessorLists();
						sortTick++;
						sortTick %= 3;
					});
				}

				/// <summary>
				/// Performs full tree update synchronously
				/// </summary>
				private void UpdateAccessorListsImmediate()
				{
					RichHudStats.UI.Tree.BeginTick();
					isUpdatingTree = true;

					RebuildUpdateLists(true);
					UpdateDistances();

					UpdateIndexBuffer();
					ResetUpdateBuffers();

					BuildSortedUpdateLists(true);

					RichHudStats.UI.Tree.EndTick();
					isUpdatingTree = false;
					isImmediateUpdateReq = false;
				}

				/// <summary>
				/// Parallel tree list update
				/// </summary>
				private void UpdateAccessorLists()
				{
					clientRegLock.AcquireShared();

					try
					{
						if (sortTick % 3 == 0)
						{
							RebuildUpdateLists();
							instance.EnqueueAction(UpdateDistances);
						}

						if (sortTick % 3 == 1)
						{
							UpdateIndexBuffer();
							ResetUpdateBuffers();
						}

						if (sortTick % 3 == 2)
						{
							BuildSortedUpdateLists();
							instance.EnqueueAction(RichHudStats.UI.Tree.EndTick);
						}
					}
					finally
					{
						clientRegLock.ReleaseShared();
						isUpdatingTree = false;
					}
				}

				/// <summary>
				/// Rebuilds main update accessor list from UI tree as well as the layout accessors
				/// </summary>
				private void RebuildUpdateLists(bool isSynchronous = false)
				{
					// Clear update lists and rebuild accessor lists from HUD tree
					originFuncSet.Clear();
					nodeBuffer.Clear();

					int nodeCount = lateUpdateBuffer.DepthData.Count;

					for (int i = 0; i < clients.Count; i++)
						nodeCount += clients[i].InactiveNodeData.DepthData.Count;

					nodeBuffer.EnsureCapacity(nodeCount);

					for (int i = 0; i < clients.Count; i++)
						nodeBuffer.AddRange(clients[i].InactiveNodeData);

					// Manually append last elements after all clients
					nodeBuffer.AddRange(lateUpdateBuffer);
					activeCount = nodeCount;

					// Build distance func HashSet
					for (int i = 0; i < nodeBuffer.DepthData.Count; i++)
						originFuncSet.Add(nodeBuffer.DepthData[i].GetPosFunc);

					originFunctions.Clear();
					originFunctions.AddRange(originFuncSet);
				}

				/// <summary>
				/// Begins distance map rebuild
				/// </summary>
				private void UpdateDistances()
				{
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

					RichHudStats.UI.InternalCounters.HudSpacesRegistered = distMap.Count;
				}

				/// <summary>
				/// Builds sorted index list for sorted accessor lists
				/// </summary>
				private void UpdateIndexBuffer()
				{
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

					// Create sorting keys
					indexBuffer.Clear();
					indexBuffer.EnsureCapacity(activeCount);

					// Build index list and sort by zOffset
					for (int i = 0; i < nodeBuffer.DepthData.Count; i++)
					{
						NodeDepthData depthData = nodeBuffer.DepthData[i];
						ulong zOffset = depthData.ZOffset,
						distance = distMap[depthData.GetPosFunc];

						indexBuffer.Add((distance << 48) | (zOffset << 32) | (uint)i);
					}

					if (indexBuffer.Capacity > indexBuffer.Count * 3 && indexBuffer.Count > 200)
						indexBuffer.TrimExcess();

					// Sort in ascending order
					indexBuffer.Sort();
				}

				/// <summary>
				/// Clears depth, input and draw buffer lists and preallocates the lists as needed
				/// </summary>
				private void ResetUpdateBuffers()
				{
					inactiveTree.Clear();
					inactiveTree.EnsureCapacity(activeCount);
				}

				/// <summary>
				/// Builds sorted update accessor lists
				/// </summary>
				private void BuildSortedUpdateLists(bool isSynchronous = false)
				{
					// Build sorted depth test list in forward, back to front order
					for (int n = 0; n < indexBuffer.Count; n++)
					{
						int index = (int)indexBuffer[n];
						Action depthTestAction = nodeBuffer.HookData[index].Item2;

						if (depthTestAction != null)
						{
							inactiveTree.Hooks.InputDepthActions.Add(new NodeHook 
							{ 
								Callback = depthTestAction,
								NodeID = index
							});
						}
					}

					// Build sorted input list in reverse, front to back order
					for (int n = indexBuffer.Count - 1; n >= 0; n--)
					{
						int index = (int)indexBuffer[n];
						Action inputAction = nodeBuffer.HookData[index].Item3;

						if (inputAction != null)
						{
							inactiveTree.Hooks.InputActions.Add(new NodeHook 
							{
								Callback = inputAction,
								NodeID = index
							});
						}
					}

					// Build sorted draw list in forward, back to front order
					for (int n = 0; n < indexBuffer.Count; n++)
					{
						int index = (int)indexBuffer[n];
						Action drawAction = nodeBuffer.HookData[index].Item6;

						if (drawAction != null)
						{
							inactiveTree.Hooks.DrawActions.Add(new NodeHook 
							{
								Callback = drawAction,
								NodeID = index
							});
						}
					}

					// Build sizing list (without sorting) in reverse, bottom-up order
					for (int i = nodeBuffer.HookData.Count - 1; i >= 0; i--)
					{
						Action sizeAction = nodeBuffer.HookData[i].Item4;

						if (sizeAction != null)
						{
							inactiveTree.Hooks.SizingActions.Add(new NodeHook 
							{
								Callback = sizeAction,
								NodeID = i
							});
						}
					}

					// Build layout list (without sorting) in forward, top-down order
					for (int i = 0; i < nodeBuffer.HookData.Count; i++)
					{
						Action<bool> layoutAction = nodeBuffer.HookData[i].Item5;
						inactiveTree.Hooks.LayoutActions.Add(new NodeHook<bool>
						{
							Callback = layoutAction,
							NodeID = i
						});
					}

					MyUtils.Swap(ref inactiveTree.StateData, ref nodeBuffer.StateData);

					// Make results active
					if (!isSynchronous)
						updateSwapLock.AcquireExclusive();

					try
					{
						MyUtils.Swap(ref inactiveTree, ref activeTree);
					}
					finally
					{
						if (!isSynchronous)
							updateSwapLock.ReleaseExclusive();
					}
				}
			}
		}
	}
}