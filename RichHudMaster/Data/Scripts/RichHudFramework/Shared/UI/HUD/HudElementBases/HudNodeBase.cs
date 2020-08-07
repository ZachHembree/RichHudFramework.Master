using System;
using VRage;
using VRageMath;
using ApiMemberAccessor = System.Func<object, int, object>;

namespace RichHudFramework
{
    namespace UI
    {
        /// <summary>
        /// Base class for hud elements that can be parented to other elements.
        /// </summary>
        public abstract class HudNodeBase : HudParentBase, IReadOnlyHudNode
        {
            /// <summary>
            /// Read-only parent object of the node.
            /// </summary>
            IReadOnlyHudParent IReadOnlyHudNode.Parent => _parent;

            /// <summary>
            /// Parent object of the node.
            /// </summary>
            public virtual HudParentBase Parent { get { return _parent; } protected set { _parent = value; } }

            /// <summary>
            /// Determines whether or not an element will be drawn or process input. Visible by default.
            /// </summary>
            public override bool Visible 
            { 
                get { return _visible && parentVisible; } 
                set { _visible = value; } 
            }

            /// <summary>
            /// Determines whether the UI element will be drawn in the Back, Mid or Foreground
            /// </summary>
            public override int ZOffset
            {
                get { return _zOffset + parentZOffset; }
                set { _zOffset = value - parentZOffset; }
            }

            /// <summary>
            /// Scales the size and offset of an element. Any offset or size set at a given
            /// be increased or decreased with scale. Defaults to 1f. Includes parent scale.
            /// </summary>
            public override float Scale
            {
                get { return localScale * parentScale; }
                set { localScale = value / parentScale; }
            }

            /// <summary>
            /// Indicates whether or not the element has been registered to a parent.
            /// </summary>
            public bool Registered { get; private set; }

            protected HudParentBase _parent;
            protected float localScale, parentScale;
            protected bool _visible, parentVisible;
            protected int _zOffset, parentZOffset;

            public HudNodeBase(HudParentBase parent)
            {
                parentScale = 1f;
                localScale = 1f;

                Register(parent);
            }

            protected override bool BeginLayout(bool refresh)
            {
                if (Visible)
                {
                    parentScale = _parent == null ? 1f : _parent.Scale;
                    Layout();
                }

                return refresh;
            }

            protected override object BeginDraw(object matrix)
            {
                if (Visible)
                    Draw(matrix);

                if (_parent == null)
                {
                    parentVisible = true;
                    parentZOffset = 0;
                }
                else
                {
                    parentVisible = _parent.Visible;
                    parentZOffset = _parent.ZOffset;
                }

                return matrix;
            }

            /// <summary>
            /// Moves the element to the end of its parent's update list in order to ensure
            /// that it's drawn/updated last.
            /// </summary>
            public void GetFocus() =>
                _parent?.SetFocus(this);

            /// <summary>
            /// Registers the element to the given parent object.
            /// </summary>
            public virtual void Register(HudParentBase parent)
            {
                if (parent != null && parent == this)
                    throw new Exception("Types of HudNodeBase cannot be parented to themselves!");

                if (parent != null && _parent == null)
                {
                    Parent = parent;
                    _parent.RegisterChild(this);

                    parentZOffset = _parent.ZOffset;
                    parentScale = _parent.Scale;
                    parentVisible = _parent.Visible;

                    Registered = true;
                }
            }

            /// <summary>
            /// Unregisters the element from its parent, if it has one.
            /// </summary>
            public virtual void Unregister()
            {
                if (Parent != null)
                {
                    HudParentBase lastParent = _parent;

                    Parent = null;
                    lastParent.RemoveChild(this);

                    Registered = false;
                }

                parentZOffset = 0;
                parentScale = 1f;
                parentVisible = true;
            }
        }
    }
}