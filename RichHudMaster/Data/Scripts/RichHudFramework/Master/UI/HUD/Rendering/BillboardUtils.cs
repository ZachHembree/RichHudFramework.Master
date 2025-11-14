using RichHudFramework.Internal;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage;
using VRage.Game;
using VRage.Utils;
using VRageMath;
using VRageRender;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace RichHudFramework
{
	namespace UI
	{
		using ApiMemberAccessor = System.Func<object, int, object>;
		using FlatTriangleBillboardData = MyTuple<
			BlendTypeEnum, // blendType
			Vector2I, // bbID + matrixID
			MyStringId, // material
			MyTuple<Vector4, BoundingBox2?>, // color + mask
			MyTuple<Vector2, Vector2, Vector2>, // texCoords
			MyTuple<Vector2, Vector2, Vector2> // flat pos
		>;
		using TriangleBillboardData = MyTuple<
			BlendTypeEnum, // blendType
			Vector2I, // bbID + matrixID
			MyStringId, // material
			Vector4, // color
			MyTuple<Vector2, Vector2, Vector2>, // texCoords
			MyTuple<Vector3D, Vector3D, Vector3D> // vertexPos
		>;

		namespace Rendering
		{
			// Returned in IReadOnlyList<BbUtilData> of length-1
			using BbUtilData = MyTuple<
				ApiMemberAccessor, // GetOrSetMember - 1
				List<MyTriangleBillboard>[], // triPoolBack - 2
				List<MyTriangleBillboard>[], // flatTriPoolBack - 3
				List<TriangleBillboardData>, // triangleList - 4
				List<FlatTriangleBillboardData>, // flatTriangleList - 5
				MyTuple<
					List<MatrixD>, // matrixBuf - 6.1
					Dictionary<MatrixD[], int>, // matrixTable - 6.2
					List<MyTriangleBillboard> // bbBuf - 6.3
				>
			>;
			// Boxed in object
			using BbUtilData10 = MyTuple<
				ApiMemberAccessor, // GetOrSetMember
				List<TriangleBillboardData>, // triangleList
				List<FlatTriangleBillboardData>, // flatTriangleList
				List<MatrixD>, // matrixBuf
				Dictionary<MatrixD[], int> // matrixTable
			>;

			public sealed partial class BillBoardUtils : RichHudComponentBase
			{
				private const int statsWindowSize = 240, sampleRateDiv = 5,
					bufMinResizeThreshold = 4000,
					bbBatchSize = 100;

				private static BillBoardUtils instance;

				private class BillboardSwapPool
				{
					/// <summary>
					/// Length-1 array containing the pool that is currently active
					/// </summary>
					public List<MyTriangleBillboard>[] ActivePool { get; }

					private List<MyTriangleBillboard>[] pools;
					private int poolIndex;

					public BillboardSwapPool(int poolCount = 3, int poolInitSize = 100)
					{
						pools = new List<MyTriangleBillboard>[poolCount];

						for (int i = 0; i < poolCount; i++)
							pools[i] = new List<MyTriangleBillboard>(poolInitSize);

						ActivePool = new List<MyTriangleBillboard>[] { null };
						poolIndex = -1;
					}

					/// <summary>
					/// Sets the least recently used list in the pool as the active pool
					/// </summary>
					public void RotatePool()
					{
						poolIndex = (poolIndex + 1) % pools.Length;
						var nextPool = pools[poolIndex];

						if (ActivePool[0] != null)
							nextPool.EnsureCapacity(ActivePool[0].Count);

						ActivePool[0] = pools[poolIndex];
					}

					/// <summary>
					/// Prunes billboard to the given maximum capacity
					/// </summary>
					public void TrimPools(int maxCount)
					{
						foreach (List<MyTriangleBillboard> bb in pools)
						{
							if (bb.Count > 0)
							{
								int remStart = Math.Min(maxCount, bb.Count - 1),
									remCount = Math.Max(bb.Count - remStart, 0);

								bb.RemoveRange(remStart, remCount);
								bb.TrimExcess();
							}
						}
					}
				}

				// Billboards
				private readonly BillboardSwapPool triSwapPool;
				private readonly BillboardSwapPool flatTriSwapPool;

				// Stats
				private readonly int[] billboardUsage, billboardAlloc, matrixUsage;
				private readonly List<int> billboardUsageStats, billboardAllocStats, matrixUsageStats;
				private readonly Action UpdateBillboardsCallback;

				private int sampleTick, tick;

				// Shared data
				// Billboard pools - parallel with corresponding triangle lists
				private readonly List<MyTriangleBillboard>[] triPoolBack;
				private readonly List<MyTriangleBillboard>[] flatTriPoolBack;
				// BB batch copy/scratch buffer
				private readonly List<MyTriangleBillboard> bbBuf; 

				// Intermediate billboard data
				private readonly List<TriangleBillboardData> triangleList;
				private readonly List<FlatTriangleBillboardData> flatTriangleList;
				private readonly List<MatrixD> matrixBuf;
				private readonly Dictionary<MatrixD[], int> matrixTable;

				private readonly BbUtilData[] clientData;

				private BillBoardUtils() : base(false, true)
				{
					if (instance != null)
						throw new Exception($"Only one instance of {nameof(BillBoardUtils)} can exist at once.");

					flatTriSwapPool = new BillboardSwapPool(6, 1000);
					triSwapPool = new BillboardSwapPool(6);
					triPoolBack = triSwapPool.ActivePool;
					flatTriPoolBack = flatTriSwapPool.ActivePool;
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

					clientData = new BbUtilData[1]
					{
						new BbUtilData
						{
							Item1 = GetOrSetMember,
							Item2 = triPoolBack,
							Item3 = flatTriPoolBack,
							Item4 = triangleList,
							Item5 = flatTriangleList,
							Item6 = new MyTuple<List<MatrixD>, Dictionary<MatrixD[], int>, List<MyTriangleBillboard>>
							{
								Item1 = matrixBuf,
								Item2 = matrixTable,
								Item3 = bbBuf
							}
						}
					};
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
					if (ExceptionHandler.Unloading)
					{
						instance = null;
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
				public static IReadOnlyList<BbUtilData> GetApiData()
				{
					return instance?.clientData;
				}

				/// <summary>
				/// Returns data needed to initialize client BB utils
				/// </summary>
				public static BbUtilData10 GetApiData10()
				{
					return new BbUtilData10
					{
						Item1 = GetOrSetMember,
						Item2 = null, // Never worked
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
							return instance.flatTriPoolBack[0];
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

				private void BeginDrawInternal()
				{
					triSwapPool.RotatePool();
					flatTriSwapPool.RotatePool();
				}

				public static void FinishDraw()
				{
					if (instance != null)
					{
						instance.FinishDrawInternal();
					}
				}

				private void FinishDrawInternal()
				{
					MyTransparentGeometry.ApplyActionOnPersistentBillboards(UpdateBillboardsCallback);

					// Update complete, log stats and clean up buffers
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
					bbBuf.Clear();

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
						int max = Math.Max(triangleList.Capacity, flatTriangleList.Capacity);
						bbBuf.ClearAndTrim(max);
						triSwapPool.TrimPools(triangleList.Capacity);
						flatTriSwapPool.TrimPools(flatTriangleList.Capacity);
					}
				}

				private void UpdateBillboards()
				{
					Update3dBillboards();
					UpdateFlatBillboards();
				}

				private void Update3dBillboards()
				{
					int count = Math.Min(triangleList.Count, triPoolBack[0].Count),
						batchCount = MathHelper.CeilToInt(count / (double)bbBatchSize),
						batchStride = Math.Max(
							MathHelper.CeilToInt(500 / (double)bbBatchSize),
							MathHelper.CeilToInt(batchCount / 4d)
						);

					MyAPIGateway.Parallel.For(0, batchCount, i =>
					{
						int strideEnd = Math.Min(count, (i + 1) * bbBatchSize);

						for (int j = i * bbBatchSize; j < strideEnd; j++)
						{
							TriangleBillboardData bbData = triangleList[j];
							MyTriangleBillboard bb = triPoolBack[0][bbData.Item2.X];

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
						}
					}, batchStride);
				}

				private void UpdateFlatBillboards()
				{
					int count = Math.Min(flatTriangleList.Count, flatTriPoolBack[0].Count),
						batchCount = MathHelper.CeilToInt(count / (double)bbBatchSize),
						batchStride = Math.Max(
							MathHelper.CeilToInt(500 / (double)bbBatchSize),
							MathHelper.CeilToInt(batchCount / 4d)
						);

					MyAPIGateway.Parallel.For(0, batchCount, i =>
					{
						int strideEnd = Math.Min(count, (i + 1) * bbBatchSize);

						for (int j = i * bbBatchSize; j < strideEnd; j++)
						{
							FlatTriangleBillboardData bbData = flatTriangleList[j];
							Triangle planePos = new Triangle { Point0 = bbData.Item6.Item1, Point1 = bbData.Item6.Item2, Point2 = bbData.Item6.Item3 },
								texCoords = new Triangle { Point0 = bbData.Item5.Item1, Point1 = bbData.Item5.Item2, Point2 = bbData.Item5.Item3 };
							BoundingBox2? mask = bbData.Item4.Item2;

							// Masking/clipping
							if (mask != null)
							{
								// Full min/max for bounds
								float bbMinX = Math.Min(Math.Min(planePos.Point0.X, planePos.Point1.X), planePos.Point2.X);
								float bbMinY = Math.Min(Math.Min(planePos.Point0.Y, planePos.Point1.Y), planePos.Point2.Y);
								float bbMaxX = Math.Max(Math.Max(planePos.Point0.X, planePos.Point1.X), planePos.Point2.X);
								float bbMaxY = Math.Max(Math.Max(planePos.Point0.Y, planePos.Point1.Y), planePos.Point2.Y);

								// Inline Intersect
								float interMinX = Math.Max(bbMinX, mask.Value.Min.X);
								float interMinY = Math.Max(bbMinY, mask.Value.Min.Y);
								float interMaxX = Math.Min(bbMaxX, mask.Value.Max.X);
								float interMaxY = Math.Min(bbMaxY, mask.Value.Max.Y);

								// Size and pos as floats
								float sizeX = bbMaxX - bbMinX;
								float sizeY = bbMaxY - bbMinY;
								float posX = bbMinX + sizeX * 0.5f;
								float posY = bbMinY + sizeY * 0.5f;

								// Min/Max-based Clamp for planePos points
								planePos.Point0.X = Math.Max(interMinX, Math.Min(interMaxX, planePos.Point0.X));
								planePos.Point0.Y = Math.Max(interMinY, Math.Min(interMaxY, planePos.Point0.Y));
								planePos.Point1.X = Math.Max(interMinX, Math.Min(interMaxX, planePos.Point1.X));
								planePos.Point1.Y = Math.Max(interMinY, Math.Min(interMaxY, planePos.Point1.Y));
								planePos.Point2.X = Math.Max(interMinX, Math.Min(interMaxX, planePos.Point2.X));
								planePos.Point2.Y = Math.Max(interMinY, Math.Min(interMaxY, planePos.Point2.Y));

								if (bbData.Item3 != Material.Default.TextureID)
								{
									// Full min/max for texBounds
									float texMinX = Math.Min(Math.Min(texCoords.Point0.X, texCoords.Point1.X), texCoords.Point2.X);
									float texMinY = Math.Min(Math.Min(texCoords.Point0.Y, texCoords.Point1.Y), texCoords.Point2.Y);
									float texMaxX = Math.Max(Math.Max(texCoords.Point0.X, texCoords.Point1.X), texCoords.Point2.X);
									float texMaxY = Math.Max(Math.Max(texCoords.Point0.Y, texCoords.Point1.Y), texCoords.Point2.Y);

									float clipSizeX = interMaxX - interMinX;
									float clipSizeY = interMaxY - interMinY;

									float invSizeX = 1f / sizeX, invSizeY = 1f / sizeY;
									float clipScaleX = clipSizeX * invSizeX;
									float clipScaleY = clipSizeY * invSizeY;

									float clipOffsetX = ((interMinX + clipSizeX * 0.5f) - posX) * invSizeX;
									float clipOffsetY = ((interMinY + clipSizeY * 0.5f) - posY) * invSizeY;

									float uvScaleX = texMaxX - texMinX;
									float uvScaleY = texMaxY - texMinY;
									float uvOffsetX = texMinX + uvScaleX * 0.5f;
									float uvOffsetY = texMinY + uvScaleY * 0.5f;

									clipOffsetX *= uvScaleX;
									clipOffsetY *= -uvScaleY; // Flip Y

									// Recalculate tex bounds inline
									float newTexMinX = ((texMinX - uvOffsetX) * clipScaleX) + (uvOffsetX + clipOffsetX);
									float newTexMinY = ((texMinY - uvOffsetY) * clipScaleY) + (uvOffsetY + clipOffsetY);
									float newTexMaxX = ((texMaxX - uvOffsetX) * clipScaleX) + (uvOffsetX + clipOffsetX);
									float newTexMaxY = ((texMaxY - uvOffsetY) * clipScaleY) + (uvOffsetY + clipOffsetY);

									// Min/Max-based Clamp for texCoords
									texCoords.Point0.X = Math.Max(newTexMinX, Math.Min(newTexMaxX, texCoords.Point0.X));
									texCoords.Point0.Y = Math.Max(newTexMinY, Math.Min(newTexMaxY, texCoords.Point0.Y));
									texCoords.Point1.X = Math.Max(newTexMinX, Math.Min(newTexMaxX, texCoords.Point1.X));
									texCoords.Point1.Y = Math.Max(newTexMinY, Math.Min(newTexMaxY, texCoords.Point1.Y));
									texCoords.Point2.X = Math.Max(newTexMinX, Math.Min(newTexMaxX, texCoords.Point2.X));
									texCoords.Point2.Y = Math.Max(newTexMinY, Math.Min(newTexMaxY, texCoords.Point2.Y));
								}
							}

							// Transform 2D planar positions into world space
							MatrixD matrix = matrixBuf[bbData.Item2.Y];

							MyTriangleBillboard bb = flatTriPoolBack[0][bbData.Item2.X];
							bb.BlendType = bbData.Item1;
							bb.Position0 = matrix.Translation + (planePos.Point0.X * matrix.Right) + (planePos.Point0.Y * matrix.Up);
							bb.Position1 = matrix.Translation + (planePos.Point1.X * matrix.Right) + (planePos.Point1.Y * matrix.Up);
							bb.Position2 = matrix.Translation + (planePos.Point2.X * matrix.Right) + (planePos.Point2.Y * matrix.Up);
							bb.UV0 = texCoords.Point0;
							bb.UV1 = texCoords.Point1;
							bb.UV2 = texCoords.Point2;
							bb.Material = bbData.Item3;
							bb.Color = bbData.Item4.Item1;
						}
					}, batchStride);
				}

			}
		}
	}
}