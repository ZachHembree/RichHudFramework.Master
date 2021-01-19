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
    public class TerminalControlBase : ScrollBoxEntry, ITerminalControl
    {
        /// <summary>
        /// Invoked whenver a change occurs to a control that requires a response, like a change
        /// to a value.
        /// </summary>
        public virtual event EventHandler ControlChanged;

        /// <summary>
        /// The name of the control as it appears in the terminal.
        /// </summary>
        public virtual string Name { get; set; }

        /// <summary>
        /// Unique identifer.
        /// </summary>
        public object ID => this;

        public EventHandler ControlChangedHandler { get; set; }

        protected Action ControlCallbackAction;

        public TerminalControlBase()
        {
            ControlChanged += InvokeApiCallback;
        }

        /// <summary>
        /// Used to update the internal state of the control.
        /// </summary>
        public virtual void Update()
        { }

        protected virtual void InvokeApiCallback(object sender, EventArgs args)
        {
            ControlCallbackAction?.Invoke();
            ControlChangedHandler?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Faciltates access to object members via the Framework API.
        /// </summary>
        public ControlMembers GetApiData()
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
        protected virtual object GetOrSetMember(object data, int memberEnum)
        {
            var member = (TerminalControlAccessors)memberEnum;

            switch (member)
            {
                case TerminalControlAccessors.GetOrSetControlCallback:
                    {
                        if (data == null)
                            return ControlCallbackAction;
                        else
                            ControlCallbackAction = data as Action;

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
    /// Base type for settings menu controls associated with a value of a given type.
    /// </summary>
    public abstract class TerminalValue<TValue> : TerminalControlBase, ITerminalValue<TValue>
    {
        /// <summary>
        /// Invoked whenver a change occurs to a control that requires a response, like a change
        /// to a value.
        /// </summary>
        public override event EventHandler ControlChanged;

        /// <summary>
        /// Value associated with the control.
        /// </summary>
        public virtual TValue Value { get; set; }

        /// <summary>
        /// Used to periodically update the value associated with the control. Optional.
        /// </summary>
        public virtual Func<TValue> CustomValueGetter { get; set; }

        protected TValue lastValue;
        protected bool controlUpdating;

        public TerminalValue()
        { }

        public override void Update()
        {
            if (Value != null && !Value.Equals(lastValue) && !controlUpdating)
            {
                controlUpdating = true;
                lastValue = Value;
                ControlChanged?.Invoke(this, EventArgs.Empty);
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