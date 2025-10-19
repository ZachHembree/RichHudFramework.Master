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
				public class TreeClient
				{
					/// <summary>
					/// Delegate used to retrieve UI update delegates from clients
					/// </summary>
					public Action<List<HudUpdateAccessors>, byte> GetUpdateAccessors;

					/// <summary>
					/// Read only list of accessor list for UI elements registered to this client
					/// </summary>
					public IReadOnlyList<HudUpdateAccessors> UpdateAccessors => activeUpdateBuffer;

					/// <summary>
					/// Returns true if the client has been registered to the TreeManager
					/// </summary>
					public bool Registered { get; private set; }

					/// <summary>
					/// If true, then the client is requesting that the cursor be enabled
					/// </summary>
					public bool EnableCursor
					{
						get { return _enableCursor && (apiVersion > 8 || MyAPIGateway.Gui.ChatEntryVisible); }
						set { _enableCursor = value; }
					}

					/// <summary>
					/// If true, then the client is requesting that the draw list be rebuilt
					/// </summary>
					public bool RefreshDrawList => _refreshDrawList;

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

					private bool _refreshDrawList, _enableCursor, refreshRequested, updatePending;
					private readonly int apiVersion;
					private List<HudUpdateAccessors> activeUpdateBuffer,
						inactiveUpdateBuffer;

					public TreeClient(int apiVersion = RichHudMaster.apiVID)
					{
						this.apiVersion = apiVersion;

						activeUpdateBuffer = new List<HudUpdateAccessors>(200);
						inactiveUpdateBuffer = new List<HudUpdateAccessors>(200);
						Registered = TreeManager.RegisterClient(this);
					}

					public void Update(int tick)
					{
						if (RefreshDrawList || apiVersion > 7)
							refreshRequested = true;

						if (refreshRequested && (tick % treeRefreshRate) == 0)
						{
							inactiveUpdateBuffer.Clear();
							GetUpdateAccessors?.Invoke(inactiveUpdateBuffer, 0);

							if (inactiveUpdateBuffer.Capacity > inactiveUpdateBuffer.Count * 2)
								inactiveUpdateBuffer.TrimExcess();

							refreshRequested = false;
							_refreshDrawList = false;

							if (apiVersion <= 7)
								TreeManager.RefreshRequested = true;

							updatePending = true;
						}
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
							case HudMainAccessors.RefreshDrawList:
								{
									if (data == null)
										return RefreshDrawList;
									else
										_refreshDrawList = (bool)data;
									break;
								}
							case HudMainAccessors.GetUpdateAccessors:
								{
									if (data == null)
										return GetUpdateAccessors;
									else
									{
										GetUpdateAccessors = data as Action<List<HudUpdateAccessors>, byte>;
										_refreshDrawList = true;
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
						}

						return null;
					}
				}
			}
		}
	}
}
