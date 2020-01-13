using System;
using System.Text;
using VRage;
using VRageMath;
using ApiMemberAccessor = System.Func<object, int, object>;
using GlyphFormatMembers = VRage.MyTuple<VRageMath.Vector2I, int, VRageMath.Color, float>;

namespace RichHudFramework.UI.Server
{
    using CollectionData = MyTuple<Func<int, ApiMemberAccessor>, Func<int>>;
    using RichStringMembers = MyTuple<StringBuilder, GlyphFormatMembers>;

    internal enum ListControlAccessors : int
    {
        ListAccessors = 16,
    }

    public class ListControl<T> : TerminalValue<ListBoxEntry<T>, ListControl<T>>
    {
        public override event Action OnControlChanged;

        public override RichText Name { get { return name.TextBoard.GetText(); } set { name.TextBoard.SetText(value); } }
        public override ListBoxEntry<T> Value { get { return List.Selection; } set { List.SetSelection(value); } }
        public override Func<ListBoxEntry<T>> CustomValueGetter { get; set; }
        public override Action<ListBoxEntry<T>> CustomValueSetter { get; set; }
        public ListBox<T> List { get; }

        public override float Width
        {
            get { return hudChain.Width; }
            set { hudChain.Width = value; }
        }

        public override float Height
        {
            get { return hudChain.Height; }
            set { List.Height = value - name.Height - Padding.Y; }
        }

        public override Vector2 Padding
        {
            get { return hudChain.Padding; }
            set { hudChain.Padding = value; }
        }

        /// <summary>
        /// Background color
        /// </summary>
        public Color Color { get { return List.Color; } set { List.Color = value; } }

        /// <summary>
        /// Color of the border box
        /// </summary>
        public Color BorderColor { get { return List.BorderColor; } set { List.BorderColor = value; } }

        /// <summary>
        /// Default format for member text;
        /// </summary>
        public GlyphFormat Format { get { return List.Format; } set { List.Format = value; name.Format = value; } }

        private readonly Label name;
        private readonly HudChain<HudElementBase> hudChain;
        private ListBoxEntry<T> lastValue;

        public ListControl(IHudParent parent = null) : base(parent)
        {
            name = new Label(this)
            {
                Format = RichHudTerminal.ControlFormat,
                Text = "NewListBox",
                AutoResize = false,
                Height = 24f,
            };

            List = new ListBox<T>(name)
            { };

            hudChain = new HudChain<HudElementBase>()
            {
                AutoResize = true,
                AlignVertical = true,
                ChildContainer = { name, List },
            };
        }

        protected override void Draw()
        {
            if (Value != lastValue)
            {
                lastValue = Value;
                OnControlChanged?.Invoke();
                CustomValueSetter?.Invoke(Value);
            }

            if (CustomValueGetter != null && Value != CustomValueGetter())
                Value = CustomValueGetter();

            base.Draw();
        }

        protected override object GetOrSetMember(object data, int memberEnum)
        {
            if (memberEnum < 16)
            {
                return base.GetOrSetMember(data, memberEnum);
            }
            else
            {
                var member = (ListControlAccessors)memberEnum;

                if (member == ListControlAccessors.ListAccessors)
                    return (ApiMemberAccessor)List.GetOrSetMember;

                return null;
            }
        }
    }
}