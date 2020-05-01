﻿using RichHudFramework.Internal;
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
    namespace UI.Server
    {
        public sealed partial class BindManager
        {
            private partial class BindGroup
            {
                /// <summary>
                /// Logic and data for individual keybinds
                /// </summary>
                private class Bind : IBind
                {
                    /// <summary>
                    /// Invoked when the bind is first pressed.
                    /// </summary>
                    public event Action OnNewPress;

                    /// <summary>
                    /// Invoked after the bind has been held and pressed for at least 500ms.
                    /// </summary>
                    public event Action OnPressAndHold;

                    /// <summary>
                    /// Invoked after the bind has been released.
                    /// </summary>
                    public event Action OnRelease;

                    /// <summary>
                    /// Name of the keybind
                    /// </summary>
                    public string Name { get; }

                    /// <summary>
                    /// Index of the bind within its group
                    /// </summary>
                    public int Index { get; }

                    /// <summary>
                    /// True if any controls in the bind are marked analog. For these types of binds, IsPressed == IsNewPressed.
                    /// </summary>
                    public bool Analog { get; set; }

                    /// <summary>
                    /// True if currently pressed.
                    /// </summary>
                    public bool IsPressed { get; private set; }

                    /// <summary>
                    /// True if just pressed.
                    /// </summary>
                    public bool IsNewPressed { get { return IsPressed && (!wasPressed || Analog); } }

                    /// <summary>
                    /// True after being held for more than 500ms.
                    /// </summary>
                    public bool IsPressedAndHeld { get; private set; }

                    /// <summary>
                    /// True if just released.
                    /// </summary>
                    public bool IsReleased { get { return !IsPressed && wasPressed; } }

                    public bool beingReleased;
                    public int length, bindHits;

                    private bool wasPressed;
                    private readonly Utils.Stopwatch stopwatch;
                    private readonly BindGroup group;

                    public Bind(string name, int index, BindGroup group)
                    {
                        Name = name;
                        Index = index;
                        stopwatch = new Utils.Stopwatch();
                        this.group = group;

                        IsPressedAndHeld = false;
                        wasPressed = false;

                        bindHits = 0;
                        Analog = false;
                        beingReleased = false;
                        length = 0;
                    }

                    /// <summary>
                    /// Used to update the key bind with each tick of the Binds.Update function. 
                    /// </summary>
                    public void UpdatePress(bool isPressed)
                    {
                        wasPressed = IsPressed;
                        IsPressed = isPressed;

                        if (IsNewPressed)
                        {
                            OnNewPress?.Invoke();
                            stopwatch.Start();
                        }

                        if (IsPressed && stopwatch.ElapsedTicks > holdTime)
                        {
                            if (!IsPressedAndHeld)
                                OnPressAndHold?.Invoke();

                            IsPressedAndHeld = true;
                        }
                        else
                            IsPressedAndHeld = false;

                        if (IsReleased)
                            OnRelease?.Invoke();
                    }

                    /// <summary>
                    /// Returns a list of the current key combo for this bind.
                    /// </summary>
                    public IList<IControl> GetCombo()
                    {
                        List<IControl> combo = new List<IControl>();

                        foreach (IControl con in group.usedControls)
                        {
                            if (group.BindUsesControl(this, con))
                                combo.Add(con);
                        }

                        return combo;
                    }

                    private List<int> GetComboIndices()
                    {
                        List<int> combo = new List<int>();

                        foreach (IControl con in group.usedControls)
                        {
                            if (group.BindUsesControl(this, con))
                                combo.Add(con.Index);
                        }

                        return combo;
                    }

                    /// <summary>
                    /// Tries to update a key bind using the given control combination.
                    /// </summary>
                    public bool TrySetCombo(IList<int> combo, bool strict = true, bool silent = true) =>
                        TrySetCombo(BindManager.GetCombo(combo), strict, silent);

                    /// <summary>
                    /// Tries to update a key bind using the given control combination.
                    /// </summary>
                    public bool TrySetCombo(IList<string> combo, bool strict = true, bool silent = true) =>
                        TrySetCombo(BindManager.GetCombo(combo), strict, silent);

                    /// <summary>
                    /// Tries to update a key bind using the given control combination.
                    /// </summary>
                    public bool TrySetCombo(IList<IControl> combo, bool strict = true, bool silent = true)
                    {
                        if (combo == null)
                            combo = new IControl[0];

                        if (combo.Count <= maxBindLength && (!strict || combo.Count > 0))
                        {
                            if (!strict || !group.DoesComboConflict(combo, this))
                            {
                                group.RegisterBindToCombo(this, combo);
                                return true;
                            }
                            else if (!silent)
                                ExceptionHandler.SendChatMessage($"Invalid bind for {group.Name}.{Name}. One or more of the given controls conflict with existing binds.");
                        }
                        else if (!silent)
                        {
                            if (combo.Count > 0)
                                ExceptionHandler.SendChatMessage($"Invalid key bind. No more than {maxBindLength} keys in a bind are allowed.");
                            else
                                ExceptionHandler.SendChatMessage("Invalid key bind. There must be at least one control in a key bind.");
                        }

                        return false;
                    }

                    /// <summary>
                    /// Clears all controls from the bind.
                    /// </summary>
                    public void ClearCombo() =>
                        group.UnregisterBindFromCombo(this);

                    /// <summary>
                    /// Clears all even subscribers from the bind.
                    /// </summary>
                    public void ClearSubscribers()
                    {
                        OnNewPress = null;
                        OnPressAndHold = null;
                        OnRelease = null;
                    }

                    private object GetOrSetMember(object data, int memberEnum)
                    {
                        var member = (BindAccesssors)memberEnum;

                        if (member == BindAccesssors.Name)
                        {
                            return Name;
                        }
                        else if (member == BindAccesssors.Analog)
                        {
                            return Analog;
                        }
                        else if (member == BindAccesssors.Index)
                        {
                            return Index;
                        }
                        else if (member == BindAccesssors.OnNewPress)
                        {
                            var eventData = (MyTuple<bool, Action>)data;

                            if (eventData.Item1)
                                OnNewPress += eventData.Item2;
                            else
                                OnNewPress -= eventData.Item2;
                        }
                        else if (member == BindAccesssors.OnPressAndHold)
                        {
                            var eventData = (MyTuple<bool, Action>)data;

                            if (eventData.Item1)
                                OnPressAndHold += eventData.Item2;
                            else
                                OnPressAndHold -= eventData.Item2;
                        }
                        else if (member == BindAccesssors.OnRelease)
                        {
                            var eventData = (MyTuple<bool, Action>)data;

                            if (eventData.Item1)
                                OnRelease += eventData.Item2;
                            else
                                OnRelease -= eventData.Item2;
                        }
                        else if (member == BindAccesssors.GetCombo)
                            return GetComboIndices();
                        else if (member == BindAccesssors.SetCombo)
                        {
                            var comboData = (MyTuple<IList<int>, bool, bool>)data;
                            return TrySetCombo(comboData.Item1, comboData.Item2, comboData.Item3);
                        }
                        else if (member == BindAccesssors.ClearCombo)
                        {
                            ClearCombo();
                        }
                        else if (member == BindAccesssors.ClearSubscribers)
                            ClearSubscribers();

                        return null;
                    }

                    /// <summary>
                    /// Returns information needed to access the bind via the API.
                    /// </summary>
                    public BindMembers GetApiData()
                    {
                        return new BindMembers()
                        {
                            Item1 = GetOrSetMember,
                            Item2 = () => IsPressed,
                            Item3 = () => IsNewPressed,
                            Item4 = () => IsPressedAndHeld,
                            Item5 = () => IsReleased
                        };
                    }
                }
            }
        }
    }
}