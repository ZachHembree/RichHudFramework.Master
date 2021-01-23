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
    using CursorMembers = MyTuple<
        Func<HudSpaceDelegate, bool>, // IsCapturingSpace
        Func<float, HudSpaceDelegate, bool>, // TryCaptureHudSpace
        Func<ApiMemberAccessor, bool>, // IsCapturing
        Func<ApiMemberAccessor, bool>, // TryCapture
        Func<ApiMemberAccessor, bool>, // TryRelease
        ApiMemberAccessor // GetOrSetMember
    >;
    using TextBoardMembers = MyTuple<
        // TextBuilderMembers
        MyTuple<
            MyTuple<Func<int, int, object>, Func<int>>, // GetLineMember, GetLineCount
            Func<Vector2I, int, object>, // GetCharMember
            ApiMemberAccessor, // GetOrSetMember
            Action<IList<RichStringMembers>, Vector2I>, // Insert
            Action<IList<RichStringMembers>>, // SetText
            Action // Clear
        >,
        FloatProp, // Scale
        Func<Vector2>, // Size
        Func<Vector2>, // TextSize
        Vec2Prop, // FixedSize
        Action<Vector2, MatrixD> // Draw 
    >;

    namespace UI.Server
    {
        using HudClientMembers = MyTuple<
            CursorMembers, // Cursor
            Func<TextBoardMembers>, // GetNewTextBoard
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
                public IReadOnlyList<HudUpdateAccessors> UpdateAccessors => updateAccessors;

                /// <summary>
                /// Returns true if the client has been registered to the TreeManager
                /// </summary>
                public bool Registered { get; private set; }

                /// <summary>
                /// If true, then the client is requesting that the cursor be enabled
                /// </summary>
                public bool enableCursor;

                /// <summary>
                /// If true, then the client is requesting that the draw list be rebuilt
                /// </summary>
                public bool refreshDrawList;

                private bool refreshRequested;
                private readonly List<HudUpdateAccessors> updateAccessors;

                public TreeClient()
                {
                    updateAccessors = new List<HudUpdateAccessors>();
                    Registered = TreeManager.RegisterClient(this);
                }

                public void Update(int tick)
                {
                    if (refreshDrawList)
                        refreshRequested = true;

                    if (enableCursor)
                        instance._cursor.Visible = true;

                    if (refreshRequested && (tick % treeRefreshRate) == 0)
                    {
                        updateAccessors.Clear();
                        GetUpdateAccessors?.Invoke(updateAccessors, 0);

                        if (updateAccessors.Capacity > updateAccessors.Count * 2)
                            updateAccessors.TrimExcess();

                        refreshRequested = false;
                        refreshDrawList = false;
                        TreeManager.RefreshRequested = true;
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
                        Item2 = GetTextBoardData,
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
                                    return enableCursor;
                                else
                                    enableCursor = (bool)data;
                                break;
                            }
                        case HudMainAccessors.RefreshDrawList:
                            {
                                if (data == null)
                                    return refreshDrawList;
                                else
                                    refreshDrawList = (bool)data;
                                break;
                            }
                        case HudMainAccessors.GetUpdateAccessors:
                            {
                                if (data == null)
                                    return GetUpdateAccessors;
                                else
                                {
                                    GetUpdateAccessors = data as Action<List<HudUpdateAccessors>, byte>;
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
                    }

                    return null;
                }
            }
        }
    }
}
