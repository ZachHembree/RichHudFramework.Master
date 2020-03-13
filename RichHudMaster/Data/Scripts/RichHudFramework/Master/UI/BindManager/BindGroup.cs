using RichHudFramework.Internal;
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
using ControlMembers = VRage.MyTuple<string, int, System.Func<bool>, bool>;
using ApiMemberAccessor = System.Func<object, int, object>;
using System.Collections;

namespace RichHudFramework
{
    using BindGroupMembers = MyTuple<
        string, // Name                
        BindMembers[], // Binds
        Action, // HandleInput
        ApiMemberAccessor // GetOrSetMember
    >;

    namespace UI.Server
    {
        public sealed partial class BindManager
        {
            /// <summary>
            /// A collection of unique keybinds.
            /// </summary>
            private partial class BindGroup : IBindGroup
            {
                public const int maxBindLength = 3;
                private const long holdTime = TimeSpan.TicksPerMillisecond * 500;

                public string Name { get; }
                public IBind this[int index] => keyBinds[index];
                public int Count => keyBinds.Count;
                public object ID => this;

                private readonly List<Bind> keyBinds;
                private readonly List<IBind>[] controlMap; // X = controls; Y = associated binds
                private List<IControl> usedControls;
                private List<List<IBind>> bindMap; // X = used controls; Y = associated binds

                public BindGroup(string name)
                {
                    Name = name;
                    controlMap = new List<IBind>[Controls.Count];

                    for (int n = 0; n < controlMap.Length; n++)
                        controlMap[n] = new List<IBind>();

                    keyBinds = new List<Bind>();
                    usedControls = new List<IControl>();
                    bindMap = new List<List<IBind>>();
                }

                public void ClearSubscribers()
                {
                    foreach (Bind bind in keyBinds)
                        bind.ClearSubscribers();
                }

                /// <summary>
                /// Updates input state
                /// </summary>
                public void HandleInput()
                {
                    if (keyBinds.Count > 0)
                    {
                        int bindsPressed = GetPressedBinds();

                        if (bindsPressed > 1)
                            DisambiguatePresses();

                        foreach (Bind bind in keyBinds)
                            bind.UpdatePress(bind.length > 0 && bind.bindHits == bind.length && !bind.beingReleased);
                    }
                }

                /// <summary>
                /// Finds and counts number of pressed key binds.
                /// </summary>
                private int GetPressedBinds()
                {
                    int bindsPressed = 0;

                    foreach (Bind bind in keyBinds)
                        bind.bindHits = 0;

                    foreach (IControl con in usedControls)
                    {
                        if (con.IsPressed)
                        {
                            foreach (Bind bind in controlMap[con.Index])
                                bind.bindHits++;
                        }
                    }

                    // Partial presses on previously pressed binds count as full presses.
                    foreach (Bind bind in keyBinds)
                    {
                        if (bind.IsPressed || bind.beingReleased)
                        {
                            if (bind.bindHits > 0 && bind.bindHits < bind.length)
                            {
                                bind.bindHits = bind.length;
                                bind.beingReleased = true;
                            }
                            else if (bind.beingReleased)
                                bind.beingReleased = false;
                        }

                        if (bind.length > 0 && bind.bindHits == bind.length)
                            bindsPressed++;
                        else
                            bind.bindHits = 0;
                    }

                    return bindsPressed;
                }

                /// <summary>
                /// Resolves conflicts between pressed binds with shared controls.
                /// </summary>
                private void DisambiguatePresses()
                {
                    Bind first, longest;
                    int controlHits;

                    // If more than one pressed bind shares the same control, the longest
                    // binds take precedence. Any binds shorter than the longest will not
                    // be counted as being pressed.
                    foreach (IControl con in usedControls)
                    {
                        first = null;
                        controlHits = 0;
                        longest = GetLongestBindPressForControl(con);

                        foreach (Bind bind in controlMap[con.Index])
                        {
                            if (bind.bindHits > 0 && (bind != longest))
                            {
                                if (controlHits > 0)
                                    bind.bindHits--;
                                else if (controlHits == 0)
                                    first = bind;

                                controlHits++;
                            }
                        }

                        if (controlHits > 0)
                            first.bindHits--;
                    }
                }

                /// <summary>
                /// Determines the length of the longest bind pressed for a given control on the bind map.
                /// </summary>
                private Bind GetLongestBindPressForControl(IControl con)
                {
                    Bind longest = null;

                    foreach (Bind bind in controlMap[con.Index])
                    {
                        if (bind.bindHits > 0 && (longest == null || bind.length > longest.length || (longest.beingReleased && !bind.beingReleased && longest.length == bind.length)))
                            longest = bind;
                    }

                    return longest;
                }

                /// <summary>
                /// Attempts to register a set of binds with the given names.
                /// </summary>
                public void RegisterBinds(IList<string> bindNames)
                {
                    IBind newBind;

                    foreach (string name in bindNames)
                        TryRegisterBind(name, out newBind, silent: true);
                }

                /// <summary>
                /// Attempts to register a set of binds with the given names.
                /// </summary>
                public void RegisterBinds(IEnumerable<MyTuple<string, IList<int>>> bindData)
                {
                    foreach (var bind in bindData)
                        AddBind(bind.Item1, bind.Item2);
                }

                /// <summary>
                /// Attempts to register a set of binds using the names and controls specified in the definitions.
                /// </summary>
                public void RegisterBinds(IList<BindDefinition> bindData)
                {
                    IBind newBind;

                    foreach (BindDefinition bind in bindData)
                        TryRegisterBind(bind.name, out newBind, bind.controlNames, true);
                }

                /// <summary>
                /// Adds a bind with the given name and the given key combo. Throws an exception if the bind is invalid.
                /// </summary>
                public IBind AddBind(string bindName, IList<string> combo) =>
                    AddBind(bindName, GetCombo(combo));

                /// <summary>
                /// Adds a bind with the given name and the given key combo. Throws an exception if the bind is invalid.
                /// </summary>
                public IBind AddBind(string bindName, IList<ControlData> combo) =>
                    AddBind(bindName, GetCombo(combo));

                /// <summary>
                /// Adds a bind with the given name and the given key combo. Throws an exception if the bind is invalid.
                /// </summary>
                public IBind AddBind(string bindName, IList<int> combo) =>
                    AddBind(bindName, GetCombo(combo));

                /// <summary>
                /// Adds a bind with the given name and the given key combo. Throws an exception if the bind is invalid.
                /// </summary>
                public IBind AddBind(string bindName, IList<IControl> combo = null)
                {
                    IBind bind;

                    if (TryRegisterBind(bindName, combo, out bind, true))
                        return bind;
                    else
                        throw new Exception($"Bind {Name}.{bindName} is invalid. Bind names and key combinations must be unique.");
                }

                /// <summary>
                /// Tries to register a bind using the given name and the given key combo. Shows an error message in chat upon failure.
                /// </summary>
                public bool TryRegisterBind(string bindName, IList<int> combo, out IBind newBind, bool silent = false) =>
                    TryRegisterBind(bindName, GetCombo(combo), out newBind, silent);

                /// <summary>
                /// Tries to register a bind using the given name and the given key combo. Shows an error message in chat upon failure.
                /// </summary>
                public bool TryRegisterBind(string bindName, out IBind bind, IList<string> combo = null, bool silent = false)
                {
                    string[] uniqueControls = combo?.GetUnique();
                    IControl[] newCombo = null;
                    bind = null;

                    if (combo == null || TryGetCombo(uniqueControls, out newCombo))
                        return TryRegisterBind(bindName, newCombo, out bind, silent);
                    else if (!silent)
                        ExceptionHandler.SendChatMessage($"Invalid bind for {Name}.{bindName}. One or more control names were not recognised.");

                    return false;
                }

                /// <summary>
                /// Tries to register a bind using the given name and the given key combo. Shows an error message in chat upon failure.
                /// </summary>
                public bool TryRegisterBind(string bindName, IList<IControl> combo, out IBind newBind, bool silent = false)
                {
                    newBind = null;

                    if (!DoesBindExist(bindName))
                    {
                        Bind bind = new Bind(bindName, keyBinds.Count, this);
                        newBind = bind;
                        keyBinds.Add(bind);

                        if (combo != null)
                            return bind.TrySetCombo(combo, true, silent);
                        else
                            return true;
                    }
                    else if (!silent)
                        ExceptionHandler.SendChatMessage($"Bind {Name}.{bindName} already exists.");

                    return false;
                }

                /// <summary>
                /// Attempts to register a bind using the name and controls. Returns API data.
                /// </summary>
                private BindMembers? TryRegisterBind(string name, IList<string> combo, bool silent)
                {
                    IBind bind;

                    if (TryRegisterBind(name, out bind, combo, silent))
                        return bind.GetApiData();
                    else
                        return null;
                }

                /// <summary>
                /// Attempts to register a bind using the name and controls. Returns API data.
                /// </summary>
                private BindMembers? TryRegisterBind(string name, IList<int> combo, bool silent)
                {
                    IBind bind;
                    IControl[] controls = combo != null ? GetCombo(combo) : null;

                    if (TryRegisterBind(name, controls, out bind, silent))
                        return bind.GetApiData();
                    else
                        return null;
                }

                /// <summary>
                /// Replaces current bind combos with combos based on the given <see cref="BindDefinition"/>[]. Does not register new binds.
                /// </summary>
                public bool TryLoadBindData(IList<BindDefinition> bindData)
                {
                    List<IControl> oldUsedControls;
                    List<List<IBind>> oldBindMap;
                    bool bindError = false;

                    if (bindData != null && bindData.Count > 0)
                    {
                        oldUsedControls = usedControls;
                        oldBindMap = bindMap;

                        UnregisterControls();
                        usedControls = new List<IControl>(bindData.Count);
                        bindMap = new List<List<IBind>>(bindData.Count);

                        foreach (BindDefinition bind in bindData)
                            if (!GetBind(bind.name).TrySetCombo(bind.controlNames, false, false))
                            {
                                bindError = true;
                                break;
                            }

                        if (bindError)
                        {
                            ExceptionHandler.SendChatMessage("One or more keybinds in the given configuration were invalid or conflict with oneanother.");
                            UnregisterControls();

                            usedControls = oldUsedControls;
                            bindMap = oldBindMap;
                            ReregisterControls();

                            return false;
                        }
                        else
                            return true;
                    }
                    else
                    {
                        ExceptionHandler.SendChatMessage("Bind data cannot be null or empty.");
                        return false;
                    }
                }

                /// <summary>
                /// Replaces current key combinations with those specified by the BindDefinitionData. Does not register new binds.
                /// </summary>
                private BindMembers[] TryLoadApiBindData(IList<BindDefinitionData> data)
                {
                    BindDefinition[] definitions = new BindDefinition[data.Count];

                    for (int n = 0; n < data.Count; n++)
                        definitions[n] = data[n];

                    if (TryLoadBindData(definitions))
                    {
                        BindMembers[] binds = new BindMembers[keyBinds.Count];

                        for (int n = 0; n < keyBinds.Count; n++)
                            binds[n] = keyBinds[n].GetApiData();

                        return binds;
                    }
                    else
                        return null;
                }

                private void ReregisterControls()
                {
                    for (int n = 0; n < usedControls.Count; n++)
                        controlMap[usedControls[n].Index] = bindMap[n];
                }

                private void UnregisterControls()
                {
                    foreach (IControl con in usedControls)
                        controlMap[con.Index] = new List<IBind>();
                }

                /// <summary>
                /// Unregisters a given bind from its current key combination and registers it to a
                /// new one.
                /// </summary>
                private void RegisterBindToCombo(Bind bind, IList<IControl> newCombo)
                {
                    if (bind != null && newCombo != null)
                    {
                        UnregisterBindFromCombo(bind);

                        foreach (IControl con in newCombo)
                        {
                            List<IBind> registeredBinds = controlMap[con.Index];

                            if (registeredBinds.Count == 0)
                            {
                                usedControls.Add(con);
                                bindMap.Add(registeredBinds);
                            }

                            registeredBinds.Add(bind);

                            if (con.Analog)
                                bind.Analog = true;
                        }

                        bind.length = newCombo.Count;
                    }
                }

                /// <summary>
                /// Unregisters a bind from its key combo if it has one.
                /// </summary>
                private void UnregisterBindFromCombo(Bind bind)
                {
                    for (int n = 0; n < usedControls.Count; n++)
                    {
                        List<IBind> registeredBinds = controlMap[usedControls[n].Index];
                        registeredBinds.Remove(bind);

                        if (registeredBinds.Count == 0)
                        {
                            bindMap.Remove(registeredBinds);
                            usedControls.Remove(usedControls[n]);
                        }
                    }

                    bind.Analog = false;
                    bind.length = 0;
                }

                /// <summary>
                /// Retrieves key bind using its name.
                /// </summary>
                public IBind GetBind(string name)
                {
                    name = name.ToLower();

                    foreach (Bind bind in keyBinds)
                        if (bind.Name.ToLower() == name)
                            return bind;

                    ExceptionHandler.SendChatMessage($"{name} is not a valid bind name.");
                    return null;
                }

                /// <summary>
                /// Retrieves the set of key binds as an array of BindDefinition
                /// </summary>
                public BindDefinition[] GetBindDefinitions()
                {
                    BindDefinition[] bindData = new BindDefinition[keyBinds.Count];
                    string[][] combos = new string[keyBinds.Count][];

                    for (int x = 0; x < keyBinds.Count; x++)
                    {
                        IList<IControl> combo = keyBinds[x].GetCombo();
                        combos[x] = new string[combo.Count];

                        for (int y = 0; y < combo.Count; y++)
                            combos[x][y] = combo[y].Name;
                    }

                    for (int n = 0; n < keyBinds.Count; n++)
                        bindData[n] = new BindDefinition(keyBinds[n].Name, combos[n]);

                    return bindData;
                }

                /// <summary>
                /// Retrieves the set of key binds as an array of BindDefinition
                /// </summary>
                private BindDefinitionData[] GetBindData()
                {
                    BindDefinitionData[] bindData = new BindDefinitionData[keyBinds.Count];
                    string[][] combos = new string[keyBinds.Count][];

                    for (int x = 0; x < keyBinds.Count; x++)
                    {
                        IList<IControl> combo = keyBinds[x].GetCombo();
                        combos[x] = new string[combo.Count];

                        for (int y = 0; y < combo.Count; y++)
                            combos[x][y] = combo[y].Name;
                    }

                    for (int n = 0; n < keyBinds.Count; n++)
                        bindData[n] = new BindDefinitionData(keyBinds[n].Name, combos[n]);

                    return bindData;
                }

                private object GetOrSetMember(object data, int memberEnum)
                {
                    switch ((BindGroupAccessors)memberEnum)
                    {
                        case BindGroupAccessors.DoesComboConflict:
                            {
                                var args = (MyTuple<IList<int>, int>)data;
                                return DoesComboConflict(args.Item1, args.Item2);
                            }
                        case BindGroupAccessors.TryRegisterBind:
                            {
                                var args = (MyTuple<string, IList<int>, bool>)data;
                                return TryRegisterBind(args.Item1, args.Item2, args.Item3);
                            }
                        case BindGroupAccessors.TryLoadBindData:
                            {
                                var arg = data as IList<BindDefinitionData>;
                                return TryLoadApiBindData(arg);
                            }
                        case BindGroupAccessors.TryRegisterBind2:
                            {
                                var args = (MyTuple<string, IList<string>, bool>)data;
                                return TryRegisterBind(args.Item1, args.Item2, args.Item3);
                            }
                        case BindGroupAccessors.GetBindData:
                            return GetBindData();
                        case BindGroupAccessors.ClearSubscribers:
                            ClearSubscribers();
                            break;
                        case BindGroupAccessors.ID:
                            return this;
                    }

                    return null;
                }

                /// <summary>
                /// Retreives information needed to access the BindGroup via the API.
                /// </summary>
                public BindGroupMembers GetApiData()
                {
                    BindMembers[] bindData = new BindMembers[keyBinds.Count];

                    for (int n = 0; n < keyBinds.Count; n++)
                        bindData[n] = keyBinds[n].GetApiData();

                    BindGroupMembers apiData = new BindGroupMembers()
                    {
                        Item1 = Name,
                        Item2 = bindData,
                        Item3 = HandleInput,
                        Item4 = GetOrSetMember
                    };

                    return apiData;
                }

                /// <summary>
                /// Returns true if the given list of controls conflicts with any existing binds.
                /// </summary>
                public bool DoesComboConflict(IList<IControl> newCombo, IBind exception = null) =>
                    DoesComboConflict(BindManager.GetComboIndices(newCombo), (exception != null) ? exception.Index : -1);

                /// <summary>
                /// Determines if given combo is equivalent to any existing binds.
                /// </summary>
                private bool DoesComboConflict(IList<int> newCombo, int exception = -1)
                {
                    int matchCount;

                    for (int n = 0; n < keyBinds.Count; n++)
                        if (keyBinds[n].Index != exception && keyBinds[n].length == newCombo.Count)
                        {
                            matchCount = 0;

                            foreach (int con in newCombo)
                                if (BindUsesControl(keyBinds[n], BindManager.Controls[con]))
                                    matchCount++;
                                else
                                    break;

                            if (matchCount > 0 && matchCount == newCombo.Count)
                                return true;
                        }

                    return false;
                }

                /// <summary>
                /// Returns true if a keybind with the given name exists.
                /// </summary>
                public bool DoesBindExist(string name)
                {
                    name = name.ToLower();

                    foreach (Bind bind in keyBinds)
                        if (bind.Name.ToLower() == name)
                            return true;

                    return false;
                }

                /// <summary>
                /// Determines whether or not a bind with a given index uses a given control.
                /// </summary>
                private bool BindUsesControl(Bind bind, IControl con) =>
                    controlMap[con.Index].Contains(bind);
            }
        }
    }
}