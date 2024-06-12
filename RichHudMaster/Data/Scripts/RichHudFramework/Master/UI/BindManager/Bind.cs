using RichHudFramework.Internal;
using System;
using System.Text;
using System.Collections.Generic;

namespace RichHudFramework
{
    namespace UI.Server
    {
        public sealed partial class BindManager
        {
            public partial class BindGroup
            {
                /// <summary>
                /// Input tied to one or more key combinations
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
                    /// Updates the state of the bind
                    /// </summary>
                    public bool Update(KeyCombo combo)
                    {
                        IsPressed = combo.IsPressed;
                        IsNewPressed = combo.IsNewPressed;
                        IsPressedAndHeld = combo.IsPressedAndHeld;
                        IsReleased = combo.IsReleased;

                        Analog = combo.Analog;
                        AnalogValue = combo.AnalogValue;

                        if (IsNewPressed)
                            NewPressed?.Invoke(this, EventArgs.Empty);

                        if (IsReleased)
                            Released?.Invoke(this, EventArgs.Empty);

                        if (IsPressedAndHeld)
                            PressedAndHeld?.Invoke(this, EventArgs.Empty);

                        return IsPressed || IsNewPressed || IsPressedAndHeld || IsReleased;
                    }

                    /// <summary>
                    /// Returns a list of controls representing the binds key combo
                    /// </summary>
                    public List<IControl> GetCombo()
                    {
                        group.TryGetBindCombo(_instance.conIDbuf, Index, 0);
                        var controls = new List<IControl>(_instance.conIDbuf.Count);

                        foreach (int conID in _instance.conIDbuf)
                            controls.Add(_instance.controls[conID]);

                        return controls;
                    }

                    public List<int> GetComboIndices()
                    {
                        group.TryGetBindCombo(_instance.conIDbuf, Index, 0);
                        return new List<int>(_instance.conIDbuf);
                    }

                    /// <summary>
                    /// Tries to update a key bind using the given control combination.
                    /// </summary>
                    public bool TrySetCombo(IReadOnlyList<int> combo, int alias = 0, bool isStrict = true, bool isSilent = true)
                    {
                        var controls = _instance.controls;

                        for (int i = 0; i < combo.Count; i++)
                        {
                            if (combo[i] == 0 || combo[i] < 0 || combo[i] >= controls.Length)
                            {
                                if (!isSilent)
                                    ExceptionHandler.SendChatMessage(
                                        $"Invalid key bind for {group.Name}.{Name}. " +
                                        $"Key in position {i + 1} not recognised.");

                                return false;
                            }
                        }

                        var buf = _instance.conIDbuf;

                        if (buf != combo)
                        {
                            buf.Clear();
                            buf.AddRange(combo);
                        }

                        SanitizeCombo(buf);

                        if (buf.Count <= maxBindLength && (!isStrict || buf.Count > 0))
                        {
                            bool success = group.TrySetBindInternal(Index, buf, alias, isStrict);

                            if (!success && !isSilent)
                                ExceptionHandler.SendChatMessage($"Invalid key bind for {group.Name}.{Name}. " +
                                $"One or more of the given controls conflict with existing binds.");

                            return success;
                        }
                        else if (!isSilent)
                        {
                            if (buf.Count > 0)
                            {
                                ExceptionHandler.SendChatMessage(
                                    $"Invalid key bind. No more than {maxBindLength} keys in a bind are allowed.");
                            }
                            else
                            {
                                ExceptionHandler.SendChatMessage(
                                    "Invalid key bind. There must be at least one control in a key bind.");
                            }
                        }

                        return false;
                    }

                    /// <summary>
                    /// Tries to update a key bind using the given control combination.
                    /// </summary>
                    public bool TrySetCombo(IReadOnlyList<string> combo, int alias = 0, bool isStrict = true, bool isSilent = true)
                    {
                        var buf = _instance.conIDbuf;
                        BindManager.GetComboIndices(combo, buf, false);

                        for (int i = 0; i < combo.Count; i++)
                        {
                            if (buf[i] == 0)
                            {
                                ExceptionHandler.SendChatMessage(
                                    $"Invalid key bind for {group.Name}.{Name}. " +
                                    $"Key name '{combo[i]}' not recognised.");

                                return false;
                            }
                        }

                        return TrySetCombo(buf, alias, isStrict, isSilent);
                    }
                        
                    /// <summary>
                    /// Tries to update a key bind using the given control combination.
                    /// </summary>
                    public bool TrySetCombo(IReadOnlyList<IControl> combo, int alias = 0, bool isStrict = true, bool isSilent = true)
                    {
                        var buf = _instance.conIDbuf;
                        BindManager.GetComboIndices(combo, buf, false);
                        return TrySetCombo(buf, alias, isStrict, isSilent);
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