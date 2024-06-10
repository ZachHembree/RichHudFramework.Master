using RichHudFramework.Internal;
using System;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;

namespace RichHudFramework
{
    namespace UI.Server
    {
        public sealed partial class BindManager
        {
            public partial class BindGroup
            {
                private class KeyCombo
                {
                    /// <summary>
                    /// True if any controls in the bind are marked analog. For these types of binds, IsPressed == IsNewPressed.
                    /// </summary>
                    public bool Analog { get; set; }

                    /// <summary>
                    /// Analog value of the bind, if it has one. Returns the sum of all analog values in
                    /// key combo. Multiple analog controls per bind are not recommended.
                    /// </summary>
                    public float AnalogValue { get; set; }

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

                    /// <summary>
                    /// Number of keys in the combo
                    /// </summary>
                    public int length;
                    
                    /// <summary>
                    /// Number of keys last pressed in the combo
                    /// </summary>
                    public int bindHits;

                    private bool wasPressed;
                    private readonly Stopwatch stopwatch;

                    public KeyCombo()
                    {
                        stopwatch = new Stopwatch();
                        Reset();
                    }

                    public void Reset()
                    {
                        IsPressedAndHeld = false;
                        wasPressed = false;

                        Analog = false;
                        beingReleased = false;
                        bindHits = 0;
                        length = 0;
                    }

                    public void Update(bool isPressed)
                    {
                        wasPressed = IsPressed;
                        IsPressed = isPressed;

                        if (!isPressed)
                            AnalogValue = 0f;

                        if (IsNewPressed)
                        {
                            stopwatch.Restart();
                        }

                        if (IsPressed && stopwatch.ElapsedTicks > holdTime)
                        {
                            IsPressedAndHeld = true;
                        }
                        else
                            IsPressedAndHeld = false;
                    }
                }

                /// <summary>
                /// Logic and data for individual keybinds
                /// </summary>
                private class Bind : IBind
                {
                    /// <summary>
                    /// Invoked when the bind is first pressed.
                    /// </summary>
                    public event EventHandler NewPressed;

                    /// <summary>
                    /// Invoked after the bind has been held and pressed for at least 500ms.
                    /// </summary>
                    public event EventHandler PressedAndHeld;

                    /// <summary>
                    /// Invoked after the bind has been released.
                    /// </summary>
                    public event EventHandler Released;

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
                    public bool Analog { get; private set; }

                    /// <summary>
                    /// Analog value of the bind, if it has one. Returns the sum of all analog values in
                    /// key combo. Multiple analog controls per bind are not recommended.
                    /// </summary>
                    public float AnalogValue { get; private set; }

                    /// <summary>
                    /// True if currently pressed.
                    /// </summary>
                    public bool IsPressed { get; private set; }

                    /// <summary>
                    /// True if just pressed.
                    /// </summary>
                    public bool IsNewPressed { get; private set; }

                    /// <summary>
                    /// True after being held for more than 500ms.
                    /// </summary>
                    public bool IsPressedAndHeld { get; private set; }

                    /// <summary>
                    /// True if just released.
                    /// </summary>
                    public bool IsReleased { get; private set; }

                    private readonly BindGroup group;

                    public Bind(string name, int index, BindGroup group)
                    {
                        Name = name;
                        Index = index;
                        this.group = group;

                        IsPressed = false;
                        IsNewPressed = false;
                        IsPressedAndHeld = false;
                        IsReleased = false;
                        Analog = false;
                        AnalogValue = 0f;
                    }

                    /// <summary>
                    /// Used to update the key bind with each tick of the Binds.Update function. 
                    /// </summary>
                    public bool Update(KeyCombo combo)
                    {
                        Analog = combo.Analog;
                        AnalogValue = combo.AnalogValue;
                        IsPressed = combo.IsPressed;
                        IsNewPressed = combo.IsNewPressed;
                        IsPressedAndHeld = combo.IsPressedAndHeld;
                        IsReleased = combo.IsReleased;

                        if (IsNewPressed)
                            NewPressed?.Invoke(this, EventArgs.Empty);

                        if (IsReleased)
                            Released?.Invoke(this, EventArgs.Empty);

                        if (IsPressedAndHeld)
                            PressedAndHeld?.Invoke(this, EventArgs.Empty);

                        return combo.IsPressed || combo.IsNewPressed || combo.IsReleased || combo.IsPressedAndHeld;
                    }

                    /// <summary>
                    /// Returns a list of the current key combo for this bind.
                    /// </summary>
                    public List<IControl> GetCombo()
                    {
                        group.GetBindCombo(_instance.conIDbuf, Index, 0);
                        var controls = new List<IControl>(_instance.conIDbuf.Count);

                        foreach (int conID in _instance.conIDbuf)
                            controls.Add(_instance.controls[conID]);

                        return controls;
                    }

                    public List<int> GetComboIndices()
                    {
                        group.GetBindCombo(_instance.conIDbuf, Index, 0);
                        return new List<int>(_instance.conIDbuf);
                    }

                    /// <summary>
                    /// Tries to update a key bind using the given control combination.
                    /// </summary>
                    public bool TrySetCombo(IReadOnlyList<int> combo, bool strict = true, bool silent = true) =>
                        TrySetCombo(BindManager.GetCombo(combo), strict, silent);

                    /// <summary>
                    /// Tries to update a key bind using the given control combination.
                    /// </summary>
                    public bool TrySetCombo(IReadOnlyList<string> combo, bool strict = true, bool silent = true) =>
                        TrySetCombo(BindManager.GetCombo(combo), strict, silent);

                    /// <summary>
                    /// Tries to update a key bind using the given control combination.
                    /// </summary>
                    public bool TrySetCombo(IReadOnlyList<IControl> combo, bool strict = true, bool silent = true)
                    {
                        if (combo == null)
                            combo = new IControl[0];

                        for (int i = 0; i < combo.Count; i++)
                        {
                            if (combo[i] == null || combo[i] == Control.Default)
                            {
                                if (!silent)
                                    ExceptionHandler.SendChatMessage($"Invalid bind for {group.Name}.{Name}. Key name not recognised.");

                                return false;
                            }
                        }

                        if (combo.Count <= maxBindLength && (!strict || combo.Count > 0))
                        {
                            if (!strict || !group.DoesComboConflict(combo, this))
                            {
                                group.TrySetBindInternal(Index, BindManager.GetComboIndices(combo));
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
                        group.ResetBindInternal(Index);

                    /// <summary>
                    /// Clears all even subscribers from the bind.
                    /// </summary>
                    public void ClearSubscribers()
                    {
                        NewPressed = null;
                        PressedAndHeld = null;
                        Released = null;
                    }

                    public override string ToString()
                    {
                        var sb = new StringBuilder();
                        var combo = GetCombo();

                        sb.Append(Name);
                        sb.Append(": ");

                        if (combo.Count > 0)
                            sb.Append(combo[0].ToString());

                        for (int i = 1; i < combo.Count; i++)
                        {
                            sb.Append(", ");
                            sb.Append(combo[i].ToString());                       
                        }

                        return sb.ToString();
                    }
                }
            }
        }
    }
}