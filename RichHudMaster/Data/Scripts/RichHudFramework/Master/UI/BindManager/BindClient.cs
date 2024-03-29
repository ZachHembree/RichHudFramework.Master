﻿using System;
using System.Collections.Generic;
using VRage;
using VRageMath;
using RichHudFramework.Server;
using BindDefinitionData = VRage.MyTuple<string, string[]>;
using ApiMemberAccessor = System.Func<object, int, object>;

namespace RichHudFramework
{
    namespace UI.Server
    {
        using BindClientMembers = MyTuple<
            ApiMemberAccessor, // GetOrSetMember
            MyTuple<Func<int, object, int, object>, Func<int>>, // GetOrSetGroupMember, GetGroupCount
            MyTuple<Func<Vector2I, object, int, object>, Func<int, int>>, // GetOrSetBindMember, GetBindCount
            Func<Vector2I, int, bool>, // IsBindPressed
            MyTuple<Func<int, int, object>, Func<int>>, // GetControlMember, GetControlCount
            Action // Unload
        >;

        public sealed partial class BindManager
        {
            public class Client
            {
                /// <summary>
                /// Read-only collection of bind groups registered to the client
                /// </summary>
                public IReadOnlyList<IBindGroup> Groups => bindGroups;

                /// <summary>
                /// Read-only collection of all available controls for use with key binds
                /// </summary>
                public IReadOnlyList<IControl> Controls => BindManager.Instance.controls;

                public SeBlacklistModes RequestBlacklistMode 
                { 
                    get { return _requestBlacklistMode | tmpBlacklist; } 
                    set { lastBlacklist = value; _requestBlacklistMode = value; } 
                }

                private readonly RichHudMaster.ModClient masterClient;
                private readonly Action UpdateAction;
                private readonly List<BindGroup> bindGroups;
                private readonly int index;
                private SeBlacklistModes _requestBlacklistMode, lastBlacklist, tmpBlacklist;

                public Client(RichHudMaster.ModClient masterClient = null)
                {
                    this.masterClient = masterClient;
                    bindGroups = new List<BindGroup>();
                    index = _instance.bindClients.Count;
                    _instance.bindClients.Add(this);

                    UpdateAction = UpdateInputInternal;
                }

                /// <summary>
                /// Updates the state of each bind group's controls
                /// </summary>
                public void HandleInput()
                {
                    tmpBlacklist = SeBlacklistModes.None;
                    _requestBlacklistMode = lastBlacklist;

                    if (masterClient?.RunOnExceptionHandler != null)
                        masterClient.RunOnExceptionHandler(UpdateAction);
                    else
                        UpdateInputInternal();
                }

                private void UpdateInputInternal()
                {
                    for(int n = 0; n < bindGroups.Count; n++)
                        bindGroups[n].HandleInput();
                }

                /// <summary>
                /// Sets a temporary control blacklist cleared after every frame. Blacklists set via
                /// property will persist regardless.
                /// </summary>
                public void RequestTempBlacklist(SeBlacklistModes mode)
                {
                    tmpBlacklist |= mode;
                }

                /// <summary>
                /// Retrieves the control with the given name
                /// </summary>
                public IControl GetControl(string name) =>
                    BindManager.GetControl(name);

                /// <summary>
                /// Returns the first bind group with the name given or creates
                /// a new one if one isn't found.
                /// </summary>
                public IBindGroup GetOrCreateGroup(string name)
                {
                    name = name.ToLower();
                    BindGroup group = bindGroups.Find(x => (x.Name == name));

                    if (group == null)
                    {
                        group = new BindGroup(bindGroups.Count, name);
                        bindGroups.Add(group);
                    }

                    return group;
                }

                /// <summary>
                /// Retrieves a bind group using its name.
                /// </summary>
                public IBindGroup GetBindGroup(string name)
                {
                    name = name.ToLower();
                    return bindGroups.Find(x => (x.Name == name));
                }

                /// <summary>
                /// Clears subscribers to all binds and remove all bind groups
                /// </summary>
                public void ClearBindGroups()
                {
                    foreach (BindGroup group in bindGroups)
                        group.ClearSubscribers();

                    bindGroups.Clear();
                }

                /// <summary>
                /// Clears bind group data and unregisters it from master
                /// </summary>
                public void Unload()
                {
                    if (_instance?.bindClients != null && _instance.bindClients.Count > index && _instance.bindClients[index] == this)
                        _instance.bindClients.RemoveAt(index);
                }

                /// <summary>
                /// Returns or modifies the group member associated with the given enum.
                /// </summary>
                private object GetOrSetMember(object data, int memberEnum)
                {
                    switch ((BindClientAccessors)memberEnum)
                    {
                        case BindClientAccessors.GetOrCreateGroup:
                            return GetOrCreateGroup(data as string)?.Index ?? -1;
                        case BindClientAccessors.GetBindGroup:
                            return GetBindGroup(data as string)?.Index ?? -1;
                        case BindClientAccessors.GetComboIndices:
                            return BindManager.GetComboIndices(data as IReadOnlyList<string>);
                        case BindClientAccessors.GetControlByName:
                            return BindManager.GetControl(data as string)?.Index ?? -1;
                        case BindClientAccessors.ClearBindGroups:
                            ClearBindGroups(); break;
                        case BindClientAccessors.Unload:
                            Unload(); break;
                        case BindClientAccessors.RequestBlacklistMode:
                            {
                                if (data != null)
                                    { RequestBlacklistMode = (SeBlacklistModes)data; break; }
                                else
                                    return RequestBlacklistMode;
                            }
                        case BindClientAccessors.IsChatOpen:
                            return IsChatOpen;
                    }

                    return null;
                }

                private object GetOrSetGroupMember(int index, object data, int memberEnum)
                {
                    IBindGroup group = bindGroups[index];

                    switch ((BindGroupAccessors)memberEnum)
                    {
                        case BindGroupAccessors.Name:
                            return group.Name;
                        case BindGroupAccessors.GetBindFromName:
                            return new Vector2I(group.Index, group.GetBind(data as string)?.Index ?? -1);
                        case BindGroupAccessors.DoesBindExist:
                            return group.DoesBindExist(data as string);
                        case BindGroupAccessors.RegisterBindNames:
                            group.RegisterBinds(data as IReadOnlyList<string>); break;
                        case BindGroupAccessors.RegisterBindIndices:
                            group.RegisterBinds(data as IReadOnlyList<MyTuple<string, IReadOnlyList<int>>>); break;
                        case BindGroupAccessors.RegisterBindDefinitions:
                            group.RegisterBinds(data as IReadOnlyList<BindDefinitionData>); break;
                        case BindGroupAccessors.AddBindWithIndices:
                            {
                                var args = (MyTuple<string, IReadOnlyList<int>>)data;
                                return new Vector2I(group.Index, group.AddBind(args.Item1, args.Item2).Index);
                            }
                        case BindGroupAccessors.AddBindWithNames:
                            {
                                var args = (MyTuple<string, IReadOnlyList<string>>)data;
                                return new Vector2I(group.Index, group.AddBind(args.Item1, args.Item2).Index);
                            }
                        case BindGroupAccessors.DoesComboConflict:
                            {
                                var args = (MyTuple<IReadOnlyList<int>, int>)data;
                                return group.DoesComboConflict(args.Item1, args.Item2);
                            }
                        case BindGroupAccessors.TryRegisterBindName:
                            {
                                IBind bind;
                                bool success = group.TryRegisterBind(data as string, out bind);

                                return success ? bind.Index : -1;
                            }
                        case BindGroupAccessors.TryRegisterBindWithIndices:
                            {
                                var args = (MyTuple<string, IReadOnlyList<int>>)data;

                                IBind bind;
                                bool success = group.TryRegisterBind(args.Item1, out bind, args.Item2);

                                return success ? bind.Index : -1;
                            }
                        case BindGroupAccessors.TryRegisterBindWithNames:
                            {
                                var args = (MyTuple<string, IReadOnlyList<string>>)data;

                                IBind bind;
                                bool success = group.TryRegisterBind(args.Item1, out bind, args.Item2);

                                return success ? bind.Index : -1;
                            }
                        case BindGroupAccessors.TryLoadBindData:
                            {
                                var arg = data as IReadOnlyList<BindDefinitionData>;
                                return group.TryLoadBindData(arg);
                            }
                        case BindGroupAccessors.GetBindData:
                            return group.GetBindData();
                        case BindGroupAccessors.ClearSubscribers:
                            group.ClearSubscribers();
                            break;
                        case BindGroupAccessors.ID:
                            return group;
                    }

                    return null;
                }

                /// <summary>
                /// Returns or sets the member associated with the given enum of the bind at the given index .
                /// </summary>
                private object GetOrSetBindMember(Vector2I index, object data, int memberEnum)
                {
                    IBind bind = bindGroups[index.X][index.Y];

                    switch ((BindAccesssors)memberEnum)
                    {
                        case BindAccesssors.Name:
                            return bind.Name;
                        case BindAccesssors.Analog:
                            return bind.Analog;
                        case BindAccesssors.Index:
                            return bind.Index;
                        case BindAccesssors.IsPressed:
                            return bind.IsPressed;
                        case BindAccesssors.IsNewPressed:
                            return bind.IsNewPressed;
                        case BindAccesssors.IsPressedAndHeld:
                            return bind.IsPressedAndHeld;
                        case BindAccesssors.OnNewPress:
                            {
                                var eventData = (MyTuple<bool, Action>)data;

                                if (eventData.Item1)
                                    bind.NewPressed += (sender, args) => eventData.Item2();
                                else
                                    bind.NewPressed -= (sender, args) => eventData.Item2();

                                break;
                            }
                        case BindAccesssors.OnPressAndHold:
                            {
                                var eventData = (MyTuple<bool, Action>)data;

                                if (eventData.Item1)
                                    bind.PressedAndHeld += (sender, args) => eventData.Item2();
                                else
                                    bind.PressedAndHeld -= (sender, args) => eventData.Item2();

                                break;
                            }
                        case BindAccesssors.OnRelease:
                            {
                                var eventData = (MyTuple<bool, Action>)data;

                                if (eventData.Item1)
                                    bind.Released += (sender, args) => eventData.Item2();
                                else
                                    bind.Released -= (sender, args) => eventData.Item2();

                                break;
                            }
                        case BindAccesssors.GetCombo:
                            return bind.GetComboIndices();
                        case BindAccesssors.TrySetComboWithIndices:
                            {
                                var comboData = (MyTuple<IReadOnlyList<int>, bool, bool>)data;
                                return bind.TrySetCombo(comboData.Item1, comboData.Item2, comboData.Item3);
                            }
                        case BindAccesssors.TrySetComboWithNames:
                            {
                                var comboData = (MyTuple<IReadOnlyList<string>, bool, bool>)data;
                                return bind.TrySetCombo(comboData.Item1, comboData.Item2, comboData.Item3);
                            }
                        case BindAccesssors.ClearCombo:
                            bind.ClearCombo(); break;
                        case BindAccesssors.ClearSubscribers:
                            bind.ClearSubscribers(); break;
                    }

                    return null;
                }

                /// <summary>
                /// Returns true if the bind at the given index is pressed. Whether IsPressed,
                /// IsNewPressed or IsPressedAndHeld is used depends on the enum.
                /// </summary>
                private bool IsBindPressed(Vector2I index, int memberEnum)
                {
                    IBind bind = bindGroups[index.X][index.Y];

                    switch ((BindAccesssors)memberEnum)
                    {
                        case BindAccesssors.IsPressed:
                            return bind.IsPressed;
                        case BindAccesssors.IsNewPressed:
                            return bind.IsNewPressed;
                        case BindAccesssors.IsPressedAndHeld:
                            return bind.IsPressedAndHeld;
                        case BindAccesssors.IsReleased:
                            return bind.IsReleased;
                    }

                    return false;
                }

                /// <summary>
                /// Returns the member associated with the enum for the control at the
                /// given index.
                /// </summary>
                private object GetControlMember(int index, int memberEnum)
                {
                    Control control = _instance.controls[index];

                    if (control != null)
                    {
                        switch ((ControlAccessors)memberEnum)
                        {
                            case ControlAccessors.Name:
                                return control.Name;
                            case ControlAccessors.DisplayName:
                                return control.DisplayName;
                            case ControlAccessors.Index:
                                return control.Index;
                            case ControlAccessors.IsPressed:
                                return control.IsPressed;
                            case ControlAccessors.IsNewPressed:
                                return control.IsNewPressed;
                            case ControlAccessors.IsReleased:
                                return control.IsReleased;
                            case ControlAccessors.Analog:
                                return control.Analog;
                        }
                    }

                    return null;
                }

                public BindClientMembers GetApiData()
                {
                    BindClientMembers apiData = new BindClientMembers()
                    {
                        Item1 = GetOrSetMember,
                        Item2 = new MyTuple<Func<int, object, int, object>, Func<int>>(GetOrSetGroupMember, () => bindGroups.Count),
                        Item3 = new MyTuple<Func<Vector2I, object, int, object>, Func<int, int>>(GetOrSetBindMember, x => bindGroups[x].Count),
                        Item4 = IsBindPressed,
                        Item5 = new MyTuple<Func<int, int, object>, Func<int>>(GetControlMember, () => _instance.controls.Length),
                        Item6 = Unload
                    };

                    return apiData;
                }
            }
        }
    }
}