using RichHudFramework.UI.Rendering.Server;
using Sandbox.ModAPI;
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
	using Internal;
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
	using HudNodeData = MyTuple<
		uint[], // 1 - Config { 1.0 - State, 1.1 - NodeVisibleMask, 1.2 - NodeInputMask, 1.3 - zOffset, 1.4 - zOffsetInner, 1.5 - fullZOffset }
		Func<Vector3D>[],  // 2 - GetNodeOriginFunc
		HudNodeHookData, // 3 - Main hooks
		object, // 4 - Parent as HudNodeDataHandle
		List<object>, // 5 - Children as IReadOnlyList<HudNodeDataHandle>
		object // 6 - Unused
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
		using TextBoardMembers = MyTuple<
			TextBuilderMembers,
			FloatProp, // Scale
			Func<Vector2>, // Size
			Func<Vector2>, // TextSize
			Vec2Prop, // FixedSize
			Action<BoundingBox2, BoundingBox2, MatrixD[]> // Draw 
		>;

		namespace Server
		{
			using static RichHudFramework.Server.RichHudMaster;
			using HudClientMembers = MyTuple<
				CursorMembers, // Cursor
				Func<TextBoardMembers>, // GetNewTextBoard
				ApiMemberAccessor, // GetOrSetMembers
				Action // Unregister
			>;
			// Read-only length-1 array containing raw UI node data
			using HudNodeDataHandle = IReadOnlyList<HudNodeData>;

			public sealed partial class HudMain
			{
				public partial class TreeClient
				{
					/// <summary>
					/// Read only list of accessor list for UI elements registered to this client to be 
					/// added to the tree in the next update.
					/// </summary>
					public IReadOnlyList<FlatSubtree> InactiveNodeData { get; private set; }

					/// <summary>
					/// Number of elements registered to the client
					/// </summary>
					public int ElementsUpdating { get; private set; }

					/// <summary>
					/// Number of subtrees registered to the client
					/// </summary>
					public int SubtreesUpdating { get; private set; }

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

					/// <summary>
					/// Mod client reference that this TreeClient belongs to
					/// </summary>
					public Action<Exception> ReportExceptionFunc { get; private set; }

					private bool _enableCursor;
					private readonly List<FlatSubtree> subtreeBuffers;

					// Deprecated
					private bool refreshRequested, refreshDrawList;

					public TreeClient(ModClient modClient = null, int apiVersion = (int)APIVersionTable.Latest)
					{
						this.ReportExceptionFunc = modClient?.ReportException ?? ExceptionHandler.ReportException;
						this.ApiVersion = apiVersion;

						subtreeBuffers = new List<FlatSubtree>();
						InactiveNodeData = subtreeBuffers;

						Registered = TreeManager.RegisterClient(this);
					}

					public void Update(HudNodeIterator nodeIterator, ObjectPool<FlatSubtree> bufferPool, int tick, int clientID)
					{
						if (refreshDrawList || ApiVersion > (int)APIVersionTable.Version1Base)
							refreshRequested = true;

						if (refreshRequested && (tick % treeRefreshRate) == 0)
						{
							bufferPool.ReturnRange(subtreeBuffers);
							subtreeBuffers.Clear();

							if (ApiVersion >= (int)APIVersionTable.HudNodeHandleSupport)
							{
								if (RootNodeHandle != null)
									ElementsUpdating = nodeIterator.GetNodeData(RootNodeHandle, subtreeBuffers, bufferPool, clientID);
							}
							else if (GetUpdateAccessors != null)
								ElementsUpdating = LegacyNodeUpdate(bufferPool, clientID);

							SubtreesUpdating = subtreeBuffers.Count;
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

						return GetOrSetMemberDeprecated(data, memberEnum);
					}
				}
			}
		}
	}
}
