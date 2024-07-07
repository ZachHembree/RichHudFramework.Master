using System.Diagnostics;

namespace RichHudFramework
{
    namespace UI.Server
    {
        public sealed partial class BindManager
        {
            public partial class BindGroup
            {
                public class KeyCombo
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
                    public bool IsNewPressed { get; private set; }

                    /// <summary>
                    /// True after being held for more than 500ms.
                    /// </summary>
                    public bool IsPressedAndHeld { get; private set; }

                    /// <summary>
                    /// True if just released.
                    /// </summary>
                    public bool IsReleased { get; private set; }

                    /// <summary>
                    /// True if the bind was pressed, but now only partly pressed
                    /// </summary>
                    public bool isBeingReleased;

                    /// <summary>
                    /// True if any keys in combo are newly pressed
                    /// </summary>
                    public bool hasNewPresses;

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
                        isBeingReleased = false;
                        bindHits = 0;
                        length = 0;
                    }

                    public void Update(bool isPressed)
                    {
                        wasPressed = IsPressed;
                        IsPressed = isPressed;
                        IsNewPressed = IsPressed && hasNewPresses;
                        IsReleased = !IsPressed && wasPressed;

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
            }
        }
    }
}