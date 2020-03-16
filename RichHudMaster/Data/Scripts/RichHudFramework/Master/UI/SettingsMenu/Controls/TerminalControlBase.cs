using System;
using System.Collections.Generic;
using System.Text;
using VRage;
using ApiMemberAccessor = System.Func<object, int, object>;
using GlyphFormatMembers = VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>;

namespace RichHudFramework.UI.Server
{
    using ControlMembers = MyTuple<
        ApiMemberAccessor, // GetOrSetMember
        object // ID
    >;
    using RichStringMembers = MyTuple<StringBuilder, GlyphFormatMembers>;

    /// <summary>
    /// Base type for all controls in the Rich Hud Terminal.
    /// </summary>
    public abstract class TerminalControlBase : HudElementBase, IListBoxEntry, ITerminalControl
    {
        /// <summary>
        /// Invoked whenver a change occurs to a control that requires a response, like a change
        /// to a value.
        /// </summary>
        public abstract event Action OnControlChanged;

        /// <summary>
        /// The name of the control as it appears in the terminal.
        /// </summary>
        public abstract string Name { get; set; }

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
                            return Name;
                        else
                            Name = data as string;

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

    /// <summary>
    /// Clickable control used in conjunction by the settings menu.
    /// </summary>
    public abstract class TerminalControlBase<T> : TerminalControlBase, ITerminalControl<T> where T : TerminalControlBase<T>
    {
        /// <summary>
        /// Delegate invoked by OnControlChanged. Passes in a reference to the control.
        /// </summary>
        public Action<T> ControlChangedAction { get; set; }

        public TerminalControlBase(IHudParent parent) : base(parent)
        {
            OnControlChanged += UpdateControl;
        }

        private void UpdateControl() =>
            ControlChangedAction?.Invoke(this as T);

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
    /// Base type for settings menu controls associated with a value of a given type.
    /// </summary>
    public abstract class TerminalValue<TValue, TCon> : TerminalControlBase<TCon>, ITerminalValue<TValue, TCon> where TCon : TerminalControlBase<TCon>
    {
        /// <summary>
        /// Invoked whenver a change occurs to a control that requires a response, like a change
        /// to a value.
        /// </summary>
        public override event Action OnControlChanged;

        /// <summary>
        /// Value associated with the control.
        /// </summary>
        public virtual TValue Value { get; set; }

        /// <summary>
        /// Used to periodically update the value associated with the control. Optional.
        /// </summary>
        public abstract Func<TValue> CustomValueGetter { get; set; }

        private TValue lastValue;
        private bool controlUpdating;

        public TerminalValue(IHudParent parent) : base(parent)
        { }

        protected override void Layout()
        {
            if (Value != null && !Value.Equals(lastValue) && !controlUpdating)
            {
                controlUpdating = true;
                lastValue = Value;
                OnControlChanged?.Invoke();
                controlUpdating = false;
            }

            if (CustomValueGetter != null && !Value.Equals(CustomValueGetter()))
                Value = CustomValueGetter();
        }

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
                }
            }

            return null;
        }
    }
}