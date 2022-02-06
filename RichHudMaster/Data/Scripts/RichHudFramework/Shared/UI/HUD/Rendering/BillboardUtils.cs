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
        namespace Rendering
        {
            public struct TriangleBillboardData
            {
                public BlendTypeEnum blendType;
                public MyStringId material;
                public Vector4 color;
                public Triangle texCoords;
                public TriangleD positions;
            }

            public sealed class BillBoardUtils : RichHudComponentBase
            {
                private static BillBoardUtils instance;
                private const int statsWindowSize = 240, sampleRateDiv = 5;

                private readonly List<MyTriangleBillboard> bbBuf;
                private readonly List<MyTriangleBillboard> bbPool;

                private List<TriangleBillboardData> bbDataBack, bbDataFront;

                private readonly int[] billboardUsage, billboardAlloc;
                private readonly List<int> billboardUsageStats, billboardAllocStats;
                private readonly Action RenderCallback;
                private int sampleTick, tick;

                private BillBoardUtils() : base(false, true)
                {
                    if (instance != null)
                        throw new Exception($"Only one instance of {GetType().Name} can exist at once.");

                    bbPool = new List<MyTriangleBillboard>(1000);
                    bbBuf = new List<MyTriangleBillboard>(100);

                    bbDataBack = new List<TriangleBillboardData>(1000);
                    bbDataFront = new List<TriangleBillboardData>(1000);

                    billboardUsage = new int[statsWindowSize];
                    billboardAlloc = new int[statsWindowSize];

                    billboardUsageStats = new List<int>(statsWindowSize);
                    billboardAllocStats = new List<int>(statsWindowSize);

                    RenderCallback = Render;
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
                {
                    MyTransparentGeometry.ApplyActionOnPersistentBillboards(RenderCallback);

                    if (tick == 0)
                    {
                        billboardUsage[sampleTick] = bbDataBack.Count;
                        billboardUsageStats.Clear();
                        billboardUsageStats.AddRange(billboardUsage);
                        billboardUsageStats.Sort();

                        billboardAlloc[sampleTick] = bbDataBack.Capacity;
                        billboardAllocStats.Clear();
                        billboardAllocStats.AddRange(billboardAlloc);
                        billboardAllocStats.Sort();

                        sampleTick++;
                        sampleTick %= statsWindowSize;
                    }

                    tick++;
                    tick %= sampleRateDiv;

                    if (Monitor.IsEntered(bbDataBack))
                        Monitor.Exit(bbDataBack);

                    Monitor.Enter(bbDataFront);
                    MyUtils.Swap(ref bbDataBack, ref bbDataFront);

                    bbDataBack.Clear();
                }   

                private void Render()
                {
                    try
                    {
                        Monitor.Enter(bbDataFront);

                        for (int i = 0; i < bbDataFront.Count; i++)
                        {
                            MyTriangleBillboard bb = bbPool[i];
                            TriangleBillboardData bbData = bbDataFront[i];

                            bb.BlendType = bbData.blendType;
                            bb.Position0 = bbData.positions.Point0;
                            bb.Position1 = bbData.positions.Point1;
                            bb.Position2 = bbData.positions.Point2;
                            bb.UV0 = bbData.texCoords.Point0;
                            bb.UV1 = bbData.texCoords.Point1;
                            bb.UV2 = bbData.texCoords.Point2;
                            bb.Material = bbData.material;
                            bb.Color = bbData.color;
                        }
                    }
                    catch (Exception e)
                    {
                        ExceptionHandler.ReportException(e);
                    }
                    finally
                    {
                        if (Monitor.IsEntered(bbDataFront))
                            Monitor.Exit(bbDataFront);
                    }
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
                    var bbPool = instance.bbPool;
                    var bbDataBack = instance.bbDataBack;
                    var bbBuf = instance.bbBuf;

                    int triangleCount = indices.Count / 3,
                        bbRemaining = bbPool.Count - bbDataBack.Count,
                        bbToAdd = Math.Max(triangleCount - bbRemaining, 0);

                    bbPool.EnsureCapacity(bbDataBack.Count + triangleCount);
                    bbBuf.Clear();

                    for (int i = 0; i < bbToAdd; i++)
                        bbPool.Add(GetNewBB());

                    for (int i = bbDataBack.Count; i < triangleCount + bbDataBack.Count; i++)
                        bbBuf.Add(instance.bbPool[i]);

                    for (int i = 0; i < indices.Count; i += 3)
                    {
                        var bb = new TriangleBillboardData
                        {
                            blendType = BlendTypeEnum.PostPP,
                            positions = new TriangleD
                            {
                                Point0 = vertices[indices[i]],
                                Point1 = vertices[indices[i + 1]],
                                Point2 = vertices[indices[i + 2]],
                            },
                            texCoords = new Triangle
                            {
                                Point0 = mat.texCoords[indices[i]],
                                Point1 = mat.texCoords[indices[i + 1]],
                                Point2 = mat.texCoords[indices[i + 2]],
                            },
                            material = mat.textureID,
                            color = mat.bbColor,
                        };
                        bbDataBack.Add(bb);
                    }

                    MyTransparentGeometry.AddBillboards(bbBuf, false);
                    bbBuf.Clear();
                }

                /// <summary>
                /// Adds a triangles in the given starting index range
                /// </summary>
                public static void AddTriangleRange(Vector2I range, List<int> indices, List<Vector3D> vertices, ref PolyMaterial mat)
                {
                    var bbPool = instance.bbPool;
                    var bbDataBack = instance.bbDataBack;
                    var bbBuf = instance.bbBuf;

                    int triangleCount = indices.Count / 3,
                        bbRemaining = bbPool.Count - bbDataBack.Count,
                        bbToAdd = Math.Max(triangleCount - bbRemaining, 0);

                    bbPool.EnsureCapacity(bbDataBack.Count + triangleCount);
                    bbBuf.Clear();

                    for (int i = 0; i < bbToAdd; i++)
                        bbPool.Add(GetNewBB());

                    for (int i = bbDataBack.Count; i < triangleCount + bbDataBack.Count; i++)
                        bbBuf.Add(instance.bbPool[i]);

                    for (int i = range.X; i <= range.Y; i += 3)
                    {
                        var bb = new TriangleBillboardData
                        {
                            blendType = BlendTypeEnum.PostPP,
                            positions = new TriangleD
                            {
                                Point0 = vertices[indices[i]],
                                Point1 = vertices[indices[i + 1]],
                                Point2 = vertices[indices[i + 2]],
                            },
                            texCoords = new Triangle
                            {
                                Point0 = mat.texCoords[indices[i]],
                                Point1 = mat.texCoords[indices[i + 1]],
                                Point2 = mat.texCoords[indices[i + 2]],
                            },
                            material = mat.textureID,
                            color = mat.bbColor,
                        };
                        bbDataBack.Add(bb);
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
                    var bbPool = instance.bbPool;
                    var bbDataBack = instance.bbDataBack;
                    var bbBuf = instance.bbBuf;

                    int triangleCount = indices.Count / 3, 
                        bbRemaining = bbPool.Count - bbDataBack.Count,
                        bbToAdd = Math.Max(triangleCount - bbRemaining, 0);

                    bbPool.EnsureCapacity(bbDataBack.Count + triangleCount);
                    bbBuf.Clear();

                    for (int i = 0; i < bbToAdd; i++)
                        bbPool.Add(GetNewBB());

                    for (int i = bbDataBack.Count; i < triangleCount + bbDataBack.Count; i++)
                        bbBuf.Add(instance.bbPool[i]);

                    for (int i = 0; i < indices.Count; i += 3)
                    {
                        var bb = new TriangleBillboardData
                        {
                            blendType = BlendTypeEnum.PostPP,
                            positions = new TriangleD
                            {
                                Point0 = vertices[indices[i]],
                                Point1 = vertices[indices[i + 1]],
                                Point2 = vertices[indices[i + 2]],
                            },
                            texCoords = mat.texCoords,
                            material = mat.textureID,
                            color = mat.bbColor,
                        };
                        bbDataBack.Add(bb);
                    }

                    MyTransparentGeometry.AddBillboards(bbBuf, false);
                    bbBuf.Clear();
                }

                /// <summary>
                /// Adds a triangle starting at the given index.
                /// </summary>
                public static void AddTriangle(int start, List<int> indices, List<Vector3D> vertices, ref TriMaterial mat)
                {
                    var bbPool = instance.bbPool;
                    var bbDataBack = instance.bbDataBack;
                    int index = bbDataBack.Count;

                    var bb = new TriangleBillboardData
                    {
                        blendType = BlendTypeEnum.PostPP,
                        positions = new TriangleD
                        {
                            Point0 = vertices[indices[start]],
                            Point1 = vertices[indices[start + 1]],
                            Point2 = vertices[indices[start + 2]],
                        },
                        texCoords = mat.texCoords,
                        material = mat.textureID,
                        color = mat.bbColor,
                    };
                    bbDataBack.Add(bb);

                    if (index >= bbPool.Count)
                        bbPool.Add(GetNewBB());

                    MyTransparentGeometry.AddBillboard(bbPool[index], false);
                }

                public static void AddTriangle(ref TriMaterial mat, ref TriangleD triangle)
                {
                    var bbPool = instance.bbPool;
                    var bbDataBack = instance.bbDataBack;
                    int index = bbDataBack.Count;

                    var bb = new TriangleBillboardData
                    {
                        blendType = BlendTypeEnum.PostPP,
                        positions = triangle,
                        texCoords = mat.texCoords,
                        material = mat.textureID,
                        color = mat.bbColor,
                    };
                    bbDataBack.Add(bb);

                    if (index >= bbPool.Count)
                        bbPool.Add(GetNewBB());

                    MyTransparentGeometry.AddBillboard(bbPool[index], false);
                }

                public static void AddQuad(ref QuadMaterial mat, ref MyQuadD quad)
                {
                    var bbPool = instance.bbPool;
                    var bbDataBack = instance.bbDataBack;
                    int indexL = bbDataBack.Count,
                        indexR = bbDataBack.Count + 1;

                    var bbL = new TriangleBillboardData
                    {
                        blendType = BlendTypeEnum.PostPP,
                        positions = new TriangleD
                        {
                            Point0 = quad.Point0,
                            Point1 = quad.Point1,
                            Point2 = quad.Point2,
                        },
                        texCoords = new Triangle
                        {
                            Point0 = mat.texCoords.Point0,
                            Point1 = mat.texCoords.Point1,
                            Point2 = mat.texCoords.Point2,
                        },
                        material = mat.textureID,
                        color = mat.bbColor,
                    };
                    var bbR = new TriangleBillboardData
                    {
                        blendType = BlendTypeEnum.PostPP,
                        positions = new TriangleD
                        {
                            Point0 = quad.Point0,
                            Point1 = quad.Point2,
                            Point2 = quad.Point3,
                        },
                        texCoords = new Triangle
                        {
                            Point0 = mat.texCoords.Point0,
                            Point1 = mat.texCoords.Point2,
                            Point2 = mat.texCoords.Point3,
                        },
                        material = mat.textureID,
                        color = mat.bbColor,
                    };

                    bbDataBack.Add(bbL);
                    bbDataBack.Add(bbR);

                    if (indexL >= bbPool.Count)
                        bbPool.Add(GetNewBB());
                    if (indexR >= bbPool.Count)
                        bbPool.Add(GetNewBB());

                    MyTransparentGeometry.AddBillboard(bbPool[indexL], false);
                    MyTransparentGeometry.AddBillboard(bbPool[indexR], false);
                }

                public static void AddQuad(ref BoundedQuadMaterial mat, ref MyQuadD quad)
                {
                    var bbPool = instance.bbPool;
                    var bbDataBack = instance.bbDataBack;
                    int indexL = bbDataBack.Count,
                        indexR = bbDataBack.Count + 1;

                    var bbL = new TriangleBillboardData
                    {
                        blendType = BlendTypeEnum.PostPP,
                        positions = new TriangleD
                        {
                            Point0 = quad.Point0,
                            Point1 = quad.Point1,
                            Point2 = quad.Point2,
                        },
                        texCoords = new Triangle
                        {
                            Point0 = mat.texBounds.Min,
                            Point1 = mat.texBounds.Min + new Vector2(0f, mat.texBounds.Size.Y),
                            Point2 = mat.texBounds.Max,
                        },
                        material = mat.textureID,
                        color = mat.bbColor,
                    };
                    var bbR = new TriangleBillboardData
                    {
                        blendType = BlendTypeEnum.PostPP,
                        positions = new TriangleD
                        {
                            Point0 = quad.Point0,
                            Point1 = quad.Point2,
                            Point2 = quad.Point3,
                        },
                        texCoords = new Triangle
                        {
                            Point0 = mat.texBounds.Min,
                            Point1 = mat.texBounds.Max,
                            Point2 = mat.texBounds.Min + new Vector2(mat.texBounds.Size.X, 0f),
                        },
                        material = mat.textureID,
                        color = mat.bbColor,
                    };
   
                    bbDataBack.Add(bbL);
                    bbDataBack.Add(bbR);

                    if (indexL >= bbPool.Count)
                        bbPool.Add(GetNewBB());
                    if (indexR >= bbPool.Count)
                        bbPool.Add(GetNewBB());

                    MyTransparentGeometry.AddBillboard(bbPool[indexL], false);
                    MyTransparentGeometry.AddBillboard(bbPool[indexR], false);
                }

                private static MyTriangleBillboard GetNewBB()
                {
                    var bb = new MyTriangleBillboard();
                    bb.BlendType = BlendTypeEnum.PostPP;
                    bb.Position0 = Vector3D.Zero;
                    bb.Position1 = Vector3D.Zero;
                    bb.Position2 = Vector3D.Zero;
                    bb.UV0 = Vector2.Zero;
                    bb.UV1 = Vector2.Zero;
                    bb.UV2 = Vector2.Zero;
                    bb.Material = Material.Default.TextureID;
                    bb.Color = Vector4.One;
                    bb.ColorIntensity = 1f;
                    bb.CustomViewProjection = -1;

                    return bb;
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