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
            public class Client
            {
                /// <summary>
                /// If true, then the client is requesting that the cursor be enabled
                /// </summary>
                public bool EnableCursor { get; private set; }

                /// <summary>
                /// If true, then the client is requesting that the draw list be rebuilt
                /// </summary>
                public bool RefreshDrawList { get; set; }

                /// <summary>
                /// Delegate used to retrieve UI update delegates from clients
                /// </summary>
                public Action<List<HudUpdateAccessors>, byte> GetUpdateAccessors { get; private set; }

                private int index;

                public Client()
                {
                    index = _instance.hudClients.Count;
                    _instance.hudClients.Add(this);
                }

                /// <summary>
                /// Unregisters the client from HudMain
                /// </summary>
                public void Unregister()
                {
                    if (_instance.hudClients[index] == this)
                        _instance.hudClients.RemoveAt(index);
                }

                /// <summary>
                /// Retrieves API accessor delegates
                /// </summary>
                public HudClientMembers GetApiData()
                {
                    Init();

                    return new HudClientMembers()
                    {
                        Item1 = _instance._cursor.GetApiData(),
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
                                    return ClipBoard?.ApiData;
                                else
                                    ClipBoard = new RichText(data as IList<RichStringMembers>);
                                break;
                            }
                        case HudMainAccessors.EnableCursor:
                            {
                                if (data == null)
                                    return EnableCursor;
                                else
                                    EnableCursor = (bool)data;
                                break;
                            }
                        case HudMainAccessors.RefreshDrawList:
                            {
                                if (data == null)
                                    return RefreshDrawList;
                                else
                                    RefreshDrawList = (bool)data;
                                break;
                            }
                        case HudMainAccessors.GetUpdateAccessors:
                            {
                                if (data == null)
                                    return GetUpdateAccessors;
                                else
                                    GetUpdateAccessors = data as Action<List<HudUpdateAccessors>, byte>;
                                break;
                            }
                        case HudMainAccessors.GetFocusOffset:
                            return GetFocusOffset(data as Action<byte>);
                    }

                    return null;
                }
            }
        }
    }
}
