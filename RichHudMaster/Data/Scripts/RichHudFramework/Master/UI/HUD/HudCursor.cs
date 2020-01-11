using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage;
using VRage.Utils;
using VRageMath;
using FloatProp = VRage.MyTuple<System.Func<float>, System.Action<float>>;
using RichStringMembers = VRage.MyTuple<System.Text.StringBuilder, VRage.MyTuple<VRageMath.Vector2I, int, VRageMath.Color, float>>;
using Vec2Prop = VRage.MyTuple<System.Func<VRageMath.Vector2>, System.Action<VRageMath.Vector2>>;
using ApiMemberAccessor = System.Func<object, int, object>;

namespace RichHudFramework
{
    using CursorMembers = MyTuple<
        Func<bool>, // Visible
        Func<bool>, // IsCaptured
        Func<Vector2>, // Origin
        Action<object>, // Capture
        Func<object, bool>, // IsCapturing
        MyTuple<
            Func<object, bool>, // TryCapture
            Func<object, bool>, // TryRelease
            ApiMemberAccessor // GetOrSetMember
        >
    >;

    namespace UI.Server
    {
        using Rendering;

        public sealed partial class HudMain
        {
            /// <summary>
            /// Draws cursor.
            /// </summary>
            private sealed class HudCursor : ICursor
            {
                public Vector2 Origin => shadow.Origin + shadow.Offset - new Vector2(12f, -12f) * ResScale;
                public bool Visible { get { return visible && MyAPIGateway.Gui.ChatEntryVisible; } set { visible = value; } }
                public bool IsCaptured => CapturedElement != null;
                public object CapturedElement { get; private set; }

                private bool visible;
                private readonly TexturedBox shadow, arrow;

                public HudCursor()
                {
                    shadow = new TexturedBox()
                    {
                        Material = new Material(MyStringId.GetOrCompute("RadialShadow"), new Vector2(32f, 32f)),
                        Color = new Color(0, 0, 0, 96),
                        Width = 64f,
                        Height = 64f,
                        Visible = true,
                    };

                    arrow = new TexturedBox(shadow)
                    {
                        Material = new Material(MyStringId.GetOrCompute("MouseCursor"), new Vector2(64f, 64f)),
                        Offset = new Vector2(-12f, 12f),
                        Width = 64f,
                        Height = 64f,
                        Visible = true
                    };
                }

                public bool IsCapturing(object possibleCaptive) =>
                    Visible && possibleCaptive == CapturedElement;

                public void Capture(object capturedElement)
                {
                    if (this.CapturedElement == null)
                        this.CapturedElement = capturedElement;
                }

                public bool TryCapture(object capturedElement)
                {
                    if (this.CapturedElement == null)
                    {
                        this.CapturedElement = capturedElement;
                        return true;
                    }
                    else
                        return false;
                }

                public bool TryRelease(object capturedElement)
                {
                    if (this.CapturedElement == capturedElement && capturedElement != null)
                    {
                        Release();
                        return true;
                    }
                    else
                        return false;
                }

                public void Release()
                {
                    CapturedElement = null;
                }

                public void Draw()
                {
                    if (Visible)
                    {
                        shadow.Scale = ResScale;
                        shadow.DrawStart();
                    }
                }

                public void HandleInput()
                {
                    if (Visible)
                    {
                        Vector2 pos = MyAPIGateway.Input.GetMousePosition();
                        shadow.Offset = new Vector2(pos.X - ScreenWidth / 2f, -(pos.Y - ScreenHeight / 2f)) + new Vector2(12f, -12f) * ResScale;
                    }
                }

                private object GetOrSetMember(object data, int memberEnum)
                {
                    return null;
                }

                public CursorMembers GetApiData()
                {
                    return new CursorMembers()
                    {
                        Item1 = () => Visible,
                        Item2 = () => IsCaptured,
                        Item3 = () => Origin,
                        Item4 = Capture,
                        Item5 = IsCapturing,
                        Item6 = new MyTuple<Func<object, bool>, Func<object, bool>, ApiMemberAccessor>()
                        {
                            Item1 = TryCapture,
                            Item2 = TryRelease,
                            Item3 = GetOrSetMember
                        }
                    };
                }
            }
        }
    }
}
