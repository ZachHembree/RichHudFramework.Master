using RichHudFramework.UI.Rendering;
using System;
using System.Text;
using VRage;
using GlyphFormatMembers = VRage.MyTuple<VRageMath.Vector2I, int, VRageMath.Color, float>;
using ApiMemberAccessor = System.Func<object, int, object>;

namespace RichHudFramework.UI.Server
{
    using RichStringMembers = MyTuple<StringBuilder, GlyphFormatMembers>;
    using ControlMembers = MyTuple<
        ApiMemberAccessor, // GetOrSetMember
        object // ID
    >;

    /// <summary>
    /// Abstract base for all controls in the settings menu accessible via the Framework API.
    /// </summary>
    public abstract class TerminalControlBase : HudElementBase, IListBoxEntry, ITerminalControl
    {
        /// <summary>
        /// Invoked whenver a change occurs to a control that requires a response, like a change
        /// to a value.
        /// </summary>
        public abstract event Action OnControlChanged;

        /// <summary>
        /// The name of the control as rendred in the terminal.
        /// </summary>
        public abstract RichText Name { get; set; }

        /// <summary>
        /// Determines whether or not the control should be visible in the terminal.
        /// </summary>
        public bool Enabled { get; set; }

        public TerminalControlBase(IHudParent parent) : base(parent)
        {
            Enabled = true;
        }

        /// <summary>
        /// Faciltates access to object members via the Framework API.
        /// </summary>
        public new ControlMembers GetApiData()
        {
            return new ControlMembers()
            {
                Item1 = GetOrSetMember,
                Item2 = this
            };
        }

        /// <summary>
        /// Used for accessing arbitrary control members via the Framework API.
        /// </summary>
        protected new virtual object GetOrSetMember(object data, int memberEnum)
        {
            var member = (TerminalControlAccessors)memberEnum;

            switch (member)
            {
                case TerminalControlAccessors.OnSettingChanged:
                    {
                        var eventData = (MyTuple<bool, Action>)data;

                        if (eventData.Item1)
                            OnControlChanged += eventData.Item2;
                        else
                            OnControlChanged -= eventData.Item2;

                        break;
                    }
                case TerminalControlAccessors.Name:
                    {
                        if (data == null)
                            return Name.GetApiData();
                        else
                            Name = new RichText((RichStringMembers[])data);

                        break;
                    }
                case TerminalControlAccessors.Enabled:
                    {
                        if (data == null)
                            return Enabled;
                        else
                            Enabled = (bool)data;

                        break;
                    }
            }

            return null;
        }
    }

    public abstract class TerminalControlBase<T> : TerminalControlBase, ITerminalControl<T> where T : TerminalControlBase<T>
    {
        /// <summary>
        /// Delegate invoked by OnControlChanged. Passes in a reference of type calling.
        /// </summary>
        public Action<T> ControlChangedAction { get; set; }

        public TerminalControlBase(IHudParent parent) : base(parent)
        {
            OnControlChanged += UpdateControl;
        }

        protected virtual void UpdateControl()
        {
            ControlChangedAction?.Invoke(this as T);
        }

        /// <summary>
        /// Faciltates access to object members via the Framework API.
        /// </summary>
        public new ControlMembers GetApiData()
        {
            return new ControlMembers()
            {
                Item1 = GetOrSetMember,
                Item2 = this
            };
        }
    }

    /// <summary>
    /// Abstract base for all settings menu controls associated with a given type of value.
    /// </summary>
    public abstract class TerminalValue<TValue, TCon> : TerminalControlBase<TCon>, ITerminalValue<TValue, TCon> where TCon : TerminalControlBase<TCon>
    {
        /// <summary>
        /// Value associated with the control.
        /// </summary>
        public virtual TValue Value { get; set; }

        public abstract Func<TValue> CustomValueGetter { get; set; }

        public abstract Action<TValue> CustomValueSetter { get; set; }

        private TValue value;

        public TerminalValue(IHudParent parent) : base(parent)
        { }

        protected override object GetOrSetMember(object data, int memberEnum)
        {
            if (memberEnum < 8)
                return base.GetOrSetMember(data, memberEnum);
            else
            {
                switch ((TerminalControlAccessors)memberEnum)
                {
                    case TerminalControlAccessors.Value:
                        {
                            if (data == null)
                                return Value;
                            else
                                Value = (TValue)data;

                            break;
                        }
                    case TerminalControlAccessors.ValueGetter:
                        {
                            if (data == null)
                                return CustomValueGetter;
                            else
                                CustomValueGetter = data as Func<TValue>;

                            break;
                        }
                    case TerminalControlAccessors.ValueSetter:
                        {
                            if (data == null)
                                return CustomValueSetter;
                            else
                                CustomValueSetter = data as Action<TValue>;

                            break;
                        }
                }
            }

            return null;
        }
    }
}