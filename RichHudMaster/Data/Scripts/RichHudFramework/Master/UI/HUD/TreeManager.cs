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

	namespace UI.Server
	{
		// Read-only length-1 array containing raw UI node data
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
				/// Used to indicate older clients requesting tree updates. Deprecated.
				/// </summary>
				public static bool RefreshRequested;

				// Sync
				private readonly FastResourceLock clientRegLock, updateSwapLock;
				private volatile bool isUpdatingTree, isImmediateUpdateReq;
				private volatile int sortTick, activeCount;

				// Client data
				private readonly List<TreeClient> clients;
				private readonly TreeClient mainClient;

				// Sorting buffers
				private readonly Dictionary<Func<Vector3D>, ushort> distMap;
				private readonly HashSet<Func<Vector3D>> originFuncSet;
				private readonly List<Func<Vector3D>> originFunctions;
				private readonly List<ulong> indexBuffer;

				private readonly HudNodeIterator nodeIterator;
				private List<TreeNodeData> activeLateUpdateBuffer, inactiveLateUpdateBuffer;

				// Sorted client data
				private List<TreeNodeData> activeNodeData, inactiveNodeData;
				private List<int> activeDepthTests, inactiveDepthTests;
				private List<int> activeInputActions, inactiveInputActions;
				private List<int> activeDrawActions, inactiveDrawActions;
				private List<int> activeSizingActions, inactiveSizingActions;
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

					nodeIterator = new HudNodeIterator();

					activeNodeData = new List<TreeNodeData>(200);
					inactiveNodeData = new List<TreeNodeData>(200);

					inactiveLateUpdateBuffer = new List<TreeNodeData>();
					activeLateUpdateBuffer = new List<TreeNodeData>();

					distMap = new Dictionary<Func<Vector3D>, ushort>(50);
					originFuncSet = new HashSet<Func<Vector3D>>();
					originFunctions = new List<Func<Vector3D>>();
					indexBuffer = new List<ulong>(200);

					activeDepthTests = new List<int>(200);
					inactiveDepthTests = new List<int>(200);

					activeInputActions = new List<int>(200);
					inactiveInputActions = new List<int>(200);

					activeDrawActions = new List<int>(200);
					inactiveDrawActions = new List<int>(200);

					activeSizingActions = new List<int>(200);
					inactiveSizingActions = new List<int>(200);

					clients = new List<TreeClient>();
					mainClient = new TreeClient() { RootNodeHandle = instance._root.DataHandle };
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
				/// Updates UI tree, updates layout and draws
				/// </summary>
				public void Draw()
				{
					clientRegLock.AcquireShared();
					updateSwapLock.AcquireShared();

					try
					{
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

				private void UpdateTree(bool isSynchronous)
				{
					int drawTick = instance.drawTick;

					// Spread out client tree updates
					for (int n = 0; n < clients.Count; n++)
					{
						int clientTick = drawTick + (n % treeRefreshRate);
						clients[n].Update(nodeIterator, clientTick);
					}

					// Manually append cursor update list
					activeLateUpdateBuffer.Clear();
					nodeIterator.GetNodeData(instance._cursor.DataHandle, activeLateUpdateBuffer);

					if (sortTick == 0)
					{
						for (int n = 0; n < clients.Count; n++)
							clients[n].FinishUpdate();

						MyUtils.Swap(ref activeLateUpdateBuffer, ref inactiveLateUpdateBuffer);
					}

					if (isSynchronous)
						UpdateAccessorListsImmediate();
					else
						BeginAccessorListsUpdate();
				}

				private void DrawUI()
				{
					BillBoardUtils.BeginDraw();
					RichHudStats.UI.InternalCounters.ElementsRegistered = activeNodeData.Count;

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
					nodeIterator.UpdateNodeSizing(activeNodeData, activeSizingActions);

					// Older clients (1.0.3-) node spaces require layout refreshes to function
					// Flag no longer used for vID 13+ (1.3+).
					bool rebuildLists = RefreshRequested && (drawTick % treeRefreshRate) == 0,
						refreshLayout = lastResScale != resScale || rebuildLists;

					// Arrange/layout
					nodeIterator.UpdateNodeLayout(activeNodeData, refreshLayout);

					// Draw UI elements
					nodeIterator.DrawNodes(activeNodeData, activeDrawActions);

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

						nodeIterator.UpdateNodeInputDepth(activeNodeData, activeDepthTests);

						nodeIterator.UpdateNodeInput(activeNodeData, activeInputActions);

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
				/// Dispatches parallel tree update divided over 3 ticks
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
					inactiveNodeData.Clear();

					int nodeCount = inactiveLateUpdateBuffer.Count;

					for (int i = 0; i < clients.Count; i++)
						nodeCount += clients[i].InactiveNodeData.Count;

					inactiveNodeData.EnsureCapacity(nodeCount);

					for (int i = 0; i < clients.Count; i++)
						inactiveNodeData.AddRange(clients[i].InactiveNodeData);

					// Manually append last elements after all clients
					inactiveNodeData.AddRange(inactiveLateUpdateBuffer);
					activeCount = nodeCount;

					// Build distance func HashSet
					for (int i = 0; i < inactiveNodeData.Count; i++)
						originFuncSet.Add(inactiveNodeData[i].GetPosFunc);

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
					for (int i = 0; i < inactiveNodeData.Count; i++)
					{
						var accessors = inactiveNodeData[i];
						ulong zOffset = accessors.ZOffset,
						distance = distMap[accessors.GetPosFunc];

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
					inactiveDepthTests.Clear();
					inactiveInputActions.Clear();
					inactiveDrawActions.Clear();
					inactiveSizingActions.Clear();

					inactiveDepthTests.EnsureCapacity(activeCount);
					inactiveInputActions.EnsureCapacity(activeCount);
					inactiveDrawActions.EnsureCapacity(activeCount);
					inactiveSizingActions.EnsureCapacity(activeCount);
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
						Action depthTestAction = inactiveNodeData[index].Hooks.Item2;

						if (depthTestAction != null)
							inactiveDepthTests.Add(index);
					}

					// Build sorted input list in reverse, front to back order
					for (int n = indexBuffer.Count - 1; n >= 0; n--)
					{
						int index = (int)indexBuffer[n];
						TreeNodeData accessors = inactiveNodeData[index];
						Action inputAction = inactiveNodeData[index].Hooks.Item3;

						if (inputAction != null)
							inactiveInputActions.Add(index);
					}

					// Build sorted draw list in forward, back to front order
					for (int n = 0; n < indexBuffer.Count; n++)
					{
						int index = (int)indexBuffer[n];
						Action drawAction = inactiveNodeData[index].Hooks.Item6;

						if (drawAction != null)
							inactiveDrawActions.Add(index);
					}

					// Build sizing list (without sorting) in reverse, bottom-up order
					for (int i = inactiveNodeData.Count - 1; i >= 0; i--)
					{
						Action sizeAction = inactiveNodeData[i].Hooks.Item4;

						if (sizeAction != null)
							inactiveSizingActions.Add(i);
					}

					// Make results active
					if (!isSynchronous)
						updateSwapLock.AcquireExclusive();

					try
					{
						MyUtils.Swap(ref inactiveNodeData, ref activeNodeData);
						MyUtils.Swap(ref inactiveDepthTests, ref activeDepthTests);
						MyUtils.Swap(ref inactiveInputActions, ref activeInputActions);
						MyUtils.Swap(ref inactiveDrawActions, ref activeDrawActions);
						MyUtils.Swap(ref inactiveSizingActions, ref activeSizingActions);
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