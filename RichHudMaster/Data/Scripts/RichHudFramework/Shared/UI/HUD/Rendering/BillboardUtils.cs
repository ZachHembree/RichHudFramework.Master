using System.Collections.Generic;
using System;
using System.Threading;
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
        namespace Rendering
        {
            public sealed class BillBoardUtils : RichHudComponentBase
            {
                private static BillBoardUtils instance;
                private static int triBillboardCount;
                private const int statsWindowSize = 240, sampleRateDiv = 5;

                private readonly List<MyBillboard> billboardBuffer;
                private List<MyTriangleBillboard> triBillboardBack, triBillboardFront;

                private readonly int[] billboardUsage, billboardAlloc;
                private readonly List<int> billboardUsageStats, billboardAllocStats;
                private int sampleTick, tick;

                static BillBoardUtils()
                {
                    MyTransparentGeometry.ApplyActionOnPersistentBillboards(Render);
                }

                private BillBoardUtils() : base(false, true)
                {
                    if (instance != null)
                        throw new Exception($"Only one instance of {GetType().Name} can exist at once.");

                    triBillboardBack = new List<MyTriangleBillboard>(1000);
                    triBillboardFront = new List<MyTriangleBillboard>(1000);
                    billboardBuffer = new List<MyBillboard>(100);

                    billboardUsage = new int[statsWindowSize];
                    billboardAlloc = new int[statsWindowSize];

                    billboardUsageStats = new List<int>(statsWindowSize);
                    billboardAllocStats = new List<int>(statsWindowSize);
                }

                public static void Init()
                {
                    if (instance == null)
                    {
                        instance = new BillBoardUtils();
                    }
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

                public override void Draw()
                { }

                private static void Render()
                {
                    if (instance != null)
                    {
                        
                    }
                }

                public override void HandleInput()
                {
                    if (tick == 0)
                    {
                        billboardUsage[sampleTick] = triBillboardCount;
                        billboardUsageStats.Clear();
                        billboardUsageStats.AddRange(billboardUsage);
                        billboardUsageStats.Sort();

                        billboardAlloc[sampleTick] = triBillboardBack.Count;
                        billboardAllocStats.Clear();
                        billboardAllocStats.AddRange(billboardAlloc);
                        billboardAllocStats.Sort();

                        sampleTick++;
                        sampleTick %= statsWindowSize;
                    }

                    tick++;
                    tick %= sampleRateDiv;

                    MyUtils.Swap(ref instance.triBillboardBack, ref instance.triBillboardFront);
                    triBillboardCount = 0;
                }

                public override void Close()
                {
                    instance = null;
                }

                /// <summary>
                /// Renders a polygon from a given set of unique vertex coordinates. Triangles are defined by their
                /// indices and the tex coords are parallel to the vertex list.
                /// </summary>
                public static void AddTriangles(List<int> indices, List<Vector3D> vertices, ref PolyMaterial mat)
                {
                    var bbPool = instance.triBillboardBack;
                    var bbBuf = instance.billboardBuffer;

                    int triangleCount = indices.Count / 3,
                        bbRemaining = bbPool.Count - triBillboardCount,
                        bbToAdd = Math.Max(triangleCount - bbRemaining, 0);

                    bbPool.EnsureCapacity(triBillboardCount + triangleCount);

                    for (int i = 0; i < bbToAdd; i++)
                        bbPool.Add(new MyTriangleBillboard());

                    for (int i = triBillboardCount; i < triangleCount + triBillboardCount; i++)
                        bbBuf.Add(instance.triBillboardBack[i]);

                    for (int i = 0; i < indices.Count; i += 3)
                    {
                        MyTriangleBillboard bb = instance.triBillboardBack[triBillboardCount];
                        triBillboardCount++;

                        bb.BlendType = BlendTypeEnum.PostPP;
                        bb.Position0 = vertices[indices[i]];
                        bb.Position1 = vertices[indices[i + 1]];
                        bb.Position2 = vertices[indices[i + 2]];
                        bb.UV0 = mat.texCoords[indices[i]];
                        bb.UV1 = mat.texCoords[indices[i + 1]];
                        bb.UV2 = mat.texCoords[indices[i + 2]];
                        bb.Material = mat.textureID;
                        bb.Color = mat.bbColor;
                        bb.ColorIntensity = 1f;
                        bb.CustomViewProjection = -1;
                    }

                    MyTransparentGeometry.AddBillboards(bbBuf, false);
                    bbBuf.Clear();
                }

                /// <summary>
                /// Adds a triangles in the given starting index range
                /// </summary>
                public static void AddTriangleRange(Vector2I range, List<int> indices, List<Vector3D> vertices, ref PolyMaterial mat)
                {
                    var bbPool = instance.triBillboardBack;
                    var bbBuf = instance.billboardBuffer;

                    int triangleCount = (range.Y - range.X) / 3,
                        bbRemaining = bbPool.Count - triBillboardCount,
                        bbToAdd = Math.Max(triangleCount - bbRemaining, 0);

                    bbPool.EnsureCapacity(triBillboardCount + triangleCount);

                    for (int i = 0; i < bbToAdd; i++)
                        bbPool.Add(new MyTriangleBillboard());

                    for (int i = triBillboardCount; i < triangleCount + triBillboardCount; i++)
                        bbBuf.Add(instance.triBillboardBack[i]);

                    for (int i = range.X; i <= range.Y; i += 3)
                    {
                        MyTriangleBillboard bb = instance.triBillboardBack[triBillboardCount];
                        triBillboardCount++;

                        bb.BlendType = BlendTypeEnum.PostPP;
                        bb.Position0 = vertices[indices[i]];
                        bb.Position1 = vertices[indices[i + 1]];
                        bb.Position2 = vertices[indices[i + 2]];
                        bb.UV0 = mat.texCoords[indices[i]];
                        bb.UV1 = mat.texCoords[indices[i + 1]];
                        bb.UV2 = mat.texCoords[indices[i + 2]];
                        bb.Material = mat.textureID;
                        bb.Color = mat.bbColor;
                        bb.ColorIntensity = 1f;
                        bb.CustomViewProjection = -1;
                    }

                    MyTransparentGeometry.AddBillboards(bbBuf, false);
                    bbBuf.Clear();
                }

                /// <summary>
                /// Renders a polygon from a given set of unique vertex coordinates. Triangles are defined by their
                /// indices.
                /// </summary>
                public static void AddTriangles(List<int> indices, List<Vector3D> vertices, ref TriMaterial mat)
                {
                    var bbPool = instance.triBillboardBack;
                    var bbBuf = instance.billboardBuffer;

                    int triangleCount = indices.Count / 3, 
                        bbRemaining = bbPool.Count - triBillboardCount,
                        bbToAdd = Math.Max(triangleCount - bbRemaining, 0);

                    bbPool.EnsureCapacity(triBillboardCount + triangleCount);

                    for (int i = 0; i < bbToAdd; i++)
                        bbPool.Add(new MyTriangleBillboard());

                    for (int i = triBillboardCount; i < triangleCount + triBillboardCount; i++)
                        bbBuf.Add(instance.triBillboardBack[i]);

                    for (int i = 0; i < indices.Count; i += 3)
                    {
                        MyTriangleBillboard bb = instance.triBillboardBack[triBillboardCount];
                        triBillboardCount++;

                        bb.BlendType = BlendTypeEnum.PostPP;
                        bb.Position0 = vertices[indices[i]];
                        bb.Position1 = vertices[indices[i + 1]];
                        bb.Position2 = vertices[indices[i + 2]];
                        bb.UV0 = mat.texCoords.Point0;
                        bb.UV1 = mat.texCoords.Point1;
                        bb.UV2 = mat.texCoords.Point2;
                        bb.Material = mat.textureID;
                        bb.Color = mat.bbColor;
                        bb.ColorIntensity = 1f;
                        bb.CustomViewProjection = -1;
                    }

                    MyTransparentGeometry.AddBillboards(bbBuf, false);
                    bbBuf.Clear();
                }

                /// <summary>
                /// Adds a triangle starting at the given index.
                /// </summary>
                public static void AddTriangle(int start, List<int> indices, List<Vector3D> vertices, ref TriMaterial mat)
                {
                    MyTriangleBillboard bb;
                    triBillboardCount++;

                    if (triBillboardCount < instance.triBillboardBack.Count)
                        bb = instance.triBillboardBack[triBillboardCount];
                    else
                    {
                        bb = new MyTriangleBillboard();
                        instance.triBillboardBack.Add(bb);
                    }

                    bb.BlendType = BlendTypeEnum.PostPP;
                    bb.Position0 = vertices[indices[start]];
                    bb.Position1 = vertices[indices[start + 1]];
                    bb.Position2 = vertices[indices[start + 2]];
                    bb.UV0 = mat.texCoords.Point0;
                    bb.UV1 = mat.texCoords.Point1;
                    bb.UV2 = mat.texCoords.Point2;
                    bb.Material = mat.textureID;
                    bb.Color = mat.bbColor;
                    bb.ColorIntensity = 1f;
                    bb.CustomViewProjection = -1;

                    MyTransparentGeometry.AddBillboard(bb, false);
                }

                public static void AddTriangle(ref TriMaterial mat, ref TriangleD triangle)
                {
                    MyTriangleBillboard bb;
                    triBillboardCount++;

                    if (triBillboardCount < instance.triBillboardBack.Count)
                        bb = instance.triBillboardBack[triBillboardCount];
                    else
                    {
                        bb = new MyTriangleBillboard();
                        instance.triBillboardBack.Add(bb);
                    }

                    bb.BlendType = BlendTypeEnum.PostPP;
                    bb.Position0 = triangle.Point0;
                    bb.Position1 = triangle.Point1;
                    bb.Position2 = triangle.Point2;
                    bb.UV0 = mat.texCoords.Point0;
                    bb.UV1 = mat.texCoords.Point1;
                    bb.UV2 = mat.texCoords.Point2;
                    bb.Material = mat.textureID;
                    bb.Color = mat.bbColor;
                    bb.ColorIntensity = 1f;
                    bb.CustomViewProjection = -1;

                    MyTransparentGeometry.AddBillboard(bb, false);
                }

                public static void AddQuad(ref QuadMaterial mat, ref MyQuadD quad)
                {
                    MyTriangleBillboard bbL, bbR;
                    triBillboardCount++;

                    if (triBillboardCount < instance.triBillboardBack.Count)
                        bbL = instance.triBillboardBack[triBillboardCount];
                    else
                    {
                        bbL = new MyTriangleBillboard();
                        instance.triBillboardBack.Add(bbL);
                    }

                    triBillboardCount++;

                    if (triBillboardCount < instance.triBillboardBack.Count)
                        bbR = instance.triBillboardBack[triBillboardCount];
                    else
                    {
                        bbR = new MyTriangleBillboard();
                        instance.triBillboardBack.Add(bbR);
                    }

                    bbL.BlendType = BlendTypeEnum.PostPP;
                    bbL.Position0 = quad.Point0;
                    bbL.Position1 = quad.Point1;
                    bbL.Position2 = quad.Point2;
                    bbL.UV0 = mat.texCoords.Point0;
                    bbL.UV1 = mat.texCoords.Point1;
                    bbL.UV2 = mat.texCoords.Point2;
                    bbL.Material = mat.textureID;
                    bbL.Color = mat.bbColor;
                    bbL.ColorIntensity = 1f;
                    bbL.CustomViewProjection = -1;

                    bbR.BlendType = BlendTypeEnum.PostPP;
                    bbR.Position0 = quad.Point0;
                    bbR.Position1 = quad.Point2;
                    bbR.Position2 = quad.Point3;
                    bbR.UV0 = mat.texCoords.Point0;
                    bbR.UV1 = mat.texCoords.Point2;
                    bbR.UV2 = mat.texCoords.Point3;
                    bbR.Material = mat.textureID;
                    bbR.Color = mat.bbColor;
                    bbR.ColorIntensity = 1f;
                    bbR.CustomViewProjection = -1;

                    MyTransparentGeometry.AddBillboard(bbL, false);
                    MyTransparentGeometry.AddBillboard(bbR, false);
                }

                public static void AddQuad(ref BoundedQuadMaterial mat, ref MyQuadD quad)
                {
                    MyTriangleBillboard bbL, bbR;
                    triBillboardCount++;

                    if (triBillboardCount < instance.triBillboardBack.Count)
                        bbL = instance.triBillboardBack[triBillboardCount];
                    else
                    {
                        bbL = new MyTriangleBillboard();
                        instance.triBillboardBack.Add(bbL);
                    }

                    triBillboardCount++;

                    if (triBillboardCount < instance.triBillboardBack.Count)
                        bbR = instance.triBillboardBack[triBillboardCount];
                    else
                    {
                        bbR = new MyTriangleBillboard();
                        instance.triBillboardBack.Add(bbR);
                    }

                    bbL.BlendType = BlendTypeEnum.PostPP;
                    bbL.Position0 = quad.Point0;
                    bbL.Position1 = quad.Point1;
                    bbL.Position2 = quad.Point2;
                    bbL.UV0 = mat.texBounds.Min;
                    bbL.UV1 = mat.texBounds.Min + new Vector2(0f, mat.texBounds.Size.Y);
                    bbL.UV2 = mat.texBounds.Max;
                    bbL.Material = mat.textureID;
                    bbL.Color = mat.bbColor;
                    bbL.ColorIntensity = 1f;
                    bbL.CustomViewProjection = -1;

                    bbR.BlendType = BlendTypeEnum.PostPP;
                    bbR.Position0 = quad.Point0;
                    bbR.Position1 = quad.Point2;
                    bbR.Position2 = quad.Point3;
                    bbR.UV0 = mat.texBounds.Min;
                    bbR.UV1 = mat.texBounds.Max;
                    bbR.UV2 = mat.texBounds.Min + new Vector2(mat.texBounds.Size.X, 0f);
                    bbR.Material = mat.textureID;
                    bbR.Color = mat.bbColor;
                    bbR.ColorIntensity = 1f;
                    bbR.CustomViewProjection = -1;

                    MyTransparentGeometry.AddBillboard(bbL, false);
                    MyTransparentGeometry.AddBillboard(bbR, false);
                }

                /// <summary>
                /// Converts a color to its normalized linear RGB equivalent. Assumes additive blending
                /// with premultiplied alpha.
                /// </summary>
                public static Vector4 GetBillBoardBoardColor(Color color)
                {
                    float opacity = color.A / 255f;

                    color.R = (byte)(color.R * opacity);
                    color.G = (byte)(color.G * opacity);
                    color.B = (byte)(color.B * opacity);

                    return ((Vector4)color).ToLinearRGB();
                }
            }
        }
    }
}