using System;
using System.Collections.Generic;
using VRage;
using BindDefinitionData = VRage.MyTuple<string, string[]>;
using BindMembers = VRage.MyTuple<
    System.Func<object, int, object>, // GetOrSetMember
    System.Func<bool>, // IsPressed
    System.Func<bool>, // IsPressedAndHeld
    System.Func<bool>, // IsNewPressed
    System.Func<bool> // IsReleased
>;
using ApiMemberAccessor = System.Func<object, int, object>;

namespace RichHudFramework
{
    using ControlMembers = MyTuple<string, string, int, Func<bool>, bool, ApiMemberAccessor>;
    using BindGroupMembers = MyTuple<
        string, // Name                
        BindMembers[], // Binds
        Action, // HandleInput
        ApiMemberAccessor // GetOrSetMember
    >;

    namespace UI.Server
    {
        using BindClientMembers = MyTuple<
            MyTuple<Func<int, ControlMembers?>, Func<int>>, // Control List
            Action, // HandleInput
            ApiMemberAccessor, // GetOrSetMember
            Action // Unload
        >;

        public sealed partial class BindManager
        {
            private class BindClient : IBindClient
            {
                public ReadOnlyCollection<IBindGroup> Groups { get; }
                public ReadOnlyCollection<IControl> Controls => BindManager.Controls;

                private readonly List<IBindGroup> bindGroups;

                public BindClient()
                {
                    bindGroups = new List<IBindGroup>();
                    Groups = new ReadOnlyCollection<IBindGroup>(bindGroups);
                }

                public void HandleInput()
                {
                    foreach (IBindGroup group in bindGroups)
                        group.HandleInput();
                }

                public IControl GetControl(string name) =>
                    BindManager.GetControl(name);

                public IBindGroup GetOrCreateGroup(string name)
                {
                    IBindGroup group = GetBindGroup(name);

                    if (group == null)
                    {
                        group = new BindGroup(name);
                        bindGroups.Add(group);
                    }

                    return group;
                }

                /// <summary>
                /// Retrieves a copy of the list of all registered groups.
                /// </summary>
                public IBindGroup[] GetBindGroups() =>
                    bindGroups.ToArray();

                /// <summary>
                /// Retrieves a bind group using its name.
                /// </summary>
                public IBindGroup GetBindGroup(string name)
                {
                    name = name.ToLower();
                    return bindGroups.Find(x => (x.Name == name));
                }

                public BindGroupMembers[] GetGroupData()
                {
                    BindGroupMembers[] groupData = new BindGroupMembers[bindGroups.Count];

                    for (int n = 0; n < groupData.Length; n++)
                        groupData[n] = bindGroups[n].GetApiData();

                    return groupData;
                }

                public IControl[] GetCombo(IList<int> indices) =>
                    BindManager.GetCombo(indices);

                public int[] GetComboIndices(IList<IControl> controls) =>
                    BindManager.GetComboIndices(controls);

                public void Unload()
                {
                    foreach (BindGroup group in bindGroups)
                        group.ClearSubscribers();

                    bindGroups.Clear();
                }

                private object GetOrsetMember(object data, int memberEnum)
                {
                    switch((BindClientAccessors)memberEnum)
                    {
                        case BindClientAccessors.GetComboIndices:
                            {
                                var list = data as IList<string>;
                                return BindManager.GetComboIndices(list);
                            }
                        case BindClientAccessors.GetControlByName:
                            {
                                var name = data as string;
                                return BindManager.GetControl(name).GetApiData();
                            }
                        case BindClientAccessors.GetOrCreateGroup:
                            {
                                var name = data as string;
                                return GetOrCreateGroup(name).GetApiData();
                            }
                        case BindClientAccessors.GetGroupData:
                            return GetGroupData();
                        case BindClientAccessors.Unload:
                            Unload();
                            break;
                    }

                    return null;
                }

                public BindClientMembers GetApiData()
                {
                    BindClientMembers apiData = new BindClientMembers()
                    {
                        Item1 = new MyTuple<Func<int, ControlMembers?>, Func<int>>(x => BindManager.Controls[x]?.GetApiData(), () => BindManager.Controls.Count),
                        Item2 = HandleInput,
                        Item3 = GetOrsetMember,
                        Item4 = Unload,
                    };

                    return apiData;
                }
            }
        }
    }
}