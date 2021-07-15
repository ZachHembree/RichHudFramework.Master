using Sandbox.ModAPI;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using VRage;
using VRageMath;
using VRage.Utils;
using ApiMemberAccessor = System.Func<object, int, object>;

namespace RichHudFramework
{
    using Internal;
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

                public static bool RefreshRequested;

                public bool UpdatingTree { get; private set; }

                private readonly List<HudUpdateAccessors> updateAccessors;
                private readonly Dictionary<Func<Vector3D>, ushort> distMap;
                private readonly HashSet<Func<Vector3D>> uniqueOriginFuncs;
                private readonly List<ulong> indexBuffer;

                private List<Action> depthTestActions, depthTestActionBuffer;
                private List<Action> inputActions, inputActionBuffer;
                private List<Action> drawActions, drawActionBuffer;
                private List<Action<bool>> layoutActions;
                private float lastResScale;

                private readonly List<TreeClient> clients;
                private readonly TreeClient mainClient;

                private readonly Stopwatch treeTimer, drawTimer, inputTimer;
                private readonly long[] drawTimes, inputTimes, treeTimes;

                private TreeManager()
                {
                    HudMain.Init();

                    if (treeManager == null)
                        treeManager = this;
                    else
                        throw new Exception($"Only one instance of {GetType().Name} can exist at any given time.");

                    updateAccessors = new List<HudUpdateAccessors>(200);
                    distMap = new Dictionary<Func<Vector3D>, ushort>(50);
                    uniqueOriginFuncs = new HashSet<Func<Vector3D>>();
                    indexBuffer = new List<ulong>(200);

                    depthTestActions = new List<Action>(200);
                    depthTestActionBuffer = new List<Action>(200);

                    inputActions = new List<Action>(200);
                    inputActionBuffer = new List<Action>(200);

                    drawActions = new List<Action>(200);
                    drawActionBuffer = new List<Action>(200);

                    layoutActions = new List<Action<bool>>(200);

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

                    if (!treeManager.clients.Contains(client))
                    {
                        treeManager.clients.Add(client);
                        return true;
                    }
                    else
                        return false;
                }

                public static bool UnregisterClient(TreeClient client)
                {
                    if (treeManager != null)
                    {
                        bool success = treeManager.clients.Remove(client);
                        return success;
                    }
                    else
                        return false;
                }

                /// <summary>
                /// Updates UI tree, updates layout and draws
                /// </summary>
                public void Draw()
                {
                    int drawTick = instance.drawTick;
                    float resScale = instance._resScale;

                    mainClient.enableCursor = EnableCursor;

                    if (!UpdatingTree)
                        UpdateAccessorLists();

                    instance._cursor.Visible = false;

                    for (int n = 0; n < clients.Count; n++)
                    {
                        if (clients[n].enableCursor)
                            instance._cursor.Visible = true;
                    }

                    for (int n = 0; n < clients.Count; n++)
                        clients[n].Update(drawTick + (n % treeRefreshRate)); // Spread out client tree updates

                    treeTimes[drawTick] = treeTimer.ElapsedTicks;

                    drawTimer.Restart();

                    // Older clients (1.0.3-) node spaces require layout refreshes to function
                    bool rebuildLists = RefreshRequested && (drawTick % treeRefreshRate) == 0,
                        refreshLayout = lastResScale != resScale || rebuildLists;

                    for (int n = 0; n < layoutActions.Count; n++)
                        layoutActions[n](refreshLayout);

                    // Draw UI elements
                    for (int n = 0; n < drawActions.Count; n++)
                        drawActions[n]();

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
                    inputTimer.Restart();

                    for (int n = 0; n < depthTestActions.Count; n++)
                        depthTestActions[n]();

                    for (int n = inputActions.Count - 1; n >= 0; n--)
                        inputActions[n]();

                    inputTimer.Stop();
                    inputTimes[instance.drawTick] = inputTimer.ElapsedTicks;
                }

                /// <summary>
                /// Updates tree accessor delegate lists, spreading out updates over 5 ticks
                /// </summary>
                private void UpdateAccessorLists()
                {
                    instance.EnqueueTask(() => 
                    {
                        treeTimer.Restart();
                        UpdatingTree = true;

                        RebuildUpdateLists();
                        UpdateDistMap();
                        UpdateIndexBuffer();
                        ResetUpdateBuffers();
                        BuildSortedUpdateLists();

                        treeTimer.Stop();
                        ElementRegistered = updateAccessors.Count;

                        instance.EnqueueAction(() =>
                        {
                            MyUtils.Swap(ref depthTestActionBuffer, ref depthTestActions);
                            MyUtils.Swap(ref inputActionBuffer, ref inputActions);
                            MyUtils.Swap(ref drawActionBuffer, ref drawActions);

                            UpdatingTree = false;
                        });
                    });
                }

                /// <summary>
                /// Rebuilds main update accessor list from UI tree as well as the layout accessors
                /// </summary>
                private void RebuildUpdateLists()
                {
                    // Clear update lists and rebuild accessor lists from HUD tree
                    updateAccessors.Clear();
                    layoutActions.Clear();
                    uniqueOriginFuncs.Clear();

                    // Add client UI elements
                    for (int n = 0; n < clients.Count; n++)
                        updateAccessors.AddRange(clients[n].UpdateAccessors);

                    if (updateAccessors.Capacity > updateAccessors.Count * 3 && updateAccessors.Count > 200)
                        updateAccessors.TrimExcess();

                    // Build distance func HashSet
                    for (int n = 0; n < updateAccessors.Count; n++)
                        uniqueOriginFuncs.Add(updateAccessors[n].Item2.Item2);

                    layoutActions.EnsureCapacity(updateAccessors.Count);

                    // Build layout list (without sorting)
                    for (int n = 0; n < updateAccessors.Count; n++)
                    {
                        HudUpdateAccessors accessors = updateAccessors[n];
                        layoutActions.Add(accessors.Item5);
                    }

                    if (layoutActions.Capacity > layoutActions.Count * 3 && layoutActions.Count > 200)
                        layoutActions.TrimExcess();
                }

                /// <summary>
                /// Builds distance map for HUD Space Nodes
                /// </summary>
                private void UpdateDistMap()
                {
                    distMap.Clear();

                    // Update distance for each unique position delegate
                    // Max distance: 655.35m; Precision: 1cm/unit
                    //
                    // This should help keep profiler overhead for this part to a minimum by reducing the
                    // number of delegate calls to a small handful. This also means the cost difference between
                    // using Distance() and DistanceSquared() will be negligible.
                    Vector3D camPos = MyAPIGateway.Session.Camera.WorldMatrix.Translation;

                    foreach (Func<Vector3D> OriginFunc in uniqueOriginFuncs)
                    {
                        Vector3D nodeOrigin = OriginFunc();
                        double dist = Math.Round(Vector3D.Distance(nodeOrigin, camPos), 2);
                        var reverseDist = (ushort)(ushort.MaxValue - (ushort)Math.Min(dist * 100d, ushort.MaxValue));
                        distMap.Add(OriginFunc, reverseDist);
                    }

                    HudSpacesRegistered = distMap.Count;
                }

                /// <summary>
                /// Builds sorted index list for sorted accessor lists
                /// </summary>
                private void UpdateIndexBuffer()
                {
                    indexBuffer.Clear();
                    indexBuffer.EnsureCapacity(updateAccessors.Count);

                    // Build index list and sort by zOffset
                    for (int n = 0; n < updateAccessors.Count; n++)
                    {
                        var accessors = updateAccessors[n].Item2;
                        ulong index = (ulong)n,
                            zOffset = accessors.Item1(),
                            distance = distMap[accessors.Item2];

                        indexBuffer.Add((distance << 48) | (zOffset << 32) | index);
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

                    depthTestActionBuffer.EnsureCapacity(updateAccessors.Count);
                    inputActionBuffer.EnsureCapacity(updateAccessors.Count);
                    drawActionBuffer.EnsureCapacity(updateAccessors.Count);
                }

                /// <summary>
                /// Builds sorted update accessor lists
                /// </summary>
                private void BuildSortedUpdateLists()
                {
                    // Lower 32 bits store the index, upper 32 store draw depth and distance
                    ulong indexMask = 0x00000000FFFFFFFF;

                    // Build sorted depth test list
                    for (int n = 0; n < indexBuffer.Count; n++)
                    {
                        int index = (int)(indexBuffer[n] & indexMask);
                        HudUpdateAccessors accessors = updateAccessors[index];

                        depthTestActionBuffer.Add(accessors.Item3);
                    }

                    // Build sorted input list
                    for (int n = 0; n < indexBuffer.Count; n++)
                    {
                        int index = (int)(indexBuffer[n] & indexMask);
                        HudUpdateAccessors accessors = updateAccessors[index];

                        inputActionBuffer.Add(accessors.Item4);
                    }

                    // Build sorted draw list
                    for (int n = 0; n < indexBuffer.Count; n++)
                    {
                        int index = (int)(indexBuffer[n] & indexMask);
                        HudUpdateAccessors accessors = updateAccessors[index];

                        drawActionBuffer.Add(accessors.Item6);
                    }

                    if (depthTestActionBuffer.Capacity > depthTestActionBuffer.Count * 3 && depthTestActionBuffer.Count > 200)
                    {
                        depthTestActionBuffer.TrimExcess();
                        inputActionBuffer.TrimExcess();
                        drawActionBuffer.TrimExcess();
                    }
                }
            }
        }
    }
}