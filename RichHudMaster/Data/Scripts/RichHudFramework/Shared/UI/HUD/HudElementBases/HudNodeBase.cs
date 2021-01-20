using System;
using System.Collections.Generic;
using VRage;
using VRageMath;
using ApiMemberAccessor = System.Func<object, int, object>;
using HudSpaceDelegate = System.Func<VRage.MyTuple<bool, float, VRageMath.MatrixD>>;

namespace RichHudFramework
{
    namespace UI
    {
        using Client;
        using Server;
        using VRage.Game.ObjectBuilders;
        using HudUpdateAccessors = MyTuple<
            ApiMemberAccessor,
            MyTuple<Func<ushort>, Func<Vector3D>>, // ZOffset + GetOrigin
            Action, // DepthTest
            Action, // HandleInput
            Action<bool>, // BeforeLayout
            Action // BeforeDraw
        >;

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
            public override bool Visible => _visible && parentVisible && _registered;

            /// <summary>
            /// Determines whether the UI element will be drawn in the Back, Mid or Foreground
            /// </summary>
            public sealed override sbyte ZOffset
            {
                get { return (sbyte)(_zOffset + parentZOffset); }
                set { _zOffset = (sbyte)(value - parentZOffset); }
            }

            /// <summary>
            /// Scales the size and offset of an element. Any offset or size set at a given
            /// be increased or decreased with scale. Defaults to 1f. Includes parent scale.
            /// </summary>
            public sealed override float Scale => LocalScale * parentScale;

            /// <summary>
            /// Element scaling without parent scaling.
            /// </summary>
            public virtual float LocalScale { get; set; }

            /// <summary>
            /// Indicates whether or not the element has been registered to a parent.
            /// </summary>
            public bool Registered => _registered;

            protected HudParentBase _parent, reregParent;
            protected float parentScale;
            protected bool parentVisible, wasFastUnregistered;
            protected sbyte parentZOffset;

            public HudNodeBase(HudParentBase parent)
            {
                parentScale = 1f;
                LocalScale = 1f;
                parentVisible = true;
                _registered = false;

                Register(parent);
            }

            /// <summary>
            /// Updates layout for the element and its children. Overriding this method is rarely necessary. 
            /// If you need to update layout, use Layout().
            /// </summary>
            protected override void BeginLayout(bool refresh)
            {
                fullZOffset = GetFullZOffset(this, _parent);

                if (_parent == null)
                {
                    parentVisible = false;
                }
                else
                {
                    parentVisible = _parent.Visible;
                    parentScale = _parent.Scale;
                    parentZOffset = _parent.ZOffset;
                }

                if (Visible || refresh)
                {
                    Layout();
                }
            }

            /// <summary>
            /// Adds update delegates for members in the order dictated by the UI tree
            /// </summary>
            public override void GetUpdateAccessors(List<HudUpdateAccessors> UpdateActions, byte treeDepth)
            {
                HudSpace = _parent?.HudSpace ?? reregParent?.HudSpace;
                fullZOffset = GetFullZOffset(this, _parent);

                UpdateActions.EnsureCapacity(UpdateActions.Count + children.Count + 1);
                var accessors = new HudUpdateAccessors()
                {
                    Item1 = GetOrSetMemberFunc,
                    Item2 = new MyTuple<Func<ushort>, Func<Vector3D>>(GetZOffsetFunc, HudSpace.GetNodeOriginFunc),
                    Item3 = DepthTestAction,
                    Item4 = InputAction,
                    Item5 = LayoutAction,
                    Item6 = DrawAction
                };

                UpdateActions.Add(accessors);
                treeDepth++;

                for (int n = 0; n < children.Count; n++)
                    children[n].GetUpdateAccessors(UpdateActions, treeDepth);
            }

            /// <summary>
            /// Registers the element to the given parent object.
            /// </summary>
            /// <param name="preregister">Adds the element to the update tree without registering.</param>
            public virtual bool Register(HudParentBase newParent, bool preregister = false)
            {
                if (newParent == this)
                    throw new Exception("Types of HudNodeBase cannot be parented to themselves!");

                // Complete unregistration from previous parent if being registered to a different node
                if (wasFastUnregistered && newParent != reregParent)
                {
                    reregParent.RemoveChild(this);
                    wasFastUnregistered = false;
                    reregParent = null;
                }

                if (newParent != null && (reregParent == null || wasFastUnregistered))
                {
                    reregParent = null;

                    if (wasFastUnregistered)
                    {
                        Parent = newParent;
                        _registered = true;
                    }
                    else
                    {
                        Parent = newParent;
                        _registered = _parent.RegisterChild(this);
                    }

                    if (_registered)
                    {
                        if (!wasFastUnregistered)
                            HudMain.RefreshDrawList = true;

                        if (preregister)
                        {
                            reregParent = newParent;
                            Parent = null;
                            _registered = false;
                            wasFastUnregistered = true;
                        }
                        else
                        {
                            parentZOffset = _parent.ZOffset;
                            parentScale = _parent.Scale;
                            parentVisible = _parent.Visible;
                            wasFastUnregistered = false;
                        }

                        return true;
                    }
                    else
                        return false;
                }
                else
                    return false;
            }

            /// <summary>
            /// Unregisters the element from its parent, if it has one.
            /// </summary>
            /// <param name="fast">Prevents registration from triggering a draw list
            /// update. Meant to be used in conjunction with pooled elements being
            /// unregistered/reregistered to the same parent.</param>
            public virtual bool Unregister(bool fast = false)
            {
                if (Parent != null || (wasFastUnregistered && !fast))
                {
                    reregParent = _parent;
                    Parent = null;

                    if (!fast)
                    {
                        _registered = !reregParent.RemoveChild(this, false);

                        if (_registered)
                            Parent = reregParent;
                        else
                            HudMain.RefreshDrawList = true;

                        reregParent = null;
                    }
                    else
                    {
                        _registered = false;
                        wasFastUnregistered = true;
                    }

                    parentZOffset = 0;
                    parentVisible = false;
                }

                return !_registered;
            }

            /// <summary>
            /// Collection of utilities used internally to manage bulk element registration/unregistration
            /// </summary>
            protected static class NodeUtils
            {
                /// <summary>
                /// Used internally quickly register a list of child nodes to a parent.
                /// </summary>
                public static void RegisterNodes(HudParentBase newParent, List<HudNodeBase> children, IReadOnlyList<HudNodeBase> nodes, bool preregister)
                {
                    children.EnsureCapacity(children.Count + nodes.Count);

                    for (int n = 0; n < nodes.Count; n++)
                    {
                        HudNodeBase node = nodes[n];

                        if (node.wasFastUnregistered && newParent != node.reregParent)
                        {
                            node.reregParent.RemoveChild(node);
                            node.wasFastUnregistered = false;
                            node.reregParent = null;
                        }

                        if (preregister)
                        {
                            node.reregParent = newParent;
                            node.Parent = null;
                            node._registered = false;
                        }
                        else
                        {
                            node.parentZOffset = newParent.ZOffset;
                            node.parentScale = newParent.Scale;
                            node.parentVisible = newParent.Visible;
                        }

                        if (!node.wasFastUnregistered)
                        {
                            HudMain.RefreshDrawList = true;
                            children.Add(node);
                        }

                        node.wasFastUnregistered = preregister;
                    }
                }

                /// <summary>
                /// Used internally quickly register a list of child nodes to a parent.
                /// </summary>
                public static void RegisterNodes<TCon, TNode>(HudParentBase newParent, List<HudNodeBase> children, IReadOnlyList<TCon> nodes, bool preregister)
                    where TCon : IHudElementContainer<TNode>, new()
                    where TNode : HudNodeBase
                {
                    children.EnsureCapacity(children.Count + nodes.Count);

                    for (int n = 0; n < nodes.Count; n++)
                    {
                        HudNodeBase node = nodes[n].Element;

                        if (node.wasFastUnregistered && newParent != node.reregParent)
                        {
                            node.reregParent.RemoveChild(node);
                            node.wasFastUnregistered = false;
                            node.reregParent = null;
                        }

                        if (preregister)
                        {
                            node.reregParent = newParent;
                            node.Parent = null;
                            node._registered = false;
                        }
                        else
                        {
                            node.parentZOffset = newParent.ZOffset;
                            node.parentScale = newParent.Scale;
                            node.parentVisible = newParent.Visible;
                        }

                        if (!node.wasFastUnregistered)
                        {
                            HudMain.RefreshDrawList = true;
                            children.Add(node);
                        }

                        node.wasFastUnregistered = preregister;
                    }
                }

                /// <summary>
                /// Used internally to quickly unregister child nodes from their parent. Removes the range of nodes
                /// specified in the node list from the child list.
                /// </summary>
                public static void UnregisterNodes(List<HudNodeBase> children, IReadOnlyList<HudNodeBase> nodes, int index, int count, bool fast)
                {
                    int conEnd = index + count - 1;

                    if (!(index >= 0 && count >= 0 && index < nodes.Count && conEnd <= nodes.Count))
                        throw new Exception("Specified indices are out of range.");

                    if (!fast)
                    {
                        for (int i = index; i <= conEnd; i++)
                        {
                            int start = 0;

                            while (start < children.Count && children[start] != nodes[i])
                                start++;

                            int j = start, end = start;

                            while (j < children.Count && i <= conEnd && children[j] == nodes[i])
                            {
                                end = j;
                                i++;
                                j++;
                            }

                            children.RemoveRange(start, end - start);
                        }

                        HudMain.RefreshDrawList = true;
                    }

                    for (int n = index; n < count; n++)
                    {
                        if (fast)
                        {
                            nodes[n].reregParent = nodes[n]._parent;
                            nodes[n].wasFastUnregistered = true;
                        }
                        else
                        {
                            nodes[n].reregParent = null;
                            nodes[n].wasFastUnregistered = true;
                        }

                        nodes[n].Parent = null;
                        nodes[n]._registered = false;
                        nodes[n].parentZOffset = 0;
                        nodes[n].parentVisible = false;
                    }
                }

                /// <summary>
                /// Used internally to quickly unregister child nodes from their parent. Removes the range of nodes
                /// specified in the node list from the child list.
                /// </summary>
                public static void UnregisterNodes<TCon, TNode>(List<HudNodeBase> children, IReadOnlyList<TCon> nodes, int index, int count, bool fast)
                    where TCon : IHudElementContainer<TNode>, new()
                    where TNode : HudNodeBase
                {
                    int conEnd = index + count - 1;

                    if (!(index >= 0 && count >= 0 && index < nodes.Count && conEnd <= nodes.Count))
                        throw new Exception("Specified indices are out of range.");

                    if (!fast)
                    {
                        for (int i = index; i <= conEnd; i++)
                        {
                            int start = 0;

                            while (start < children.Count && children[start] != nodes[i].Element)
                                start++;

                            int j = start, end = start;

                            while (j < children.Count && i <= conEnd && children[j] == nodes[i].Element)
                            {
                                end = j;
                                i++;
                                j++;
                            }

                            children.RemoveRange(start, end - start);
                        }

                        HudMain.RefreshDrawList = true;
                    }

                    for (int n = index; n < count; n++)
                    {
                        if (fast)
                        {
                            nodes[n].Element.reregParent = nodes[n].Element._parent;
                            nodes[n].Element.wasFastUnregistered = true;
                        }
                        else
                        {
                            nodes[n].Element.reregParent = null;
                            nodes[n].Element.wasFastUnregistered = true;
                        }

                        nodes[n].Element.Parent = null;
                        nodes[n].Element._registered = false;
                        nodes[n].Element.parentZOffset = 0;
                        nodes[n].Element.parentVisible = false;
                    }
                }
            }
        }
    }
}