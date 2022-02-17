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

                    /// <summary>
                    /// Used for for bind input disambiguation. Binds in the process of being released are
                    /// counted as full presses for one tick, but only for bind disambiguation.
                    /// </summary>
                    public bool beingReleased;
                    public int length, bindHits;

                    private bool wasPressed;
                    private readonly Stopwatch stopwatch;
                    private readonly BindGroup group;

                    public Bind(string name, int index, BindGroup group)
                    {
                        Name = name;
                        Index = index;
                        stopwatch = new Stopwatch();
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
                            NewPressed?.Invoke(this, EventArgs.Empty);
                            stopwatch.Restart();
                        }

                        if (IsPressed && stopwatch.ElapsedTicks > holdTime)
                        {
                            if (!IsPressedAndHeld)
                                PressedAndHeld?.Invoke(this, EventArgs.Empty);

                            IsPressedAndHeld = true;
                        }
                        else
                            IsPressedAndHeld = false;

                        if (IsReleased)
                            Released?.Invoke(this, EventArgs.Empty);
                    }

                    /// <summary>
                    /// Returns a list of the current key combo for this bind.
                    /// </summary>
                    public List<IControl> GetCombo()
                    {
                        List<IControl> combo = new List<IControl>();

                        foreach (IControl con in group.usedControls)
                        {
                            if (group.BindUsesControl(this, con))
                                combo.Add(con);
                        }

                        combo.Sort((a, b) => b.Index.CompareTo(a.Index));
                        return combo;
                    }

                    public List<int> GetComboIndices()
                    {
                        List<int> combo = new List<int>();

                        foreach (IControl con in group.usedControls)
                        {
                            if (group.BindUsesControl(this, con))
                                combo.Add(con.Index);
                        }

                        combo.Sort((a, b) => b.CompareTo(a));
                        return combo;
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
                            if (combo[i] == null)
                            {
                                if (!silent)
                                    ExceptionHandler.SendChatMessage($"Invalid bind for {group.Name}.{Name}. Control supplied at index {i} does not exist.");

                                return false;
                            }
                        }

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