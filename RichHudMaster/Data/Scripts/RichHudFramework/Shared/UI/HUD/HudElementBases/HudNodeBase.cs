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
        using Internal;
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
        public abstract partial class HudNodeBase : HudParentBase, IReadOnlyHudNode
        {
            protected const uint 
                nodeVisible = (uint)(HudElementStates.IsVisible | HudElementStates.WasParentVisible),
                nodeInputEnabled = (uint)(HudElementStates.IsInputEnabled | HudElementStates.WasParentInputEnabled);
            protected const int maxPreloadDepth = 5;

            /// <summary>
            /// Read-only parent object of the node.
            /// </summary>
            IReadOnlyHudParent IReadOnlyHudNode.Parent => _parent;

            /// <summary>
            /// Parent object of the node.
            /// </summary>
            public virtual HudParentBase Parent { get { return _parent; } protected set { _parent = value; } }

            /// <summary>
            /// Indicates whether or not the element has been registered to a parent.
            /// </summary>
            public bool Registered => (State[0] & (uint)HudElementStates.IsRegistered) > 0;

            protected HudParentBase _parent;

            public HudNodeBase(HudParentBase parent)
            {
                NodeVisibleMask[0] = nodeVisible;
                NodeInputMask[0] = nodeInputEnabled;
                State[0] = (uint)(HudElementStates.WasParentVisible | HudElementStates.IsInputEnabled | HudElementStates.IsVisible);

                Register(parent);
            }

            /// <summary>
            /// Starts input update in a try-catch block. Useful for manually updating UI elements.
            /// Exceptions are reported client-side. Do not override this unless you have a good reason for it.
            /// If you need to update input, use HandleInputCallback.
            /// </summary>
            public override void BeginInput()
            {
                if (!ExceptionHandler.ClientsPaused)
                {
                    try
                    {
						if (_parent != null && (_parent.State[0] & _parent.NodeInputMask[0]) == _parent.NodeInputMask[0])
							State[0] |= (uint)HudElementStates.WasParentInputEnabled;
						else
							State[0] &= ~(uint)HudElementStates.WasParentInputEnabled;

						bool isVisible = (State[0] & NodeVisibleMask[0]) == NodeVisibleMask[0],
                             isInputEnabled = (State[0] & NodeInputMask[0]) == NodeInputMask[0];

                        if (HandleInputCallback != null && isVisible && isInputEnabled)
                        {
                            Vector3 cursorPos = HudSpace.CursorPos;
                            HandleInputCallback(new Vector2(cursorPos.X, cursorPos.Y));
                        }
					}
                    catch (Exception e)
                    {
                        ExceptionHandler.ReportException(e);
                    }
                }
            }

            /// <summary>
            /// Updates layout for the element and its children. Overriding this method is rarely necessary. 
            /// If you need to update layout, use LayoutCallback.
            /// </summary>
            public override void BeginLayout(bool isArranging)
            {
				if (!ExceptionHandler.ClientsPaused)
                {
                    try
                    {
						bool isVisible = (State[0] & NodeVisibleMask[0]) == NodeVisibleMask[0];
                        State[0] &= ~(uint)HudElementStates.IsLayoutReady;

						if (isVisible)
                        {
							if (!isArranging)
                                UpdateSizeCallback?.Invoke();
							else
							{
								LayoutCallback?.Invoke();
                                State[0] |= (uint)HudElementStates.IsLayoutReady;
							}
						}

						// Parent visibility flags need to propagate in top-down order, meaning they can only be evaluated
						// during Layout/Arrange, but Layout should not run before UpdateSize. They need to be delayed.
						if (isArranging)
						{
							if (_parent != null && (_parent.State[0] & _parent.NodeVisibleMask[0]) == _parent.NodeVisibleMask[0])
								State[0] |= (uint)HudElementStates.WasParentVisible;
							else
								State[0] &= ~(uint)HudElementStates.WasParentVisible;

							layerData[2] = ParentUtils.GetFullZOffset(layerData, _parent);
						}
					}
                    catch (Exception e)
                    {
                        ExceptionHandler.ReportException(e);
                    }
                }
            }

            /// <summary>
            /// Adds update delegates for members in the order dictated by the UI tree
            /// </summary>
            public override void GetUpdateAccessors(List<HudUpdateAccessors> UpdateActions, byte preloadDepth)
            {
                var lastState = (HudElementStates)State[0];
                State[0] |= (uint)HudElementStates.WasParentVisible;

                if ((State[0] & (uint)HudElementStates.IsVisible) == 0 && (State[0] & (uint)HudElementStates.CanPreload) > 0)
                    preloadDepth++;

                if (preloadDepth < maxPreloadDepth && (State[0] & (uint)HudElementStates.CanPreload) > 0)
                    State[0] |= (uint)HudElementStates.IsVisible;

                if ((State[0] & NodeVisibleMask[0]) == NodeVisibleMask[0])
                {
					bool isInputEnabled = (State[0] & NodeInputMask[0]) == NodeInputMask[0],
						canUseCursor = isInputEnabled && (State[0] & (uint)HudElementStates.CanUseCursor) > 0;

					HudSpace = _parent?.HudSpace;
					layerData[2] = ParentUtils.GetFullZOffset(nodeDataRef[0].Item3, _parent);

					var accessors = new HudUpdateAccessors()
					{
						Item1 = nodeDataRef[0].Item4.Item1,
						Item2 = new MyTuple<Func<ushort>, Func<Vector3D>>(() => (ushort)layerData[2], HudSpace.GetNodeOriginFunc),
						Item3 = (InputDepthCallback != null && canUseCursor) ? nodeDataRef[0].Item4.Item2 : null,
						Item4 = nodeDataRef[0].Item4.Item3,
						Item5 = nodeDataRef[0].Item4.Item5,
						Item6 = DrawCallback != null ? nodeDataRef[0].Item4.Item6 : null
					};

					UpdateActions.EnsureCapacity(UpdateActions.Count + children.Count + 1);
                    UpdateActions.Add(accessors);

                    for (int n = 0; n < children.Count; n++)
                        children[n].GetUpdateAccessors(UpdateActions, preloadDepth);
                }

                State[0] = (uint)lastState;
            }

            /// <summary>
            /// Registers the element to the given parent object.
            /// </summary>
            /// <param name="canPreload">Indicates whether or not the element's accessors can be loaded into the update tree
            /// before the element is visible. Useful for preventing flicker in scrolling lists.</param>
            public virtual bool Register(HudParentBase newParent, bool canPreload = false)
            {
                if (newParent == this)
                    throw new Exception("Types of HudNodeBase cannot be parented to themselves!");

                if (newParent != null)
                {
                    Parent = newParent;

                    if (_parent.RegisterChild(this))
                        State[0] |= (uint)HudElementStates.IsRegistered;
                    else
                        State[0] &= ~(uint)HudElementStates.IsRegistered;
                }

                if ((State[0] & (uint)HudElementStates.IsRegistered) > 0)
                {
					State[0] &= ~(uint)HudElementStates.WasParentVisible;

					if (canPreload)
                        State[0] |= (uint)HudElementStates.CanPreload;
                    else
                        State[0] &= ~(uint)HudElementStates.CanPreload;

                    return true;
                }
                else
                    return false;
            }

            /// <summary>
            /// Unregisters the element from its parent, if it has one.
            /// </summary>
            public virtual bool Unregister()
            {
                if (Parent != null)
                {
                    HudParentBase lastParent = Parent;
                    Parent = null;

                    lastParent.RemoveChild(this);
                    State[0] &= (uint)~(HudElementStates.IsRegistered | HudElementStates.WasParentVisible);
                }

                return !((State[0] & (uint)HudElementStates.IsRegistered) > 0);
            }
        }
    }
}