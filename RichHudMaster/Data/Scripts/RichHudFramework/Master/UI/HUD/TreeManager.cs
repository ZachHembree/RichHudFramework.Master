using RichHudFramework.UI.Rendering;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using VRage;
using VRage.Utils;
using VRageMath;
using ApiMemberAccessor = System.Func<object, int, object>;

namespace RichHudFramework
{
	using Internal;
	using Server;

	namespace UI.Server
	{
		using HudUpdateAccessors = MyTuple<
			ApiMemberAccessor,
			MyTuple<Func<ushort>, Func<Vector3D>>, // ZOffset + GetOrigin
			Action, // DepthTest
			Action, // HandleInput
			Action<bool>, // BeforeLayout
			Action // BeforeDraw
		>;

		public sealed partial class HudMain
		{
			public sealed class TreeManager
			{
				/// <summary>
				/// Number of unique HUD spaces registered
				/// </summary>
				public static int HudSpacesRegistered { get; private set; }

				/// <summary>
				/// Number of UI elements registered from all clients
				/// </summary>
				public static int ElementRegistered { get; private set; }

				/// <summary>
				/// Read-only list of registered tree clients
				/// </summary>
				public static IReadOnlyList<TreeClient> Clients => treeManager.clients;

				/// <summary>
				/// Tree client used by RHM
				/// </summary>
				public static TreeClient MainClient => treeManager.mainClient;

				/// <summary>
				/// Read-only list of the last 120 (tickResetInterval) draw update times. Updated as a circular array.
				/// Order not preserved.
				/// </summary>
				public static IReadOnlyList<long> DrawElapsedTicks => treeManager.drawTimes;

				/// <summary>
				/// Read-only list of the last 120 (tickResetInterval) input update times. Updated as a circular array.
				/// Order not preserved.
				/// </summary>
				public static IReadOnlyList<long> InputElapsedTicks => treeManager.inputTimes;

				/// <summary>
				/// Ticks elapsed during last rebuild
				/// </summary>
				public static IReadOnlyList<long> TreeElapsedTicks => treeManager.treeTimes;

				/// <summary>
				/// Used to indicate older clients requesting tree updates. Deprecated.
				/// </summary>
				public static bool RefreshRequested;

				// Sync
				private readonly FastResourceLock clientRegLock, updateSwapLock, treeSwapLock;
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

				private readonly List<IReadOnlyList<HudUpdateAccessors>> activeUpdateLists;
				private List<HudUpdateAccessors> activeLateUpdateBuffer, inactiveLateUpdateBuffer;

				// Sorted client data
				private List<Action> depthTestActions, depthTestActionBuffer;
				private List<Action> inputActions, inputActionBuffer;
				private List<Action> drawActions, drawActionBuffer;
				private List<Action<bool>> layoutActions, layoutActionBuffer;
				private float lastResScale;

				// Stats
				private readonly Stopwatch treeTimer, drawTimer, inputTimer;
				private readonly long[] drawTimes, inputTimes, treeTimes;

				private TreeManager()
				{
					HudMain.Init();

					if (treeManager == null)
						treeManager = this;
					else
						throw new Exception($"Only one instance of {nameof(TreeManager)} can exist at any given time.");

					clientRegLock = new FastResourceLock();
					updateSwapLock = new FastResourceLock();
					treeSwapLock = new FastResourceLock();

					activeUpdateLists = new List<IReadOnlyList<HudUpdateAccessors>>();
					activeLateUpdateBuffer = new List<HudUpdateAccessors>();
					inactiveLateUpdateBuffer = new List<HudUpdateAccessors>();

					distMap = new Dictionary<Func<Vector3D>, ushort>(50);
					originFuncSet = new HashSet<Func<Vector3D>>();
					originFunctions = new List<Func<Vector3D>>();
					indexBuffer = new List<ulong>(200);

					depthTestActions = new List<Action>(200);
					depthTestActionBuffer = new List<Action>(200);

					inputActions = new List<Action>(200);
					inputActionBuffer = new List<Action>(200);

					drawActions = new List<Action>(200);
					drawActionBuffer = new List<Action>(200);

					layoutActions = new List<Action<bool>>(200);
					layoutActionBuffer = new List<Action<bool>>(200);

					clients = new List<TreeClient>();
					mainClient = new TreeClient() { GetUpdateAccessors = instance._root.GetUpdateAccessors };

					drawTimer = new Stopwatch();
					drawTimes = new long[tickResetInterval];

					inputTimer = new Stopwatch();
					inputTimes = new long[tickResetInterval];

					treeTimer = new Stopwatch();
					treeTimes = new long[tickResetInterval];
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
							UpdateTree();

						if (!isImmediateUpdateReq)
							DrawUI();
					}
					catch (Exception e)
					{
						ExceptionHandler.ReportException(e);
					}
					finally
					{
						clientRegLock.ReleaseShared();
						updateSwapLock.ReleaseShared();
					}
				}

				private void UpdateTree()
				{
					int drawTick = instance.drawTick;

					// Spread out client tree updates
					for (int n = 0; n < clients.Count; n++)
					{
						int clientTick = drawTick + (n % treeRefreshRate);
						clients[n].Update(clientTick);
					}

					// Manually append cursor update list
					inactiveLateUpdateBuffer.Clear();
					instance._cursor.GetUpdateAccessors(inactiveLateUpdateBuffer, 0);

					if (sortTick == 0)
					{
						treeSwapLock.AcquireExclusive();

						try
						{
							for (int n = 0; n < clients.Count; n++)
								clients[n].FinishUpdate();

							MyUtils.Swap(ref inactiveLateUpdateBuffer, ref activeLateUpdateBuffer);
						}
						catch (Exception e)
						{
							ExceptionHandler.ReportException(e);
						}
						finally
						{
							treeSwapLock.ReleaseExclusive();
						}
					}

					if (isImmediateUpdateReq)
						UpdateAccessorListsImmediate();
					else
						BeginAccessorListsUpdate();

					isImmediateUpdateReq = false;
				}

				private void DrawUI()
				{
					BillBoardUtils.BeginDraw();
					float resScale = ResScale;
					int drawTick = instance.drawTick;

					mainClient.EnableCursor = EnableCursor;
					instance._cursor.DrawCursor = false;

					for (int n = 0; n < clients.Count; n++)
					{
						if (clients[n].EnableCursor)
							instance._cursor.DrawCursor = true;
					}

					treeTimes[drawTick] = treeTimer.ElapsedTicks;
					drawTimer.Restart();

					// Invoke before draw callbacks on clients
					for (int n = 0; n < clients.Count; n++)
						clients[n].BeforeDrawCallback?.Invoke();

					// Older clients (1.0.3-) node spaces require layout refreshes to function
					bool rebuildLists = RefreshRequested && (drawTick % treeRefreshRate) == 0,
						refreshLayout = lastResScale != resScale || rebuildLists;

					for (int n = 0; n < layoutActions.Count; n++)
						layoutActions[n](refreshLayout);

					// Draw UI elements
					for (int n = 0; n < drawActions.Count; n++)
						drawActions[n]();

					drawTimer.Stop();
					RichHudDebug.UpdateDisplay();
					drawTimer.Start();

					// Invoke after draw callbacks on clients
					for (int n = 0; n < clients.Count; n++)
						clients[n].AfterDrawCallback?.Invoke();

					BillBoardUtils.FinishDraw();

					drawTimer.Stop();
					drawTimes[drawTick] = drawTimer.ElapsedTicks;

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
						inputTimer.Restart();

						// Invoke after draw callbacks on clients
						for (int n = 0; n < clients.Count; n++)
							clients[n].BeforeInputCallback?.Invoke();

						for (int n = 0; n < depthTestActions.Count; n++)
							depthTestActions[n]();

						for (int n = inputActions.Count - 1; n >= 0; n--)
							inputActions[n]();

						// Invoke after draw callbacks on clients
						for (int n = 0; n < clients.Count; n++)
							clients[n].AfterInputCallback?.Invoke();

						inputTimer.Stop();
						inputTimes[instance.drawTick] = inputTimer.ElapsedTicks;
					}
					catch (Exception e)
					{
						ExceptionHandler.ReportException(e);
					}
					finally
					{
						clientRegLock.ReleaseShared();
						updateSwapLock.ReleaseShared();
					}
				}

				/// <summary>
				/// Dispatches parallel tree update divided over 3 ticks
				/// </summary>
				private void BeginAccessorListsUpdate()
				{
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
					treeTimer.Restart();
					isUpdatingTree = true;

					RebuildUpdateLists();
					UpdateDistMap();

					UpdateIndexBuffer();
					ResetUpdateBuffers();

					BuildSortedUpdateLists();

					treeTimer.Stop();
					isUpdatingTree = false;
				}

				/// <summary>
				/// Parallel tree list update
				/// </summary>
				private void UpdateAccessorLists()
				{
					clientRegLock.AcquireShared();

					try
					{
						treeTimer.Restart();

						if (sortTick % 3 == 0)
						{
							RebuildUpdateLists();
							instance.EnqueueAction(UpdateDistMap);
						}

						if (sortTick % 3 == 1)
						{
							UpdateIndexBuffer();
							ResetUpdateBuffers();
						}

						if (sortTick % 3 == 2)
						{
							BuildSortedUpdateLists();
							treeTimer.Stop();
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
				private void RebuildUpdateLists()
				{
					// Clear update lists and rebuild accessor lists from HUD tree
					originFuncSet.Clear();
					activeUpdateLists.Clear();
					treeSwapLock.AcquireShared();

					try
					{
						for (int i = 0; i < clients.Count; i++)
							activeUpdateLists.Add(clients[i].UpdateAccessors);

						// Manually append last elements after all clients
						activeUpdateLists.Add(activeLateUpdateBuffer);

						// Build distance func HashSet
						activeCount = 0;

						for (int i = 0; i < activeUpdateLists.Count; i++)
						{
							for (int j = 0; j < activeUpdateLists[i].Count; j++)
							{
								originFuncSet.Add(activeUpdateLists[i][j].Item2.Item2);
								activeCount++;
							}
						}
					}
					finally
					{
						treeSwapLock.ReleaseShared();
					}

					originFunctions.Clear();
					originFunctions.AddRange(originFuncSet);
				}

				/// <summary>
				/// Builds distance map for HUD Space Nodes
				/// </summary>
				private void UpdateDistMap()
				{
					indexBuffer.Clear();
					Vector3D camPos = PixelToWorldRef[0].Translation;

					for (int i = 0; i < originFunctions.Count; i++)
					{
						Func<Vector3D> OriginFunc = originFunctions[i];
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

					HudSpacesRegistered = distMap.Count;
				}

				/// <summary>
				/// Builds sorted index list for sorted accessor lists
				/// </summary>
				private void UpdateIndexBuffer()
				{
					indexBuffer.Clear();
					indexBuffer.EnsureCapacity(activeCount);

					// Build index list and sort by zOffset
					for (int i = 0; i < activeUpdateLists.Count; i++)
					{
						for (int j = 0; j < activeUpdateLists[i].Count; j++)
						{
							var accessors = activeUpdateLists[i][j];
							ulong index = ((uint)i << 16 | (ushort)j),
							zOffset = accessors.Item2.Item1(),
							distance = distMap[accessors.Item2.Item2];

							indexBuffer.Add((distance << 48) | (zOffset << 32) | index);
						}
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
					depthTestActionBuffer.Clear();
					inputActionBuffer.Clear();
					drawActionBuffer.Clear();
					layoutActionBuffer.Clear();

					depthTestActionBuffer.EnsureCapacity(activeCount);
					inputActionBuffer.EnsureCapacity(activeCount);
					drawActionBuffer.EnsureCapacity(activeCount);
					layoutActionBuffer.EnsureCapacity(activeCount);
				}

				/// <summary>
				/// Builds sorted update accessor lists
				/// </summary>
				private void BuildSortedUpdateLists()
				{
					// Build sorted depth test list
					for (int n = 0; n < indexBuffer.Count; n++)
					{
						uint index = (uint)indexBuffer[n];
						int i = (int)(index >> 16);
						int j = (int)(index & 0xFFFF);

						HudUpdateAccessors accessors = activeUpdateLists[i][j];
						depthTestActionBuffer.Add(accessors.Item3);
					}

					// Build sorted input list
					for (int n = 0; n < indexBuffer.Count; n++)
					{
						uint index = (uint)indexBuffer[n];
						int i = (int)(index >> 16);
						int j = (int)(index & 0xFFFF);

						HudUpdateAccessors accessors = activeUpdateLists[i][j];
						inputActionBuffer.Add(accessors.Item4);
					}

					// Build sorted draw list
					for (int n = 0; n < indexBuffer.Count; n++)
					{
						uint index = (uint)indexBuffer[n];
						int i = (int)(index >> 16);
						int j = (int)(index & 0xFFFF);

						HudUpdateAccessors accessors = activeUpdateLists[i][j];
						drawActionBuffer.Add(accessors.Item6);
					}

					// Build layout list (without sorting)
					for (int i = 0; i < activeUpdateLists.Count; i++)
					{
						for (int j = 0; j < activeUpdateLists[i].Count; j++)
						{
							var accessors = activeUpdateLists[i][j];
							layoutActionBuffer.Add(accessors.Item5);
						}
					}

					if (depthTestActionBuffer.Capacity > depthTestActionBuffer.Count * 3 && depthTestActionBuffer.Count > 200)
					{
						depthTestActionBuffer.TrimExcess();
						inputActionBuffer.TrimExcess();
						drawActionBuffer.TrimExcess();
						layoutActionBuffer.TrimExcess();
					}

					// Make results visible
					updateSwapLock.AcquireExclusive();

					try
					{
						ElementRegistered = activeCount;
						MyUtils.Swap(ref depthTestActionBuffer, ref depthTestActions);
						MyUtils.Swap(ref inputActionBuffer, ref inputActions);
						MyUtils.Swap(ref drawActionBuffer, ref drawActions);
						MyUtils.Swap(ref layoutActionBuffer, ref layoutActions);
					}
					finally
					{
						updateSwapLock.ReleaseExclusive();
					}
				}
			}
		}
	}
}