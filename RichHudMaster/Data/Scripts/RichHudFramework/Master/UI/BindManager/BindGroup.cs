using System;
using System.Collections;
using System.Collections.Generic;
using VRage;
using VRageMath;
using RichHudFramework.Internal;
using BindDefinitionData = VRage.MyTuple<string, string[]>;
using System.CodeDom;

namespace RichHudFramework
{
    namespace UI.Server
    {
        public sealed partial class BindManager
        {
            public const int MaxBindLength = 3;

            /// <summary>
            /// A collection of unique keybinds.
            /// </summary>
            public partial class BindGroup : IBindGroup
            {
                private const long holdTime = TimeSpan.TicksPerMillisecond * 500;

                public event EventHandler BindChanged;

                /// <summary>
                /// Name assigned to the bind group
                /// </summary>
                public string Name { get; }

                /// <summary>
                /// Retrieves the bind at the specified index
                /// </summary>
                public IBind this[int index] => binds[index];

                /// <summary>
                /// Retrieves the bind with the given name
                /// </summary>
                public IBind this[string name] => GetBind(name);

                /// <summary>
                /// Returns the number of binds in the group
                /// </summary>
                public int Count => binds.Count;

                /// <summary>
                /// Index of the bind group in its associated client
                /// </summary>
                public int Index { get; }

                /// <summary>
                /// Unique identifier
                /// </summary>
                public object ID => this;

                // parallel
                private readonly Control[] controls;
                private readonly List<int>[] controlComboMap; // X = controls -> Y = associated combos

                // parallel
                private List<int> usedControls; // control indices
                private List<List<int>> usedControlComboMap; // X = usedControls -> Y = KeyCombo indices

                // parallel
                private readonly List<Bind> binds;
                private readonly List<List<int>> bindCombos; // x = bind -> y = combo/alias

                private readonly List<KeyCombo> keyCombos;
                private bool wasBindChanged;

                public BindGroup(int index, string name)
                {
                    Name = name;
                    Index = index;

                    controls = _instance.controls;
                    controlComboMap = new List<int>[controls.Length];

                    for (int n = 0; n < controlComboMap.Length; n++)
                    {
                        if (controls[n].Index != 0)
                            controlComboMap[n] = new List<int>();
                    }

                    usedControls = new List<int>();
                    usedControlComboMap = new List<List<int>>();

                    keyCombos = new List<KeyCombo>();

                    binds = new List<Bind>();
                    bindCombos = new List<List<int>>();

                    wasBindChanged = false;
                }

                /// <summary>
                /// Clears bind subscribers for the entire group
                /// </summary>
                public void ClearSubscribers()
                {
                    foreach (Bind bind in binds)
                        bind.ClearSubscribers();
                }

                /// <summary>
                /// Updates input state
                /// </summary>
                public void HandleInput()
                {
                    if (keyCombos.Count > 0)
                    {
                        int controlsPressed = GetPressedControls();
                        bool canUpdateBinds = true;

                        if (controlsPressed > 0)
                        {
                            int bindsPressed = GetPressedCombos();

                            if (bindsPressed > 1)
                                DisambiguatePresses();
                        }
                        else
                            canUpdateBinds = false;

                        // Update combos
                        foreach (KeyCombo combo in keyCombos)
                        {
                            combo.beingReleased = combo.beingReleased && combo.bindHits > 0;
                            combo.Update(canUpdateBinds && (combo.length > 0 && combo.bindHits == combo.length && !combo.beingReleased));
                        }

                        // Update binds
                        for (int i = 0; i < binds.Count; i++)
                        {
                            foreach (int comboID in bindCombos[i])
                            {
                                // Stop after first combo with an update
                                if (binds[i].Update(keyCombos[comboID]))
                                    break;
                            }
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

                    foreach (int conID in usedControls)
                    {
                        if (controls[conID].IsNewPressed)
                        {
                            anyNewPresses = true;
                            break;
                        }
                    }

                    foreach (KeyCombo combo in keyCombos)
                    {
                        // If any used controls have new presses, stop releasing
                        combo.beingReleased = combo.beingReleased && !anyNewPresses;
                        combo.bindHits = 0;
                        combo.AnalogValue = 0f;
                    }

                    foreach (int conID in usedControls)
                    {
                        var con = controls[conID];

                        if (con.IsPressed)
                        {
                            foreach (int comboID in controlComboMap[conID])
                            {
                                var combo = keyCombos[comboID];
                                combo.bindHits++;

                                if (con.Analog)
                                    combo.AnalogValue += con.AnalogValue;
                            }

                            controlsPressed++;
                        }
                    }

                    return controlsPressed;
                }

                /// <summary>
                /// Finds and counts number of pressed key binds.
                /// </summary>
                private int GetPressedCombos()
                {
                    int bindsPressed = 0;

                    // Partial presses on previously pressed binds count as full presses.
                    foreach (KeyCombo combo in keyCombos)
                    {
                        if (combo.IsPressed || combo.beingReleased)
                        {
                            if (combo.bindHits > 0 && combo.bindHits < combo.length)
                            {
                                combo.bindHits = combo.length;
                                combo.beingReleased = true;
                            }
                        }

                        if (combo.length > 0 && combo.bindHits == combo.length)
                            bindsPressed++;
                        else
                            combo.bindHits = 0;
                    }

                    return bindsPressed;
                }

                /// <summary>
                /// Resolves conflicts between pressed combos with shared controls.
                /// </summary>
                private void DisambiguatePresses()
                {
                    KeyCombo first, longest;
                    int controlHits;

                    // If more than one pressed bind shares the same control, the longest
                    // combos take precedence. Any combos shorter than the longest will not
                    // be counted as being pressed.
                    foreach (int conID in usedControls)
                    {
                        var con = controls[conID];
                        first = null;
                        controlHits = 0;
                        longest = GetLongestBindPressForControl(conID);

                        foreach (int comboID in controlComboMap[conID])
                        {
                            var combo = keyCombos[comboID];

                            if (combo.bindHits > 0 && (combo != longest))
                            {
                                if (controlHits > 0)
                                    combo.bindHits--;
                                else if (controlHits == 0)
                                    first = combo;

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
                private KeyCombo GetLongestBindPressForControl(int conID)
                {
                    KeyCombo longest = null;

                    foreach (int comboID in controlComboMap[conID])
                    {
                        var combo = keyCombos[comboID];

                        if (combo.bindHits > 0 && (
                            longest == null || combo.length > longest.length ||
                            (longest.beingReleased && !combo.beingReleased && longest.length == combo.length)
                        ))
                        {
                            longest = combo;
                        }
                    }

                    return longest;
                }

                /// <summary>
                /// Attempts to register a set of binds with the given names.
                /// </summary>
                public void RegisterBinds(IReadOnlyList<string> bindNames)
                {
                    foreach (string name in bindNames)
                        AddBindInternal(name);
                }

                /// <summary>
                /// Attempts to register a set of binds with the given names.
                /// </summary>
                public void RegisterBinds(BindGroupInitializer bindData)
                {
                    foreach (var bind in bindData)
                        AddBindInternal(bind.Item1, bind.Item2, bind.Item3);
                }

                /// <summary>
                /// Attempts to register a set of binds with the given names.
                /// </summary>
                public void RegisterBinds(IReadOnlyList<BindDefinitionData> bindData)
                {
                    var buf = _instance.conIDbuf;

                    foreach (var bind in bindData)
                    {
                        GetComboIndices(bind.Item2, buf, false);
                        AddBindInternal(bind.Item1, buf);
                    }
                }

                /// <summary>
                /// Attempts to register a set of binds with the given names.
                /// </summary>
                public void RegisterBinds(IReadOnlyList<MyTuple<string, IReadOnlyList<int>>> bindData)
                {
                    foreach (var bind in bindData)
                        AddBindInternal(bind.Item1, bind.Item2);
                }

                /// <summary>
                /// Adds a bind with the given name and the given key combo. Throws an exception if the bind is invalid.
                /// </summary>
                public IBind AddBind(string bindName, IReadOnlyList<string> combo = null)
                {
                    var buf = _instance.conIDbuf;
                    GetComboIndices(combo, buf, false);
                    return AddBindInternal(bindName, buf);
                }

                /// <summary>
                /// Adds a bind with the given name and the given key combo. Throws an exception if the bind is invalid.
                /// </summary>
                public IBind AddBind(string bindName, IReadOnlyList<ControlHandle> combo = null, IReadOnlyList<IReadOnlyList<ControlHandle>> aliases = null)
                {
                    var hBuf = _instance.cHandleBuf;

                    ResetConHandleBuf();
                    AddHandlesToConBuf(combo, aliases);

                    return AddBindInternal(bindName, hBuf.Item1, hBuf.Item2);
                }

                /// <summary>
                /// Adds a bind with the given name and the given key combo. Throws an exception if the bind is invalid.
                /// </summary>
                public IBind AddBind(string bindName, IReadOnlyList<int> combo, IReadOnlyList<IReadOnlyList<int>> aliases = null) =>
                    AddBindInternal(bindName, combo, aliases);

                /// <summary>
                /// Adds a bind with the given name and the given key combo. Throws an exception if the bind is invalid.
                /// </summary>
                private IBind AddBindInternal(string bindName, IReadOnlyList<int> combo = null, IReadOnlyList<IReadOnlyList<int>> aliases = null)
                {
                    IBind bind = null;

                    if (TryRegisterBindInternal(bindName, out bind, combo, aliases))
                        return bind;
                    else
                        throw new Exception($"Attempted to add an invalid bind '{bindName}' to group {Name}.");
                }

                /// <summary>
                /// Tries to register a bind using the given name and the given key combo.
                public bool TryRegisterBind(string bindName, out IBind newBind) =>
                    TryRegisterBindInternal(bindName, out newBind);

                /// <summary>
                /// Tries to register a new bind using the given name and the given key combo.
                /// </summary>
                public bool TryRegisterBind(string bindName, out IBind newBind, IReadOnlyList<int> combo, IReadOnlyList<IReadOnlyList<int>> aliases = null) =>                    
                    TryRegisterBindInternal(bindName, out newBind, combo, aliases);

                /// <summary>
                /// Tries to register a bind using the given name and the given key combo.
                /// </summary>
                public bool TryRegisterBind(string bindName, out IBind newBind, IReadOnlyList<ControlHandle> combo, IReadOnlyList<IReadOnlyList<ControlHandle>> aliases = null)
                {
                    var cBuf = new List<int>(combo.Count);
                    var aliasBuf = new List<List<int>>(aliases.Count);

                    foreach (ControlHandle con in combo)
                        cBuf.Add(con.id);

                    foreach (var alias in aliases)
                    {
                        var aliasCons = new List<int>();
                        aliasBuf.Add(aliasCons);

                        foreach (ControlHandle con in alias)
                            aliasCons.Add(con.id);
                    }

                    return TryRegisterBindInternal(bindName, out newBind, cBuf, aliasBuf);
                }
                  
                /// <summary>
                /// Replaces current bind combos with combos based on the given <see cref="BindDefinitionData"/>[]. Does not register new binds.
                /// </summary>
                public bool TryLoadBindData(IReadOnlyList<BindDefinitionData> bindData)
                {
                    if (bindData != null && bindData.Count > 0)
                    {
                        var buf = _instance.bindDefBuf;
                        buf.Clear();
                        buf.EnsureCapacity(bindData.Count);

                        foreach (BindDefinitionData bind in bindData)
                            buf.Add(bind);

                        return TryLoadBindData(buf);
                    }

                    return false;
                }

                /// <summary>
                /// Replaces current bind combos with combos based on the given <see cref="BindDefinition"/>[]. Does not register new binds.
                /// </summary>
                public bool TryLoadBindData(IReadOnlyList<BindDefinition> bindData)
                {
                    if (bindData != null && bindData.Count > 0)
                    {
                        List<int> oldUsedControls = usedControls;
                        List<List<int>> oldControlComboMap = usedControlComboMap;

                        foreach (int conID in usedControls)
                            controlComboMap[conID] = new List<int>();

                        usedControls = new List<int>(bindData.Count);
                        usedControlComboMap = new List<List<int>>(bindData.Count);

                        if (TrySetBinds(bindData))
                            return true;
                        else
                        {
                            usedControls = oldUsedControls;
                            usedControlComboMap = oldControlComboMap;

                            for (int n = 0; n < usedControls.Count; n++)
                                controlComboMap[usedControls[n]] = usedControlComboMap[n];

                            return false;
                        }
                    }
                    else
                        return false;
                }

                /// <summary>
                /// Attempts to assign bind combos from serialized data
                /// </summary>
                private bool TrySetBinds(IReadOnlyList<BindDefinition> bindData)
                {
                    var buf = _instance.conIDbuf;

                    foreach (BindDefinition bindDef in bindData)
                    {
                        IBind bind = GetBind(bindDef.name);
                        GetComboIndices(bindDef.controlNames, buf);

                        if (bind == null)
                        {
                            ExceptionHandler.WriteToLog(
                                $"Bind load error. '{bindDef.name}' in group {Name} was not recognised.");

                            return false;
                        }
                        else if (!TrySetBindInternal(bind.Index, buf, 0))
                        {
                            ExceptionHandler.WriteToLog(
                                $"Bind load error. {Name}.{bind.Name} could not be set.");

                            return false;
                        }

                        for (int i = 0; i < (bindDef.aliases?.Length ?? 0); i++)
                        {
                            var bindAlias = bindDef.aliases[i];

                            if (bindAlias.controlNames != null)
                            {
                                GetComboIndices(bindAlias.controlNames, buf);

                                if (!TrySetBindInternal(bind.Index, buf, i + 1))
                                {
                                    ExceptionHandler.WriteToLog(
                                        $"Bind load error. {Name}.{bind.Name} alias {i + 1} could not be set.");

                                    return false;
                                }
                            }
                        }
                    }

                    return true;
                }

                /// <summary>
                /// Tries to register a new bind using the given name and the given sanitized key combo
                /// </summary>
                private bool TryRegisterBindInternal(
                    string bindName, out IBind newBind, 
                    IReadOnlyList<int> controls = null, 
                    IReadOnlyList<IReadOnlyList<int>> aliasControls = null)
                {
                    newBind = null;

                    if (!DoesBindExist(bindName))
                    {
                        Bind bind = new Bind(bindName, binds.Count, this);

                        newBind = bind;
                        binds.Add(bind);
                        bindCombos.Add(new List<int>());

                        if (controls != null && controls.Count > 0)
                        {
                            if (TrySetBindInternal(bind.Index, controls, 0))
                            {
                                if (aliasControls != null)
                                {
                                    for (int i = 0; i < aliasControls.Count; i++)
                                    {
                                        if (!TrySetBindInternal(bind.Index, aliasControls[i], i + 1))
                                        {
                                            ExceptionHandler.WriteToLog($"Invalid alias for {Name}.{bind.Name}.");
                                            return false;
                                        }
                                    }
                                }

                                return true;
                            }
                            else
                            {
                                ExceptionHandler.WriteToLog($"Invalid key combo for {Name}.{bind.Name}.");
                            }
                        }
                        else
                            return true;
                    }

                    return false;
                }

                private bool TrySetBindInternal(int bindID, IReadOnlyList<int> controls, int alias = 0, bool isStrict = true)
                {
                    alias = MathHelper.Clamp(alias, 0, bindCombos?[bindID].Count ?? 0);
                    controls = GetSanitizedComboTemp(controls);

                    if (controls == null || controls.Count == 0)
                    {
                        ResetBindInternal(bindID);
                        return true;
                    }

                    int comboID;

                    if (alias < bindCombos[bindID].Count)
                    {
                        comboID = bindCombos[bindID][alias];
                    }
                    else
                    {
                        comboID = keyCombos.Count;
                        keyCombos.Add(new KeyCombo());
                        bindCombos[bindID].Add(comboID);
                    }

                    if (!isStrict || !DoesComboConflict(controls, comboID))
                    {
                        SetCombo(comboID, controls);
                        return true;
                    }
                    else
                        return false;
                }

                private void ResetBindInternal(int bindID, int alias = -1)
                {
                    if (alias == -1)
                    {
                        foreach (int comboID in bindCombos[bindID])
                            ResetCombo(comboID);
                    }
                    else if (alias < (bindCombos?[bindID].Count ?? 0))
                    {
                        ResetCombo(bindCombos[bindID][alias]);
                    }
                }

                /// <summary>
                /// Registers the given combo using the given controlIDs and pairs it with the given bind
                /// </summary>
                private void SetCombo(int comboID, IReadOnlyList<int> uniqueKeys)
                {
                    ResetCombo(comboID);

                    foreach (int conID in uniqueKeys)
                    {
                        List<int> assocComboIDs = controlComboMap[conID];

                        if (assocComboIDs.Count == 0)
                        {
                            usedControls.Add(conID);
                            usedControlComboMap.Add(assocComboIDs);
                        }

                        assocComboIDs.Add(comboID);

                        if (controls[conID].Analog)
                            keyCombos[comboID].Analog = true;
                    }

                    keyCombos[comboID].length = uniqueKeys.Count;
                    wasBindChanged = true;
                }

                /// <summary>
                /// Clears the controls from the given <see cref="KeyCombo"/> and resets it to its default
                /// state
                /// </summary>
                private void ResetCombo(int comboID)
                {
                    for (int i = usedControls.Count - 1; i >= 0; i--)
                    {
                        usedControlComboMap[i].Remove(comboID);

                        if (usedControlComboMap[i].Count == 0)
                        {
                            usedControlComboMap.RemoveAt(i);
                            usedControls.RemoveAt(i);
                        }
                    }

                    if (comboID < keyCombos.Count)
                        keyCombos[comboID].Reset();

                    wasBindChanged = true;
                }

                /// <summary>
                /// Retrieves key bind using its name.
                /// </summary>
                public IBind GetBind(string name)
                {
                    name = name.ToLower();

                    foreach (Bind bind in binds)
                        if (bind.Name.ToLower() == name)
                            return bind;

                    return null;
                }

                private bool TryGetBindCombo(List<int> controls, int bindID, int alias = 0)
                {
                    if (alias < bindCombos[bindID].Count)
                    {
                        // Get alias 0 controls
                        int comboID = bindCombos[bindID][alias];
                        controls.Clear();

                        for (int j = 0; j < usedControls.Count; j++)
                        {
                            int conID = usedControls[j];

                            if (usedControlComboMap[j].Contains(comboID))
                                controls.Add(conID);
                        }

                        controls.Sort();
                        return true;
                    }
                    else
                        return false;
                }

                /// <summary>
                /// Retrieves the set of key binds as an array of BindDefinition
                /// </summary>
                public BindDefinition[] GetBindDefinitions()
                {
                    BindDefinition[] bindData = new BindDefinition[binds.Count];
                    var cBuf = _instance.conIDbuf;
                    var controls = _instance.controls;

                    for (int bindID = 0; bindID < binds.Count; bindID++)
                    {
                        string[] mainCombo = null;
                        BindAliasDefinition[] aliases = null;

                        if (bindCombos[bindID].Count > 0)
                        {
                            // Get main combo
                            TryGetBindCombo(cBuf, bindID, 0);

                            // Retrieve control names
                            mainCombo = new string[cBuf.Count];

                            for (int j = 0; j < cBuf.Count; j++)
                                mainCombo[j] = controls[cBuf[j]].Name;

                            // Get aliases
                            int aliasCount = Math.Max(0, bindCombos.Count - 1);
                            aliases = new BindAliasDefinition[aliasCount];

                            for (int j = 0; j < aliasCount; j++)
                            {
                                if (TryGetBindCombo(cBuf, bindID, j + 1))
                                {
                                    // Get control names
                                    aliases[j].controlNames = new string[cBuf.Count];

                                    for (int k = 0; k < cBuf.Count; k++)
                                        aliases[j].controlNames[k] = controls[cBuf[k]].Name;
                                }
                            }
                        }

                        bindData[bindID] = new BindDefinition(binds[bindID].Name, mainCombo, aliases);
                    }

                    return bindData;
                }

                /// <summary>
                /// Retrieves the set of key binds as an array of BindDefinition
                /// </summary>
                public BindDefinitionData[] GetBindData()
                {
                    BindDefinitionData[] bindData = new BindDefinitionData[binds.Count];
                    var cBuf = _instance.conIDbuf;
                    var controls = _instance.controls;

                    for (int bindID = 0; bindID < binds.Count; bindID++)
                    {
                        string[] mainCombo = null;

                        if (bindCombos[bindID].Count > 0)
                        {
                            // Get main combo
                            TryGetBindCombo(cBuf, bindID, 0);

                            // Retrieve control names
                            mainCombo = new string[cBuf.Count];

                            for (int j = 0; j < cBuf.Count; j++)
                                mainCombo[j] = controls[cBuf[j]].Name;
                        }

                        bindData[bindID] = new BindDefinitionData(binds[bindID].Name, mainCombo);
                    }

                    return bindData;
                }

                /// <summary>
                /// Returns true if the given list of controls conflicts with any existing binds.
                /// </summary>
                public bool DoesComboConflict(IReadOnlyList<IControl> controls, IBind combo = null)
                {
                    var buf = _instance.conIDbuf;
                    GetComboIndices(controls, buf);
                    return DoesComboConflict(buf, (combo != null) ? combo.Index : -1);
                }

                /// <summary>
                /// Determines if given combo is equivalent to any existing binds.
                /// </summary>
                public bool DoesComboConflict(IReadOnlyList<int> controls, int comboID = -1)
                {
                    int matchCount;

                    for (int i = 0; i < keyCombos.Count; i++)
                    {
                        if (i != comboID && keyCombos[i].length == controls.Count)
                        {
                            matchCount = 0;

                            foreach (int conID in controls)
                            {
                                if (controlComboMap[conID].Contains(i))
                                    matchCount++;
                                else
                                    break;
                            }

                            if (matchCount > 0 && matchCount == controls.Count)
                                return true;
                        }
                    }

                    return false;
                }

                /// <summary>
                /// Returns true if a keybind with the given name exists.
                /// </summary>
                public bool DoesBindExist(string name)
                {
                    name = name.ToLower();

                    foreach (Bind bind in binds)
                        if (bind.Name.ToLower() == name)
                            return true;

                    return false;
                }

                public IEnumerator<IBind> GetEnumerator() =>
                    binds.GetEnumerator();

                IEnumerator IEnumerable.GetEnumerator() =>
                    binds.GetEnumerator();
            }
        }
    }
}