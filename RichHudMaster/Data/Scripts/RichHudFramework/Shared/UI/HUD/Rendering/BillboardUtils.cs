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
                /// <summary>
                /// Shared buffer list for quads.
                /// </summary>
                public static List<QuadBoardData> PostQuadBuffer;

                private static BillBoardUtils instance;
                private const int statsWindowSize = 240, sampleRateDiv = 5,
                    bufMinResizeThreshold = 4000;

                private readonly List<MyTriangleBillboard> bbBuf;
                private readonly List<MyTriangleBillboard>[] bbSwapPools;
                private List<MyTriangleBillboard> bbPoolBack;
                private int currentPool;

                private readonly List<TriangleBillboardData> bbDataList;

                private readonly int[] billboardUsage, billboardAlloc;
                private readonly List<int> billboardUsageStats, billboardAllocStats;
                private readonly Action UpdateBillboardsCallback;
                private int sampleTick, tick;

                private BillBoardUtils() : base(false, true)
                {
                    if (instance != null)
                        throw new Exception($"Only one instance of {GetType().Name} can exist at once.");

                    PostQuadBuffer = new List<QuadBoardData>(1000);
                    bbSwapPools = new List<MyTriangleBillboard>[3]
                    {
                        new List<MyTriangleBillboard>(1000),
                        new List<MyTriangleBillboard>(1000),
                        new List<MyTriangleBillboard>(1000)
                    };
                    bbBuf = new List<MyTriangleBillboard>(1000);
                    bbDataList = new List<TriangleBillboardData>(1000);

                    billboardUsage = new int[statsWindowSize];
                    billboardAlloc = new int[statsWindowSize];

                    billboardUsageStats = new List<int>(statsWindowSize);
                    billboardAllocStats = new List<int>(statsWindowSize);

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
                    PostQuadBuffer = null;
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
                        billboardUsage[sampleTick] = bbDataList.Count;
                        billboardUsageStats.Clear();
                        billboardUsageStats.AddRange(billboardUsage);
                        billboardUsageStats.Sort();

                        billboardAlloc[sampleTick] = bbDataList.Capacity;
                        billboardAllocStats.Clear();
                        billboardAllocStats.AddRange(billboardAlloc);
                        billboardAllocStats.Sort();

                        sampleTick++;
                        sampleTick %= statsWindowSize;

                        int usage99 = GetUsagePercentile(.99f),
                            alloc0 = GetAllocPercentile(0f);

                        if (bbDataList.Capacity > bufMinResizeThreshold
                            && bbDataList.Capacity > 3 * bbDataList.Count
                            && bbDataList.Capacity > 3 * usage99
                            && alloc0 > 3 * usage99)
                        {
                            int max = Math.Max(2 * bbDataList.Count, bufMinResizeThreshold);
                            bbDataList.ClearAndTrim(max);
                            bbBuf.ClearAndTrim(max);
                            PostQuadBuffer.ClearAndTrim(max);

                            foreach (List<MyTriangleBillboard> bb in bbSwapPools)
                            {
                                int remStart = Math.Min(max, bb.Count - 1),
                                    remCount = Math.Max(bb.Count - remStart, 0);

                                bb.RemoveRange(remStart, remCount);
                                bb.TrimExcess();
                            }
                        }
                    }

                    bbDataList.Clear();

                    tick++;
                    tick %= sampleRateDiv;
                }

                private void UpdateBillboards()
                {
                    for (int i = 0; (i < bbDataList.Count && i < bbPoolBack.Count); i++)
                    {
                        MyTriangleBillboard bb = bbPoolBack[i];
                        TriangleBillboardData bbData = bbDataList[i];

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

                /// <summary>
                /// Renders a polygon from a given set of unique vertex coordinates. Triangles are defined by their
                /// indices and the tex coords are parallel to the vertex list.
                /// </summary>
                public static void AddTriangles(List<int> indices, List<Vector3D> vertices, ref PolyMaterial mat)
                {
                    var bbPool = instance.bbPoolBack;
                    var bbDataBack = instance.bbDataList;
                    var bbBuf = instance.bbBuf;

                    int triangleCount = indices.Count / 3,
                        bbRemaining = bbPool.Count - bbDataBack.Count,
                        bbToAdd = Math.Max(triangleCount - bbRemaining, 0);

                    instance.AddNewBB(bbToAdd);

                    for (int i = bbDataBack.Count; i < triangleCount + bbDataBack.Count; i++)
                        bbBuf.Add(bbPool[i]);

                    MyTransparentGeometry.AddBillboards(bbBuf, false);
                    bbBuf.Clear();

                    bbDataBack.EnsureCapacity(bbDataBack.Count + triangleCount);

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
                }

                /// <summary>
                /// Adds a triangles in the given starting index range
                /// </summary>
                public static void AddTriangleRange(Vector2I range, List<int> indices, List<Vector3D> vertices, ref PolyMaterial mat)
                {
                    var bbPool = instance.bbPoolBack;
                    var bbDataBack = instance.bbDataList;
                    var bbBuf = instance.bbBuf;

                    int triangleCount = indices.Count / 3,
                        bbRemaining = bbPool.Count - bbDataBack.Count,
                        bbToAdd = Math.Max(triangleCount - bbRemaining, 0);

                    instance.AddNewBB(bbToAdd);

                    for (int i = bbDataBack.Count; i < triangleCount + bbDataBack.Count; i++)
                        bbBuf.Add(bbPool[i]);

                    MyTransparentGeometry.AddBillboards(bbBuf, false);
                    bbBuf.Clear();

                    bbDataBack.EnsureCapacity(bbDataBack.Count + triangleCount);

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
                }

                /// <summary>
                /// Renders a polygon from a given set of unique vertex coordinates. Triangles are defined by their
                /// indices.
                /// </summary>
                public static void AddTriangles(List<int> indices, List<Vector3D> vertices, ref TriMaterial mat)
                {
                    var bbPool = instance.bbPoolBack;
                    var bbDataBack = instance.bbDataList;
                    var bbBuf = instance.bbBuf;

                    int triangleCount = indices.Count / 3, 
                        bbRemaining = bbPool.Count - bbDataBack.Count,
                        bbToAdd = Math.Max(triangleCount - bbRemaining, 0);

                    instance.AddNewBB(bbToAdd);

                    for (int i = bbDataBack.Count; i < triangleCount + bbDataBack.Count; i++)
                        bbBuf.Add(bbPool[i]);

                    MyTransparentGeometry.AddBillboards(bbBuf, false);
                    bbBuf.Clear();

                    bbDataBack.EnsureCapacity(bbDataBack.Count + triangleCount);

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
                }

                /// <summary>
                /// Adds a list of textured quads in one batch using QuadBoard data
                /// </summary>
                public static void AddQuads(List<QuadBoardData> quads)
                {
                    var bbPool = instance.bbPoolBack;
                    var bbDataBack = instance.bbDataList;
                    var bbBuf = instance.bbBuf;

                    int triangleCount = quads.Count * 2,
                        bbRemaining = bbPool.Count - bbDataBack.Count,
                        bbToAdd = Math.Max(triangleCount - bbRemaining, 0);

                    instance.AddNewBB(bbToAdd);

                    for (int i = bbDataBack.Count; i < triangleCount + bbDataBack.Count; i++)
                        bbBuf.Add(bbPool[i]);

                    MyTransparentGeometry.AddBillboards(bbBuf, false);
                    bbBuf.Clear();

                    bbDataBack.EnsureCapacity(bbDataBack.Count + triangleCount);

                    for (int i = 0; i < quads.Count; i++)
                    {
                        QuadBoardData quadBoard = quads[i];
                        MyQuadD quad = quadBoard.positions;
                        BoundedQuadMaterial mat = quadBoard.material;

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
                    }
                }

                /// <summary>
                /// Adds a triangle starting at the given index.
                /// </summary>
                public static void AddTriangle(int start, List<int> indices, List<Vector3D> vertices, ref TriMaterial mat)
                {
                    var bbPool = instance.bbPoolBack;
                    var bbDataBack = instance.bbDataList;
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
                        instance.AddNewBB(index - (bbPool.Count - 1));

                    MyTransparentGeometry.AddBillboard(bbPool[index], false);
                }

                public static void AddTriangle(ref TriMaterial mat, ref TriangleD triangle)
                {
                    var bbPool = instance.bbPoolBack;
                    var bbDataBack = instance.bbDataList;
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
                        instance.AddNewBB(index - (bbPool.Count - 1));

                    MyTransparentGeometry.AddBillboard(bbPool[index], false);
                }

                public static void AddQuad(ref QuadMaterial mat, ref MyQuadD quad)
                {
                    var bbPool = instance.bbPoolBack;
                    var bbDataBack = instance.bbDataList;
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

                    if (indexR >= bbPool.Count)
                        instance.AddNewBB(indexR - (bbPool.Count - 1));

                    MyTransparentGeometry.AddBillboard(bbPool[indexL], false);
                    MyTransparentGeometry.AddBillboard(bbPool[indexR], false);
                }

                public static void AddQuad(ref BoundedQuadMaterial mat, ref MyQuadD quad)
                {
                    var bbPool = instance.bbPoolBack;
                    var bbDataBack = instance.bbDataList;
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

                    if (indexR >= bbPool.Count)
                        instance.AddNewBB(indexR - (bbPool.Count - 1));

                    MyTransparentGeometry.AddBillboard(bbPool[indexL], false);
                    MyTransparentGeometry.AddBillboard(bbPool[indexR], false);
                }

                private void AddNewBB(int count)
                {
                    bbPoolBack.EnsureCapacity(bbPoolBack.Count + count);

                    for (int i = 0; i < count; i++)
                    {
                        bbPoolBack.Add(new MyTriangleBillboard
                        {
                            BlendType = BlendTypeEnum.PostPP,
                            Position0 = Vector3D.Zero,
                            Position1 = Vector3D.Zero,
                            Position2 = Vector3D.Zero,
                            UV0 = Vector2.Zero,
                            UV1 = Vector2.Zero,
                            UV2 = Vector2.Zero,
                            Material = Material.Default.TextureID,
                            Color = Vector4.One,
                            DistanceSquared = float.PositiveInfinity,
                            ColorIntensity = 1f,
                            CustomViewProjection = -1
                        });
                    }
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