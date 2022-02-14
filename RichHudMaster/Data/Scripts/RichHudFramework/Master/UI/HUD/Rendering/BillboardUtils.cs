using System.Collections.Generic;
using System;
using System.Threading;
using Sandbox.ModAPI;
using VRage.Game;
using VRage;
using VRage.Utils;
using VRageMath;
using VRageRender;
using RichHudFramework.Internal;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace RichHudFramework
{
    namespace UI
    {
        using ApiMemberAccessor = System.Func<object, int, object>;
        using TriangleBillboardData = MyTuple<
            BlendTypeEnum, // blendType
            Vector2I, // bbID + matrixID
            MyStringId, // material
            Vector4, // color
            MyTuple<Vector2, Vector2, Vector2>, // texCoords
            MyTuple<Vector3D, Vector3D, Vector3D> // vertexPos
        >;
        using FlatTriangleBillboardData = MyTuple<
            BlendTypeEnum, // blendType
            Vector2I, // bbID + matrixID
            MyStringId, // material
            MyTuple<Vector4, BoundingBox2?>, // color + mask
            MyTuple<Vector2, Vector2, Vector2>, // texCoords
            MyTuple<Vector2, Vector2, Vector2> // flat pos
        >;

        namespace Rendering
        {
            using BbUtilData = MyTuple<
                ApiMemberAccessor, // GetOrSetMember
                List<TriangleBillboardData>, // triangleList
                List<FlatTriangleBillboardData>, // flatTriangleList
                List<MatrixD>, // matrixBuf
                Dictionary<MatrixD[], int> // matrixTable
            >;

            public sealed partial class BillBoardUtils : RichHudComponentBase
            {
                private static BillBoardUtils instance;
                private const int statsWindowSize = 240, sampleRateDiv = 5,
                    bufMinResizeThreshold = 4000;

                private readonly List<MyTriangleBillboard> bbBuf;
                private readonly List<MyTriangleBillboard>[] bbSwapPools;
                private List<MyTriangleBillboard> bbPoolBack;
                private int currentPool;

                private readonly List<TriangleBillboardData> triangleList;
                private readonly List<FlatTriangleBillboardData> flatTriangleList;
                private readonly List<MatrixD> matrixBuf;
                private readonly Dictionary<MatrixD[], int> matrixTable;

                private readonly int[] billboardUsage, billboardAlloc, matrixUsage;
                private readonly List<int> billboardUsageStats, billboardAllocStats, matrixUsageStats;
                private readonly Action UpdateBillboardsCallback;
                private int sampleTick, tick;

                private BillBoardUtils() : base(false, true)
                {
                    if (instance != null)
                        throw new Exception($"Only one instance of {GetType().Name} can exist at once.");

                    bbSwapPools = new List<MyTriangleBillboard>[]
                    {
                        new List<MyTriangleBillboard>(1000),
                        new List<MyTriangleBillboard>(1000),
                        new List<MyTriangleBillboard>(1000)
                    };
                    bbBuf = new List<MyTriangleBillboard>(1000);

                    triangleList = new List<TriangleBillboardData>();
                    flatTriangleList = new List<FlatTriangleBillboardData>(1000);
                    matrixBuf = new List<MatrixD>();
                    matrixTable = new Dictionary<MatrixD[], int>();

                    billboardUsage = new int[statsWindowSize];
                    billboardAlloc = new int[statsWindowSize];
                    matrixUsage = new int[statsWindowSize];

                    billboardUsageStats = new List<int>(statsWindowSize);
                    billboardAllocStats = new List<int>(statsWindowSize);
                    matrixUsageStats = new List<int>(statsWindowSize);

                    UpdateBillboardsCallback = UpdateBillboards;
                }

                public static void Init()
                {
                    if (instance == null)
                    {
                        instance = new BillBoardUtils();
                    }
                }

                public override void Close()
                {
                    instance = null;
                }

                /// <summary>
                /// Returns billboard usage at the given percentile.
                /// </summary>
                public static int GetUsagePercentile(float percentile)
                {
                    if (instance != null)
                        return instance.billboardUsageStats[(int)(statsWindowSize * percentile)];
                    else
                        throw new Exception($"{typeof(BillBoardUtils).Name} not initialized!");
                }

                /// <summary>
                /// Returns billboard usage at the given percentile.
                /// </summary>
                public static int GetAllocPercentile(float percentile)
                {
                    if (instance != null)
                        return instance.billboardAllocStats[(int)(statsWindowSize * percentile)];
                    else
                        throw new Exception($"{typeof(BillBoardUtils).Name} not initialized!");
                }

                /// <summary>
                /// Returns unique matrix usage at the given percentile.
                /// </summary>
                public static int GetMatrixUsagePercentile(float percentile)
                {
                    if (instance != null)
                        return instance.matrixUsageStats[(int)(statsWindowSize * percentile)];
                    else
                        throw new Exception($"{typeof(BillBoardUtils).Name} not initialized!");
                }

                /// <summary>
                /// Returns data needed to initialize client BB utils
                /// </summary>
                public static BbUtilData GetApiData()
                {
                    return new BbUtilData 
                    {
                        Item1 = GetOrSetMember,
                        Item2 = instance.triangleList,
                        Item3 = instance.flatTriangleList,
                        Item4 = instance.matrixBuf,
                        Item5 = instance.matrixTable
                    };
                }

                private static object GetOrSetMember(object data, int member)
                {
                    switch ((BillBoardUtilAccessors)member)
                    {
                        case BillBoardUtilAccessors.GetPoolBack:
                            return instance.bbPoolBack;
                    }

                    return null;
                }

                public static void BeginDraw()
                {
                    if (instance != null)
                    {
                        instance.BeginDrawInternal();
                    }
                }

                public static void FinishDraw()
                {
                    if (instance != null)
                    {
                        instance.FinishDrawInternal();
                    }
                }

                private void BeginDrawInternal()
                {
                    bbPoolBack = bbSwapPools[currentPool];                    
                }

                private void FinishDrawInternal()
                {
                    MyTransparentGeometry.ApplyActionOnPersistentBillboards(UpdateBillboardsCallback);
                    currentPool = (currentPool + 1) % bbSwapPools.Length;

                    if (tick == 0)
                    {
                        UpdateStats();

                        sampleTick++;
                        sampleTick %= statsWindowSize;

                        UpdateBllboardTrimming();
                    }

                    triangleList.Clear();
                    flatTriangleList.Clear();
                    matrixBuf.Clear();
                    matrixTable.Clear();

                    tick++;
                    tick %= sampleRateDiv;
                }

                /// <summary>
                /// Updates pool usages stats
                /// </summary>
                private void UpdateStats()
                {
                    billboardUsage[sampleTick] = flatTriangleList.Count + triangleList.Count;
                    billboardUsageStats.Clear();
                    billboardUsageStats.AddRange(billboardUsage);
                    billboardUsageStats.Sort();

                    billboardAlloc[sampleTick] = flatTriangleList.Capacity + triangleList.Capacity;
                    billboardAllocStats.Clear();
                    billboardAllocStats.AddRange(billboardAlloc);
                    billboardAllocStats.Sort();

                    matrixUsage[sampleTick] = matrixBuf.Count;
                    matrixUsageStats.Clear();
                    matrixUsageStats.AddRange(matrixUsage);
                    matrixUsageStats.Sort();
                }

                /// <summary>
                /// Trims billboard pool and data periodically
                /// </summary>
                private void UpdateBllboardTrimming()
                {
                    int usage99 = GetUsagePercentile(.99f),
                            alloc01 = GetAllocPercentile(0.01f);
                    bool trimBillboards = false;

                    // 2D Bllboards billboards
                    if (flatTriangleList.Capacity > bufMinResizeThreshold
                        && flatTriangleList.Capacity > 3 * flatTriangleList.Count
                        && flatTriangleList.Capacity > 3 * usage99
                        && alloc01 > 3 * usage99
                    )
                    {
                        int max = Math.Max(2 * flatTriangleList.Count, bufMinResizeThreshold);
                        flatTriangleList.ClearAndTrim(max);
                        trimBillboards = true;
                    }

                    // 3D Billboard data
                    if (triangleList.Capacity > bufMinResizeThreshold
                        && triangleList.Capacity > 3 * triangleList.Count
                        && triangleList.Capacity > 3 * usage99
                        && alloc01 > 3 * usage99
                    )
                    {
                        int max = Math.Max(2 * triangleList.Count, bufMinResizeThreshold);
                        triangleList.ClearAndTrim(max);
                        trimBillboards = true;
                    }

                    // Trim billboards
                    if (trimBillboards)
                    {
                        int max = triangleList.Capacity + flatTriangleList.Capacity;
                        bbBuf.ClearAndTrim(max);

                        foreach (List<MyTriangleBillboard> bb in bbSwapPools)
                        {
                            int remStart = Math.Min(max, bb.Count - 1),
                                remCount = Math.Max(bb.Count - remStart, 0);

                            bb.RemoveRange(remStart, remCount);
                            bb.TrimExcess();
                        }
                    }
                }

                private void UpdateBillboards()
                {
                    Update3dBillboards();
                    UpdateFlatBillboards();
                }

                private void Update3dBillboards()
                {
                    int count = Math.Min(triangleList.Count, bbPoolBack.Count),
                        stride = Math.Max(500, MathHelper.CeilToInt(count / 8d));

                    MyAPIGateway.Parallel.For(0, count, i => 
                    {
                        TriangleBillboardData bbData = triangleList[i];
                        MyTriangleBillboard bb = bbPoolBack[bbData.Item2.X];

                        if (bbData.Item2.Y != -1)
                        {
                            MatrixD matrix = matrixBuf[bbData.Item2.Y];
                            Vector3D.TransformNoProjection(ref bbData.Item6.Item1, ref matrix, out bbData.Item6.Item1);
                            Vector3D.TransformNoProjection(ref bbData.Item6.Item2, ref matrix, out bbData.Item6.Item2);
                            Vector3D.TransformNoProjection(ref bbData.Item6.Item3, ref matrix, out bbData.Item6.Item3);
                        }

                        bb.BlendType = bbData.Item1;
                        bb.Material = bbData.Item3;
                        bb.Color = bbData.Item4;
                        bb.UV0 = bbData.Item5.Item1;
                        bb.UV1 = bbData.Item5.Item2;
                        bb.UV2 = bbData.Item5.Item3;
                        bb.Position0 = bbData.Item6.Item1;
                        bb.Position1 = bbData.Item6.Item2;
                        bb.Position2 = bbData.Item6.Item3;
                    });
                }

                private void UpdateFlatBillboards()
                {
                    int count = Math.Min(flatTriangleList.Count, bbPoolBack.Count),
                        stride = Math.Max(500, MathHelper.CeilToInt(count / 8d));

                    MyAPIGateway.Parallel.For(0, count, i =>
                    {
                        FlatTriangleBillboardData bbData = flatTriangleList[i];
                        Triangle planePos = new Triangle { Point0 = bbData.Item6.Item1, Point1 = bbData.Item6.Item2, Point2 = bbData.Item6.Item3 },
                            texCoords = new Triangle { Point0 = bbData.Item5.Item1, Point1 = bbData.Item5.Item2, Point2 = bbData.Item5.Item3 };
                        BoundingBox2? mask = bbData.Item4.Item2;

                        // Masking/clipping
                        if (mask != null)
                        {
                            BoundingBox2 bounds = new BoundingBox2(Vector2.Min(planePos.Point1, planePos.Point2), planePos.Point0),
                                texBounds = new BoundingBox2(Vector2.Min(texCoords.Point0, texCoords.Point2), Vector2.Max(texCoords.Point0, texCoords.Point1));
                            Vector2 size = bounds.Size,
                                pos = bounds.Center;

                            bounds = bounds.Intersect(mask.Value);
                            planePos.Point0 = Vector2.Clamp(planePos.Point0, bounds.Min, bounds.Max);
                            planePos.Point1 = Vector2.Clamp(planePos.Point1, bounds.Min, bounds.Max);
                            planePos.Point2 = Vector2.Clamp(planePos.Point2, bounds.Min, bounds.Max);

                            // Dont bother clipping texcoords for solid colors
                            if (bbData.Item3 != Material.Default.TextureID)
                            {
                                Vector2 clipSize = bounds.Size;

                                // Normalized cropped size and offset
                                Vector2 clipScale = clipSize / size,
                                    clipOffset = (bounds.Center - pos) / size,
                                    uvScale = texBounds.Size,
                                    uvOffset = texBounds.Center;

                                pos += clipOffset * size; // Offset billboard to compensate for changes in size
                                size = clipSize; // Use cropped billboard size
                                clipOffset *= uvScale * new Vector2(1f, -1f); // Scale offset to fit material and flip Y-axis

                                // Recalculate texture coordinates to simulate clipping without affecting material alignment
                                texBounds.Min = ((texBounds.Min - uvOffset) * clipScale) + (uvOffset + clipOffset);
                                texBounds.Max = ((texBounds.Max - uvOffset) * clipScale) + (uvOffset + clipOffset);

                                texCoords.Point0 = Vector2.Clamp(texCoords.Point0, texBounds.Min, texBounds.Max);
                                texCoords.Point1 = Vector2.Clamp(texCoords.Point1, texBounds.Min, texBounds.Max);
                                texCoords.Point2 = Vector2.Clamp(texCoords.Point2, texBounds.Min, texBounds.Max);
                            }
                        }

                        // Transform 2D planar positions into world space
                        MatrixD matrix = matrixBuf[bbData.Item2.Y];
                        TriangleD worldPos = new TriangleD
                        {
                            Point0 = matrix.Translation + (planePos.Point0.X * matrix.Right) + (planePos.Point0.Y * matrix.Up),
                            Point1 = matrix.Translation + (planePos.Point1.X * matrix.Right) + (planePos.Point1.Y * matrix.Up),
                            Point2 = matrix.Translation + (planePos.Point2.X * matrix.Right) + (planePos.Point2.Y * matrix.Up)
                        };

                        MyTriangleBillboard bb = bbPoolBack[bbData.Item2.X];
                        bb.BlendType = bbData.Item1;
                        bb.Position0 = worldPos.Point0;
                        bb.Position1 = worldPos.Point1;
                        bb.Position2 = worldPos.Point2;
                        bb.UV0 = texCoords.Point0;
                        bb.UV1 = texCoords.Point1;
                        bb.UV2 = texCoords.Point2;
                        bb.Material = bbData.Item3;
                        bb.Color = bbData.Item4.Item1;
                    }, stride);
                }

            }
        }
    }
}