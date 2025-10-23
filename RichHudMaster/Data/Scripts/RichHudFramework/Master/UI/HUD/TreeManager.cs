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
	using HudUpdateAccessors = MyTuple<
		ApiMemberAccessor,
		MyTuple<Func<ushort>, Func<Vector3D>>, // ZOffset + GetOrigin
		Action, // DepthTest
		Action, // HandleInput
		Action<bool>, // BeforeLayout
		Action // BeforeDraw
	>;
	namespace UI.Server
	{
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

				struct VersionedUpdateList
				{
					public IReadOnlyList<HudUpdateAccessors> accessors;
					public int vID;
				}

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

				private readonly List<VersionedUpdateList> activeUpdateLists;
				private List<HudUpdateAccessors> activeLateUpdateBuffer, inactiveLateUpdateBuffer;

				// Sorted client data
				private List<Action> depthTestActions, depthTestActionBuffer;
				private List<Action> inputActions, inputActionBuffer;
				private List<Action> drawActions, drawActionBuffer;
				private List<MyTuple<Action<bool>, int>> sizingActions, sizingActionBuffer;
				private List<MyTuple<Action<bool>, int>> layoutActions, layoutActionBuffer;
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

					activeUpdateLists = new List<VersionedUpdateList>();
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

					sizingActions = new List<MyTuple<Action<bool>, int>>(200);
					sizingActionBuffer = new List<MyTuple<Action<bool>, int>>(200);

					layoutActions = new List<MyTuple<Action<bool>, int>>(200);
					layoutActionBuffer = new List<MyTuple<Action<bool>, int>>(200);

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
						clients[n].Update(clientTick);
					}

					// Manually append cursor update list
					inactiveLateUpdateBuffer.Clear();
					instance._cursor.GetUpdateAccessors(inactiveLateUpdateBuffer, 0);

					if (sortTick == 0)
					{
						if (!isSynchronous)
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
							if (!isSynchronous)
								treeSwapLock.ReleaseExclusive();
						}
					}

					if (isSynchronous)
						UpdateAccessorListsImmediate();
					else
						BeginAccessorListsUpdate();
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

					// Sizing - vID 13+ only
					for (int n = sizingActions.Count - 1; n >= 0; n--)
						sizingActions[n].Item1(false);

					// Older clients (1.0.3-) node spaces require layout refreshes to function
					// Refresh no longer used in vID 13+ (1.3+). Flag repurposed for explicit
					// sizing/layout passes.
					bool rebuildLists = RefreshRequested && (drawTick % treeRefreshRate) == 0,
						refreshLayout = lastResScale != resScale || rebuildLists;

					// Arrange/layout
					for (int n = 0; n < layoutActions.Count; n++)
					{
						int vID = layoutActions[n].Item2;
						bool isArranging = vID >= (int)APIVersionTable.SizeAndArrangeSupport;
						// For client vID > 12, true indicates top-down arrange/layout pass
						layoutActions[n].Item1(refreshLayout || isArranging);
					}

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
						updateSwapLock.ReleaseShared();
						clientRegLock.ReleaseShared();
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

					RebuildUpdateLists(true);
					UpdateDistMap();

					UpdateIndexBuffer();
					ResetUpdateBuffers();

					BuildSortedUpdateLists(true);

					treeTimer.Stop();
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
				private void RebuildUpdateLists(bool isSynchronous = false)
				{
					// Clear update lists and rebuild accessor lists from HUD tree
					originFuncSet.Clear();
					activeUpdateLists.Clear();

					if (!isSynchronous)
						treeSwapLock.AcquireShared();

					try
					{
						for (int i = 0; i < clients.Count; i++)
						{
							activeUpdateLists.Add(new VersionedUpdateList {
								accessors = clients[i].UpdateAccessors, 
								vID = clients[i].ApiVersion
							});
						}

						// Manually append last elements after all clients
						activeUpdateLists.Add(new VersionedUpdateList {
							accessors = activeLateUpdateBuffer,
							vID = RichHudMaster.apiVID
						});

						// Build distance func HashSet
						activeCount = 0;

						for (int i = 0; i < activeUpdateLists.Count; i++)
						{
							for (int j = 0; j < activeUpdateLists[i].accessors.Count; j++)
							{
								originFuncSet.Add(activeUpdateLists[i].accessors[j].Item2.Item2);
								activeCount++;
							}
						}
					}
					finally
					{
						if (!isSynchronous)
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
						for (int j = 0; j < activeUpdateLists[i].accessors.Count; j++)
						{
							var accessors = activeUpdateLists[i].accessors[j];
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
					sizingActionBuffer.Clear();
					layoutActionBuffer.Clear();

					depthTestActionBuffer.EnsureCapacity(activeCount);
					inputActionBuffer.EnsureCapacity(activeCount);
					drawActionBuffer.EnsureCapacity(activeCount);
					sizingActionBuffer.EnsureCapacity(activeCount);
					layoutActionBuffer.EnsureCapacity(activeCount);
				}

				/// <summary>
				/// Builds sorted update accessor lists
				/// </summary>
				private void BuildSortedUpdateLists(bool isSynchronous = false)
				{
					// Build sorted depth test list
					for (int n = 0; n < indexBuffer.Count; n++)
					{
						uint index = (uint)indexBuffer[n];
						int i = (int)(index >> 16);
						int j = (int)(index & 0xFFFF);

						Action depthTestAction = activeUpdateLists[i].accessors[j].Item3;

						if (depthTestAction != null)
							depthTestActionBuffer.Add(depthTestAction);
					}

					// Build sorted input list
					for (int n = 0; n < indexBuffer.Count; n++)
					{
						uint index = (uint)indexBuffer[n];
						int i = (int)(index >> 16);
						int j = (int)(index & 0xFFFF);

						HudUpdateAccessors accessors = activeUpdateLists[i].accessors[j];
						Action inputAction = activeUpdateLists[i].accessors[j].Item4;

						if (inputAction != null)
							inputActionBuffer.Add(inputAction);
					}

					// Build sorted draw list
					for (int n = 0; n < indexBuffer.Count; n++)
					{
						uint index = (uint)indexBuffer[n];
						int i = (int)(index >> 16);
						int j = (int)(index & 0xFFFF);

						Action drawAction = activeUpdateLists[i].accessors[j].Item6;

						if (drawAction != null)
							drawActionBuffer.Add(drawAction);
					}

					// Build sizing list  (without sorting)
					for (int i = 0; i < activeUpdateLists.Count; i++)
					{
						int vID = activeUpdateLists[i].vID;

						if (vID >= (int)APIVersionTable.SizeAndArrangeSupport)
						{
							for (int j = 0; j < activeUpdateLists[i].accessors.Count; j++)
							{
								Action<bool> layoutAction = activeUpdateLists[i].accessors[j].Item5;

								if (layoutAction != null)
									sizingActionBuffer.Add(new MyTuple<Action<bool>, int>(layoutAction, vID));
							}
						}
					}

					// Build layout list (without sorting)
					for (int i = 0; i < activeUpdateLists.Count; i++)
					{
						int vID = activeUpdateLists[i].vID;

						for (int j = 0; j < activeUpdateLists[i].accessors.Count; j++)
						{
							Action<bool> layoutAction = activeUpdateLists[i].accessors[j].Item5;

							if (layoutAction != null)
								layoutActionBuffer.Add(new MyTuple<Action<bool>, int>(layoutAction, vID));
						}
					}

					// Make results visible
					if (!isSynchronous)
						updateSwapLock.AcquireExclusive();

					try
					{
						ElementRegistered = activeCount;
						MyUtils.Swap(ref depthTestActionBuffer, ref depthTestActions);
						MyUtils.Swap(ref inputActionBuffer, ref inputActions);
						MyUtils.Swap(ref drawActionBuffer, ref drawActions);
						MyUtils.Swap(ref sizingActionBuffer, ref sizingActions);
						MyUtils.Swap(ref layoutActionBuffer, ref layoutActions);
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