using System;
using System.Collections;
using System.Collections.Generic;
using VRage;
using BindDefinitionData = VRage.MyTuple<string, string[]>;

namespace RichHudFramework
{
    namespace UI.Server
    {
        public sealed partial class BindManager
        {
            /// <summary>
            /// A collection of unique keybinds.
            /// </summary>
            public partial class BindGroup : IBindGroup
            {
                public const int maxBindLength = 3;
                private const long holdTime = TimeSpan.TicksPerMillisecond * 500;

                public event EventHandler BindChanged;

                /// <summary>
                /// Name assigned to the bind group
                /// </summary>
                public string Name { get; }

                /// <summary>
                /// Retrieves the bind at the specified index
                /// </summary>
                public IBind this[int index] => keyBinds[index];

                /// <summary>
                /// Retrieves the bind with the given name
                /// </summary>
                public IBind this[string name] => GetBind(name);

                /// <summary>
                /// Returns the number of binds in the group
                /// </summary>
                public int Count => keyBinds.Count;

                /// <summary>
                /// Index of the bind group in its associated client
                /// </summary>
                public int Index { get; }

                /// <summary>
                /// Unique identifier
                /// </summary>
                public object ID => this;

                private readonly List<Bind> keyBinds;
                private readonly List<IBind>[] controlMap; // X = controls; Y = associated binds
                private List<IControl> usedControls;
                private List<List<IBind>> bindMap; // X = used controls; Y = associated binds
                private bool wasBindChanged;

                public BindGroup(int index, string name)
                {
                    Name = name;
                    Index = index;

                    controlMap = new List<IBind>[Controls.Count];

                    for (int n = 0; n < controlMap.Length; n++)
                        controlMap[n] = new List<IBind>();

                    keyBinds = new List<Bind>();
                    usedControls = new List<IControl>();
                    bindMap = new List<List<IBind>>();
                }

                /// <summary>
                /// Clears bind subscribers for the entire group
                /// </summary>
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
                        int controlsPressed = GetPressedControls();
                        bool canUpdateBinds = true;

                        if (controlsPressed > 0)
                        {
                            int bindsPressed = GetPressedBinds();

                            if (bindsPressed > 1)
                                DisambiguatePresses();

                            int bindControlsPressed = 0;

                            foreach (Bind bind in keyBinds)
                            {
                                if (bind.length > 0 && bind.bindHits == bind.length && !bind.beingReleased)
                                    bindControlsPressed += bind.length;
                            }
                        }
                        else
                            canUpdateBinds = false;

                        foreach (Bind bind in keyBinds)
                        {
                            bind.beingReleased = bind.beingReleased && bind.bindHits > 0;
                            bind.UpdatePress(canUpdateBinds && (bind.length > 0 && bind.bindHits == bind.length && !bind.beingReleased));
                        }

                        if (wasBindChanged)
                        {
                            BindChanged?.Invoke(this, EventArgs.Empty);
                            wasBindChanged = false;
                        }
                    }
                }

                /// <summary>
                /// Gets number of controls pressed within the group and updates the hit counter
                /// for each bind as appropriate.
                /// </summary>
                private int GetPressedControls()
                {
                    int controlsPressed = 0;
                    bool anyNewPresses = false;

                    foreach (Control con in usedControls)
                    {
                        if (con.IsNewPressed)
                        {
                            anyNewPresses = true;
                            break;
                        }
                    }

                    foreach (Bind bind in keyBinds)
                    {
                        // If any used controls have new presses, stop releasing
                        bind.beingReleased = bind.beingReleased && !anyNewPresses;
                        bind.bindHits = 0;
                    }

                    foreach (Control con in usedControls)
                    {
                        if (con.IsPressed)
                        {
                            foreach (Bind bind in controlMap[con.Index])
                            {
                                bind.bindHits++;
                            }

                            controlsPressed++;
                        }
                    }

                    return controlsPressed;
                }

                /// <summary>
                /// Finds and counts number of pressed key binds.
                /// </summary>
                private int GetPressedBinds()
                {
                    int bindsPressed = 0;

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
                public void RegisterBinds(IReadOnlyList<string> bindNames)
                {
                    IBind newBind;

                    foreach (string name in bindNames)
                        TryRegisterBind(name, out newBind);
                }

                /// <summary>
                /// Attempts to register a set of binds with the given names.
                /// </summary>
                public void RegisterBinds(BindGroupInitializer bindData)
                {
                    foreach (var bind in bindData)
                        AddBind(bind.Item1, bind.Item2);
                }

                /// <summary>
                /// Attempts to register a set of binds with the given names.
                /// </summary>
                public void RegisterBinds(IReadOnlyList<MyTuple<string, IReadOnlyList<int>>> bindData)
                {
                    foreach (var bind in bindData)
                        AddBind(bind.Item1, bind.Item2);
                }

                /// <summary>
                /// Attempts to register a set of binds using the names and controls specified in the definitions.
                /// </summary>
                public void RegisterBinds(IReadOnlyList<BindDefinition> bindData)
                {
                    IBind newBind;

                    foreach (BindDefinition bind in bindData)
                        TryRegisterBind(bind.name, out newBind, bind.controlNames);
                }

                /// <summary>
                /// Attempts to register a set of binds using the names and controls specified in the definitions.
                /// </summary>
                public void RegisterBinds(IReadOnlyList<BindDefinitionData> bindData)
                {
                    IBind newBind;

                    foreach (BindDefinitionData bind in bindData)
                        TryRegisterBind(bind.Item1, out newBind, bind.Item2);
                }

                /// <summary>
                /// Adds a bind with the given name and the given key combo. Throws an exception if the bind is invalid.
                /// </summary>
                public IBind AddBind(string bindName, IReadOnlyList<string> combo) =>
                    AddBind(bindName, GetCombo(combo));

                /// <summary>
                /// Adds a bind with the given name and the given key combo. Throws an exception if the bind is invalid.
                /// </summary>
                public IBind AddBind(string bindName, IReadOnlyList<ControlData> combo) =>
                    AddBind(bindName, GetCombo(combo));

                /// <summary>
                /// Adds a bind with the given name and the given key combo. Throws an exception if the bind is invalid.
                /// </summary>
                public IBind AddBind(string bindName, IReadOnlyList<int> combo) =>
                    AddBind(bindName, GetCombo(combo));

                /// <summary>
                /// Adds a bind with the given name and the given key combo. Throws an exception if the bind is invalid.
                /// </summary>
                public IBind AddBind(string bindName, IReadOnlyList<IControl> combo = null)
                {
                    IBind bind;

                    if (TryRegisterBind(bindName, out bind, combo))
                        return bind;
                    else
                        throw new Exception($"Bind {Name}.{bindName} is invalid. Bind names and key combinations must be unique.");
                }

                /// <summary>
                /// Tries to register a bind using the given name and the given key combo.
                /// </summary>
                public bool TryRegisterBind(string bindName, IReadOnlyList<int> combo, out IBind newBind) =>
                    TryRegisterBind(bindName, out newBind, GetCombo(combo));

                /// <summary>
                /// Tries to register a bind using the given name and the given key combo.
                public bool TryRegisterBind(string bindName, out IBind newBind) =>
                    TryRegisterBind(bindName, null, out newBind);

                /// <summary>
                /// Tries to register a bind using the given name and the given key combo.
                /// </summary>
                public bool TryRegisterBind(string bindName, out IBind newBind, IReadOnlyList<int> combo) =>
                    TryRegisterBind(bindName, out newBind, GetCombo(combo));

                /// <summary>
                /// Tries to register a bind using the given name and the given key combo.
                /// </summary>
                public bool TryRegisterBind(string bindName, out IBind bind, IReadOnlyList<string> combo)
                {
                    string[] uniqueControls = combo?.GetUnique();
                    IControl[] newCombo = null;
                    bind = null;

                    if (combo == null || TryGetCombo(uniqueControls, out newCombo))
                        return TryRegisterBind(bindName, out bind, newCombo);

                    return false;
                }

                /// <summary>
                /// Tries to register a bind using the given name and the given key combo.
                /// </summary>
                public bool TryRegisterBind(string bindName, out IBind newBind, IReadOnlyList<IControl> combo)
                {
                    newBind = null;

                    if (!DoesBindExist(bindName))
                    {
                        Bind bind = new Bind(bindName, keyBinds.Count, this);
                        newBind = bind;
                        keyBinds.Add(bind);

                        if (combo != null && combo.Count > 0)
                            return bind.TrySetCombo(combo, true, true);
                        else
                            return true;
                    }

                    return false;
                }

                /// <summary>
                /// Replaces current bind combos with combos based on the given <see cref="BindDefinitionData"/>[]. Does not register new binds.
                /// </summary>
                public bool TryLoadBindData(IReadOnlyList<BindDefinitionData> bindData)
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

                        foreach (BindDefinitionData bindDef in bindData)
                        {
                            IBind bind = GetBind(bindDef.Item1);

                            if (bind != null && !bind.TrySetCombo(bindDef.Item2, false, false))
                            {
                                bindError = true;
                                break;
                            }
                        }

                        if (bindError)
                        {
                            UnregisterControls();

                            usedControls = oldUsedControls;
                            bindMap = oldBindMap;
                            ReregisterControls();
                        }
                        else
                            return true;
                    }

                    return false;
                }

                /// <summary>
                /// Replaces current bind combos with combos based on the given <see cref="BindDefinition"/>[]. Does not register new binds.
                /// </summary>
                public bool TryLoadBindData(IReadOnlyList<BindDefinition> bindData)
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

                        foreach (BindDefinition bindDef in bindData)
                        {
                            IBind bind = GetBind(bindDef.name);

                            if (bind != null && !bind.TrySetCombo(bindDef.controlNames, false, false))
                            {
                                bindError = true;
                                break;
                            }
                        }

                        if (bindError)
                        {
                            UnregisterControls();

                            usedControls = oldUsedControls;
                            bindMap = oldBindMap;
                            ReregisterControls();
                        }
                        else
                            return true;
                    }

                    return false;
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
                private void RegisterBindToCombo(Bind bind, IReadOnlyList<IControl> newCombo)
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
                        wasBindChanged = true;
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
                    wasBindChanged = true;
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
                        List<IControl> combo = keyBinds[x].GetCombo();
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
                public BindDefinitionData[] GetBindData()
                {
                    BindDefinitionData[] bindData = new BindDefinitionData[keyBinds.Count];
                    string[][] combos = new string[keyBinds.Count][];

                    for (int x = 0; x < keyBinds.Count; x++)
                    {
                        List<IControl> combo = keyBinds[x].GetCombo();
                        combos[x] = new string[combo.Count];

                        for (int y = 0; y < combo.Count; y++)
                            combos[x][y] = combo[y].Name;
                    }

                    for (int n = 0; n < keyBinds.Count; n++)
                        bindData[n] = new BindDefinitionData(keyBinds[n].Name, combos[n]);

                    return bindData;
                }

                /// <summary>
                /// Returns true if the given list of controls conflicts with any existing binds.
                /// </summary>
                public bool DoesComboConflict(IReadOnlyList<IControl> newCombo, IBind exception = null) =>
                    DoesComboConflict(BindManager.GetComboIndices(newCombo), (exception != null) ? exception.Index : -1);

                /// <summary>
                /// Determines if given combo is equivalent to any existing binds.
                /// </summary>
                public bool DoesComboConflict(IReadOnlyList<int> newCombo, int exception = -1)
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

                public IEnumerator<IBind> GetEnumerator() =>
                    keyBinds.GetEnumerator();

                IEnumerator IEnumerable.GetEnumerator() =>
                    keyBinds.GetEnumerator();
            }
        }
    }
}