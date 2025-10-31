using RichHudFramework.UI.Rendering.Server;
using System;
using System.Collections.Generic;
using VRage;
using VRageMath;
using ApiMemberAccessor = System.Func<object, int, object>;
using FloatProp = VRage.MyTuple<System.Func<float>, System.Action<float>>;
using HudNodeHookData = VRage.MyTuple<
	System.Func<object, int, object>, // 1 -  GetOrSetApiMemberFunc
	System.Action, // 2 - InputDepthAction
	System.Action, // 3 - InputAction
	System.Action, // 4 - SizingAction
	System.Action<bool>, // 5 - LayoutAction
	System.Action // 6 - DrawAction
>;
using HudSpaceDelegate = System.Func<VRage.MyTuple<bool, float, VRageMath.MatrixD>>;
using RichStringMembers = VRage.MyTuple<System.Text.StringBuilder, VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>>;
using Vec2Prop = VRage.MyTuple<System.Func<VRageMath.Vector2>, System.Action<VRageMath.Vector2>>;

namespace RichHudFramework
{
	using Server;
	using CursorMembers = MyTuple<
		Func<HudSpaceDelegate, bool>, // IsCapturingSpace
		Func<float, HudSpaceDelegate, bool>, // TryCaptureHudSpace
		Func<ApiMemberAccessor, bool>, // IsCapturing
		Func<ApiMemberAccessor, bool>, // TryCapture
		Func<ApiMemberAccessor, bool>, // TryRelease
		ApiMemberAccessor // GetOrSetMember
	>;
	// Legacy UI node data
	using HudUpdateAccessorsOld = MyTuple<
		ApiMemberAccessor,
		MyTuple<Func<ushort>, Func<Vector3D>>, // ZOffset + GetOrigin
		Action, // DepthTest
		Action, // HandleInput
		Action<bool>, // BeforeLayout
		Action // BeforeDraw
	>;
	using TextBuilderMembers = MyTuple<
		MyTuple<Func<int, int, object>, Func<int>>, // GetLineMember, GetLineCount
		Func<Vector2I, int, object>, // GetCharMember
		ApiMemberAccessor, // GetOrSetMember
		Action<IList<RichStringMembers>, Vector2I>, // Insert
		Action<IList<RichStringMembers>>, // SetText
		Action // Clear
	>;

	namespace UI
	{
		using TextBoardMembers8 = MyTuple<
			TextBuilderMembers,
			FloatProp, // Scale
			Func<Vector2>, // Size
			Func<Vector2>, // TextSize
			Vec2Prop, // FixedSize
			Action<Vector2, MatrixD> // Draw 
		>;

		namespace Server
		{
			using HudClientMembers8 = MyTuple<
				CursorMembers, // Cursor
				Func<TextBoardMembers8>, // GetNewTextBoard
				ApiMemberAccessor, // GetOrSetMembers
				Action // Unregister
			>;

			public sealed partial class HudMain
			{
				public partial class TreeClient
				{
					// Legacy data
					private Action<List<HudUpdateAccessorsOld>, byte> GetUpdateAccessors;
					private List<HudUpdateAccessorsOld> convBuffer;
					private bool refreshDrawList;

					private int LegacyNodeUpdate(ObjectPool<FlatSubtree> bufferPool)
					{
						if (refreshDrawList || ApiVersion > (int)APIVersionTable.Version1Base)
						{
							if (convBuffer == null)
								convBuffer = new List<HudUpdateAccessorsOld>();

							convBuffer.Clear();
							GetUpdateAccessors?.Invoke(convBuffer, 0);

							if (convBuffer.Count == 0)
								return 0;

							GetLegacyNodeData(convBuffer, subtreeBuffers, bufferPool, this);

							if (convBuffer.Capacity > convBuffer.Count * 10)
								convBuffer.TrimExcess();

							refreshDrawList = false;

							if (ApiVersion <= (int)APIVersionTable.Version1Base)
								TreeManager.RefreshRequested = true;
						}

						return convBuffer.Count;
					}

					private static void GetLegacyNodeData(List<HudUpdateAccessorsOld> srcBuffer, List<FlatSubtree> dst,
						ObjectPool<FlatSubtree> bufferPool, TreeClient owner)
					{
						FlatSubtree subtree = null;

						// Subtree detection
						Func<Vector3D> lastGetOriginFunc = null;
						byte lastInnerOffset = 0;
						// Subtree position
						int subtreeCount = 0;
						int subtreePos = 0;
						// Set true if the contents of the subtree match the current node structure
						bool canBeEqual = false;

						for (int i = 0; i < srcBuffer.Count; i++)
						{
							var src = srcBuffer[i];
							Func<ushort> GetLayerFuncOld = src.Item2.Item1;
							Func<Vector3D> GetOriginFunc = src.Item2.Item2;
							ushort FullZOffset = GetLayerFuncOld();
							byte innerOffset = (byte)(FullZOffset >> 8);

							// Check if a new subtree needs to be started
							if (innerOffset != lastInnerOffset || lastGetOriginFunc != GetOriginFunc)
							{
								// Finalize previous subtree
								if (subtree != null)
								{
									if (subtree.Inactive.OuterOffsets.Count > subtreePos)
									{
										subtree.TruncateInactive(subtreePos);
										canBeEqual = false;
									}

									if (!canBeEqual)
										subtree.IsActiveStale = true;
								}

								// Use an existing buffer if available
								if (subtreeCount < dst.Count)
									subtree = dst[subtreeCount];
								else
								{
									subtree = bufferPool.Get();
									dst.Add(subtree);
								}

								// If the subtree was already using these values, it may be unchanged
								canBeEqual = (GetLayerFuncOld == subtree.GetLayerFuncOld && GetOriginFunc == subtree.GetOriginFunc);

								subtree.Owner = owner;
								subtree.GetLayerFuncOld = GetLayerFuncOld;
								subtree.GetOriginFunc = GetOriginFunc;

								lastInnerOffset = innerOffset;
								lastGetOriginFunc = GetOriginFunc;
								subtreePos = 0;
								subtreeCount++;
							}

							if (subtreePos < subtree.Inactive.OuterOffsets.Count)
							{
								if (canBeEqual)
								{
									byte lastOuterOffset = subtree.Inactive.OuterOffsets[subtreePos];
									byte outerOffset = (byte)FullZOffset;

									// If the GetOrSetApiMemberFunc is the same as the last, it's the same node
									canBeEqual &= subtree.Inactive.HookData[subtreePos].Item1 == src.Item1;
									// If the outer/public sorting has changed, the members will need to be resorted
									canBeEqual &= outerOffset == lastOuterOffset;
								}

								// If the tree members are unchanged and don't require resorting, this is 
								// unnecessary.
								if (!canBeEqual)
								{
									subtree.Inactive.OuterOffsets[subtreePos] = (byte)FullZOffset;
									subtree.Inactive.HookData[subtreePos] = new HudNodeHookData
									{
										Item1 = src.Item1,  // 1 - GetOrSetApiMemberFunc
										Item2 = src.Item3,  // 2 - InputDepthAction
										Item3 = src.Item4,  // 3 - InputAction
										Item4 = null,       // 4 - SizingAction
										Item5 = src.Item5,  // 5 - LayoutAction
										Item6 = src.Item6   // 6 - DrawAction
									};
								}
							}
							else
							{
								// If there were new additions, it can't be equal
								canBeEqual = false;

								// Unused node state list needs to be padded to remain parallel
								subtree.Inactive.OuterOffsets.Add((byte)FullZOffset);
								subtree.Inactive.HookData.Add(new HudNodeHookData
								{
									Item1 = src.Item1,  // 1 - GetOrSetApiMemberFunc
									Item2 = src.Item3,  // 2 - InputDepthAction
									Item3 = src.Item4,  // 3 - InputAction
									Item4 = null,       // 4 - SizingAction
									Item5 = src.Item5,  // 5 - LayoutAction
									Item6 = src.Item6   // 6 - DrawAction
								});
							}

							subtreePos++;
						}

						// Finalize trailing subtree
						if (subtree != null)
						{
							if (subtree.Inactive.OuterOffsets.Count > subtreePos)
							{
								subtree.TruncateInactive(subtreePos);
								canBeEqual = false;
							}

							if (!canBeEqual)
								subtree.IsActiveStale = true;
						}

						// Trim and return unused buffers
						if (subtreeCount < dst.Count)
						{
							int start = subtreeCount, length = dst.Count - start;
							bufferPool.ReturnRange(dst, start, length);
							dst.RemoveRange(start, length);
						}
					}

					/// <summary>
					/// Retrieves API accessor delegates
					/// </summary>
					public HudClientMembers8 GetApiData8()
					{
						Init();

						return new HudClientMembers8()
						{
							Item1 = instance._cursor.GetApiData8(),
							Item2 = () => new TextBoard().GetApiData8(),
							Item3 = GetOrSetMember,
							Item4 = Unregister
						};
					}

					private object GetOrSetMemberDeprecated(object data, int memberEnum)
					{
						switch ((HudMainAccessors)memberEnum)
						{
							// Deprecated
							case HudMainAccessors.RefreshDrawList:
								{
									if (data == null)
										return refreshDrawList;
									else
										refreshDrawList = (bool)data;
									break;
								}
							// Deprecated
							case HudMainAccessors.GetUpdateAccessorsOld:
								{
									if (data == null)
										return GetUpdateAccessors;
									else
									{
										GetUpdateAccessors = data as Action<List<HudUpdateAccessorsOld>, byte>;
										refreshDrawList = true;
									}
									break;
								}
						}

						return null;
					}
				}
			}
		}
	}
}
