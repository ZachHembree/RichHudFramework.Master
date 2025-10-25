using RichHudFramework.UI.Rendering.Server;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage;
using VRageMath;
using ApiMemberAccessor = System.Func<object, int, object>;
using FloatProp = VRage.MyTuple<System.Func<float>, System.Action<float>>;
using HudSpaceDelegate = System.Func<VRage.MyTuple<bool, float, VRageMath.MatrixD>>;
using RichStringMembers = VRage.MyTuple<System.Text.StringBuilder, VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>>;
using Vec2Prop = VRage.MyTuple<System.Func<VRageMath.Vector2>, System.Action<VRageMath.Vector2>>;
using HudNodeHookData = VRage.MyTuple<
	System.Func<object, int, object>, // 1 -  GetOrSetApiMemberFunc
	System.Action, // 2 - InputDepthAction
	System.Action, // 3 - InputAction
	System.Action, // 4 - SizingAction
	System.Action<bool>, // 5 - LayoutAction
	System.Action // 6 - DrawAction
>;
using HudNodeStateData = VRage.MyTuple<
	uint[], // 1 - State
	uint[], // 2 - NodeVisibleMask
	uint[], // 3 - NodeInputMask
	System.Func<VRageMath.Vector3D>[],  // 4 - GetNodeOriginFunc
	int[] // 5 - { 5.0 - zOffset, 5.1 - zOffsetInner, 5.2 - fullZOffset }
>;
using HudSpaceOriginFunc = System.Func<VRageMath.Vector3D>;

namespace RichHudFramework
{
	using Server;
	using VRage.Utils;
	using CursorMembers = MyTuple<
		Func<HudSpaceDelegate, bool>, // IsCapturingSpace
		Func<float, HudSpaceDelegate, bool>, // TryCaptureHudSpace
		Func<ApiMemberAccessor, bool>, // IsCapturing
		Func<ApiMemberAccessor, bool>, // TryCapture
		Func<ApiMemberAccessor, bool>, // TryRelease
		ApiMemberAccessor // GetOrSetMember
	>;
	using TextBuilderMembers = MyTuple<
		MyTuple<Func<int, int, object>, Func<int>>, // GetLineMember, GetLineCount
		Func<Vector2I, int, object>, // GetCharMember
		ApiMemberAccessor, // GetOrSetMember
		Action<IList<RichStringMembers>, Vector2I>, // Insert
		Action<IList<RichStringMembers>>, // SetText
		Action // Clear
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
	using HudNodeData = MyTuple<
		HudNodeStateData, // 1 - { 1.1 - State, 1.2 - NodeVisibleMask, 1.3 - NodeInputMask, 1.4 - GetNodeOriginFunc, 1.5 - ZOffsets }
		HudNodeHookData, // 2 - Main hooks
		object, // 3 - Parent as HudNodeDataHandle
		List<object>, // 4 - Children as IReadOnlyList<HudNodeDataHandle>
		object // 5 - Unused
	>;

	namespace UI
	{
		using TextBoardMembers = MyTuple<
			TextBuilderMembers,
			FloatProp, // Scale
			Func<Vector2>, // Size
			Func<Vector2>, // TextSize
			Vec2Prop, // FixedSize
			Action<BoundingBox2, BoundingBox2, MatrixD[]> // Draw 
		>;
		using TextBoardMembers8 = MyTuple<
			TextBuilderMembers,
			FloatProp, // Scale
			Func<Vector2>, // Size
			Func<Vector2>, // TextSize
			Vec2Prop, // FixedSize
			Action<Vector2, MatrixD> // Draw 
		>;

		// Read-only length-1 array containing raw UI node data
		using HudNodeDataHandle = IReadOnlyList<HudNodeData>;

		namespace Server
		{
			using HudClientMembers = MyTuple<
				CursorMembers, // Cursor
				Func<TextBoardMembers>, // GetNewTextBoard
				ApiMemberAccessor, // GetOrSetMembers
				Action // Unregister
			>;
			using HudClientMembers8 = MyTuple<
				CursorMembers, // Cursor
				Func<TextBoardMembers8>, // GetNewTextBoard
				ApiMemberAccessor, // GetOrSetMembers
				Action // Unregister
			>;
			// Read-only length-1 array containing raw UI node data
			using HudNodeDataHandle = IReadOnlyList<HudNodeData>;

			public sealed partial class HudMain
			{
				public class TreeClient
				{
					/// <summary>
					/// Read only list of accessor list for UI elements registered to this client
					/// </summary>
					public IReadOnlyList<TreeNodeData> UpdateAccessors => activeUpdateBuffer;

					/// <summary>
					/// Returns true if the client has been registered to the TreeManager
					/// </summary>
					public bool Registered { get; private set; }

					/// <summary>
					/// If true, then the client is requesting that the cursor be enabled
					/// </summary>
					public bool EnableCursor
					{
						get { return _enableCursor && (ApiVersion > (int)APIVersionTable.InputModeSupport || MyAPIGateway.Gui.ChatEntryVisible); }
						set { _enableCursor = value; }
					}

					/// <summary>
					/// Optional callback invoked before draw
					/// </summary>
					public Action BeforeDrawCallback { get; private set; }

					/// <summary>
					/// Optional callback invoked after draw
					/// </summary>
					public Action AfterDrawCallback { get; private set; }

					/// <summary>
					/// Optional callback invoked before handle input
					/// </summary>
					public Action BeforeInputCallback { get; private set; }

					/// <summary>
					/// Optional callback invoked after handle input
					/// </summary>
					public Action AfterInputCallback { get; private set; }

					/// <summary>
					/// RHF version ID
					/// </summary>
					public int ApiVersion { get; private set; }

					/// <summary>
					/// Handle to client root node
					/// </summary>
					public HudNodeDataHandle RootNodeHandle;

					private bool _enableCursor, updatePending;
					private List<TreeNodeData> activeUpdateBuffer,
						inactiveUpdateBuffer;

					// Legacy data
					private Action<List<HudUpdateAccessorsOld>, byte> GetUpdateAccessors;
					private readonly List<HudUpdateAccessorsOld> convBuffer;
					private bool refreshRequested, refreshDrawList;

					public TreeClient(int apiVersion = (int)APIVersionTable.Latest)
					{
						this.ApiVersion = apiVersion;

						activeUpdateBuffer = new List<TreeNodeData>(200);
						inactiveUpdateBuffer = new List<TreeNodeData>(200);
						convBuffer = new List<HudUpdateAccessorsOld>();

						Registered = TreeManager.RegisterClient(this);
					}

					public void Update(HudNodeIterator nodeIterator, int tick)
					{
						if (refreshDrawList || ApiVersion > (int)APIVersionTable.Version1Base)
							refreshRequested = true;

						if (refreshRequested && (tick % treeRefreshRate) == 0)
						{
							inactiveUpdateBuffer.Clear();

							if (ApiVersion >= (int)APIVersionTable.HudNodeHandleSupport)
							{
								if (RootNodeHandle != null)
									nodeIterator.GetNodeData(RootNodeHandle, inactiveUpdateBuffer);
							}
							else if (GetUpdateAccessors != null)
								LegacyNodeUpdate();

							if (inactiveUpdateBuffer.Capacity > inactiveUpdateBuffer.Count * 10)
								inactiveUpdateBuffer.TrimExcess();

							updatePending = true;
						}
					}

					private void LegacyNodeUpdate()
					{
						convBuffer.Clear();
						GetUpdateAccessors?.Invoke(convBuffer, 0);
						
						foreach (var src in convBuffer)
						{
							inactiveUpdateBuffer.Add(new TreeNodeData
							{
								Hooks = new HudNodeHookData 
								{
									Item1 = src.Item1,	// 1 - GetOrSetApiMemberFunc
									Item2 = src.Item3,	// 2 - InputDepthAction
									Item3 = src.Item4,	// 3 - InputAction
									Item4 = null,		// 4 - SizingAction
									Item5 = src.Item5,	// 5 - LayoutAction
									Item6 = src.Item6	// 6 - DrawAction
								},
								GetPosFunc = src.Item2.Item2,
								ZOffset = src.Item2.Item1()
							});
						}

						if (convBuffer.Capacity > convBuffer.Count * 10)
							convBuffer.TrimExcess();

						refreshRequested = false;
						refreshDrawList = false;

						if (ApiVersion <= (int)APIVersionTable.Version1Base)
							TreeManager.RefreshRequested = true;
					}

					public void FinishUpdate()
					{
						if (updatePending)
						{
							MyUtils.Swap(ref activeUpdateBuffer, ref inactiveUpdateBuffer);
							updatePending = false;
						}
					}

					/// <summary>
					/// Unregisters the client from HudMain
					/// </summary>
					public void Unregister()
					{
						Registered = !TreeManager.UnregisterClient(this);
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

					/// <summary>
					/// Retrieves API accessor delegates
					/// </summary>
					public HudClientMembers GetApiData()
					{
						Init();

						return new HudClientMembers()
						{
							Item1 = instance._cursor.GetApiData(),
							Item2 = () => new TextBoard().GetApiData(),
							Item3 = GetOrSetMember,
							Item4 = Unregister
						};
					}

					/// <summary>
					/// Provides access to HudMain properties via RHF API
					/// </summary>
					private object GetOrSetMember(object data, int memberEnum)
					{
						switch ((HudMainAccessors)memberEnum)
						{
							case HudMainAccessors.ScreenWidth:
								return ScreenWidth;
							case HudMainAccessors.ScreenHeight:
								return ScreenHeight;
							case HudMainAccessors.AspectRatio:
								return AspectRatio;
							case HudMainAccessors.ResScale:
								return ResScale;
							case HudMainAccessors.Fov:
								return Fov;
							case HudMainAccessors.FovScale:
								return FovScale;
							case HudMainAccessors.PixelToWorldTransform:
								return PixelToWorld;
							case HudMainAccessors.UiBkOpacity:
								return UiBkOpacity;
							case HudMainAccessors.ClipBoard:
								{
									if (data == null)
										return ClipBoard.apiData;
									else
										ClipBoard = new RichText(data as List<RichStringMembers>);
									break;
								}
							case HudMainAccessors.EnableCursor:
								{
									if (data == null)
										return _enableCursor;
									else
										_enableCursor = (bool)data;
									break;
								}
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
							case HudMainAccessors.GetFocusOffset:
								return GetFocusOffset(data as Action<byte>);
							case HudMainAccessors.GetPixelSpaceFunc:
								return instance._root.GetHudSpaceFunc;
							case HudMainAccessors.GetPixelSpaceOriginFunc:
								return instance._root.GetNodeOriginFunc;
							case HudMainAccessors.GetInputFocus:
								GetInputFocus(data as Action); break;
							case HudMainAccessors.InputMode:
								return InputMode;
							case HudMainAccessors.SetBeforeDrawCallback:
								BeforeDrawCallback = data as Action; break;
							case HudMainAccessors.SetAfterDrawCallback:
								AfterDrawCallback = data as Action; break;
							case HudMainAccessors.SetBeforeInputCallback:
								BeforeInputCallback = data as Action; break;
							case HudMainAccessors.SetAfterInputCallback:
								AfterInputCallback = data as Action; break;
							case HudMainAccessors.ClientRootNode:
								RootNodeHandle = data as HudNodeDataHandle; break;
						}

						return null;
					}
				}
			}
		}
	}
}
