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

					private void LegacyNodeUpdate(int clientID)
					{
						if (convBuffer == null)
							convBuffer = new List<HudUpdateAccessorsOld>();

						convBuffer.Clear();
						GetUpdateAccessors?.Invoke(convBuffer, 0);

						foreach (var src in convBuffer)
						{
							flatTreeBuffer.StateData.Add(new NodeState 
							{
								ClientID = clientID
							});
							flatTreeBuffer.DepthData.Add(new NodeDepthData
							{
								GetPosFunc = src.Item2.Item2,
								ZOffset = src.Item2.Item1()
							});
							flatTreeBuffer.HookData.Add(new HudNodeHookData 
							{
								Item1 = src.Item1,  // 1 - GetOrSetApiMemberFunc
								Item2 = src.Item3,  // 2 - InputDepthAction
								Item3 = src.Item4,  // 3 - InputAction
								Item4 = null,       // 4 - SizingAction
								Item5 = src.Item5,  // 5 - LayoutAction
								Item6 = src.Item6   // 6 - DrawAction
							});
						}

						if (convBuffer.Capacity > convBuffer.Count * 10)
							convBuffer.TrimExcess();

						refreshRequested = false;
						refreshDrawList = false;

						if (ApiVersion <= (int)APIVersionTable.Version1Base)
							TreeManager.RefreshRequested = true;
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
