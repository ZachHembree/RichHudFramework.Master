using Microsoft.Xml.Serialization.GeneratedAssembly;
using System;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using VRage;
using VRageMath;

namespace RichHudFramework
{
    namespace UI
    {
        /// <summary>
        /// Used to control sizing behavior of HudChain members and the containing chain element itself. The align axis
        /// is the axis chain elements are arranged on; the off axis is the other axis. When vertically aligned, Y is 
        /// the align axis and X is the off axis. Otherwise, it's reversed.
        /// </summary>
        public enum HudChainSizingModes : int
        {
            // Naming: [Clamp/Fit]Members[OffAxis/AlignAxis/Both]
            // Fit > Clamp

            /// <summary>
            /// If this flag is set, then member size along the off axis will be allowed to vary freely, provided they
            /// fit inside the chain. If they don't fit, they will be clamped.
            /// </summary>
            ClampMembersOffAxis = 0x1,

            /// <summary>
            /// If this flag is set, then member size along the align axis will be allowed to vary freely, provided they
            /// fit inside the chain. If they don't fit, they will be clamped.
            /// </summary>
            ClampMembersAlignAxis = 0x2,

            /// <summary>
            /// In this mode member size can vary freely in both dimensions, provided they fit within the bounds of the
            /// chain. If they don't fit, they will be clamped.
            /// </summary>
            ClampMembersBoth = ClampMembersOffAxis | ClampMembersAlignAxis,

            /// <summary>
            /// If this flag is set, member size will be set to be equal to the size of the chain on the off axis, less
            /// padding.
            /// </summary>
            FitMembersOffAxis = 0x4,

            /// <summary>
            /// If this flag is set, member size will be set to be proportional to the size of the chain on the align axis.
            /// By default, each member is weighted equally, but the proportions can be defined on registration.
            /// </summary>
            FitMembersAlignAxis = 0x8,

            /// <summary>
            /// If this flag is set, member size will be set to be proportional to the size of the chain on the align axis,
            /// and equal to the chain size on the off axis.
            /// By default, each member is weighted equally, but the proportions can be defined on registration.
            /// </summary>
            FitMembersBoth = FitMembersOffAxis | FitMembersAlignAxis,

            AlignChainStart = 0x10,

            AlignChainEnd = 0x20,

            AlignChainCenter = 0x40
        }

        /// <summary>
        /// HUD element used to organize other elements into straight lines, either horizontal or vertical.
        /// </summary>
        /*
         Rules:
            1) Chain members must fit inside the chain. How this is accomplished depends on the sizing mode. Chain size
            and position is determined before Layout by parent nodes or on initialization. The chain resizes and positions
            its children, not itself.
            2) Members must be positioned within the chain's bounds.
            3) Members are assumed to be compatible with the specified sizing mode. Otherwise the behavior is undefined
            and incorrect positioning and sizing will occur.
        */
        public class HudChain<TElementContainer, TElement> : HudCollection<TElementContainer, TElement>
            where TElementContainer : IChainElementContainer<TElement>, new()
            where TElement : HudElementBase
        {
            protected const HudElementStates nodeSetVisible = HudElementStates.IsVisible | HudElementStates.IsRegistered;

            /// <summary>
            /// Used to allow the addition of child elements using collection-initializer syntax in
            /// conjunction with normal initializers.
            /// </summary>
            public new HudChain<TElementContainer, TElement> CollectionContainer => this;

            /// <summary>
            /// Distance between chain elements along their axis of alignment.
            /// </summary>
            public float Spacing { get; set; }

            /// <summary>
            /// Determines how/if the chain will attempt to resize member elements. Default sizing mode is 
            /// HudChainSizingModes.FitChainBoth.
            /// </summary>
            public HudChainSizingModes SizingMode { get; set; }

            /// <summary>
            /// Determines whether or not chain elements will be aligned vertically.
            /// </summary>
            public virtual bool AlignVertical 
            { 
                get { return _alignVertical; }
                set 
                {
                    if (value)
                    {
                        alignAxis = 1;
                        offAxis = 0;
                    }
                    else
                    {
                        alignAxis = 0;
                        offAxis = 1;
                    }

                    _alignVertical = value;
                }
            }

            protected bool _alignVertical;
            protected int alignAxis, offAxis;

            public HudChain(bool alignVertical, HudParentBase parent = null) : base(parent)
            {
                Init();

                Spacing = 0f;
                SizingMode = HudChainSizingModes.FitMembersBoth;
                AlignVertical = alignVertical;
            }

            public HudChain(HudParentBase parent) : this(false, parent)
            { }

            public HudChain() : this(false, null)
            { }

            /// <summary>
            /// Adds a UI element to the end of the chain.
            /// </summary>
            /// <param name="alignAxisScale">Scale of the element relative to the chain along the align axis, less padding and space
            /// required for other chain members. 0f == constant size; 1f = auto (default)</param>
            /// <param name="preload"></param>
            public virtual void Add(TElement element, float alignAxisScale, bool preload = false)
            {
                var newContainer = new TElementContainer();
                newContainer.SetElement(element);
                newContainer.AlignAxisScale = alignAxisScale;
                Add(newContainer, preload);
            }

            /// <summary>
            /// Initialzer called before the constructor.
            /// </summary>
            protected virtual void Init() { }

            protected override void Layout()
            {
                Vector2 chainSize = cachedSize - cachedPadding;

                if (hudCollectionList.Count > 0 && (chainSize.X > 0f && chainSize.Y > 0f))
                {
                    float elementSpanLength;
                    int visCount;

                    if (TryGetVisibleRange(chainSize[alignAxis], chainSize[offAxis], out visCount, out elementSpanLength))
                    {
                        // Find the start and end points of the span within the chain element
                        Vector2 startOffset = Vector2.Zero,
                            endOffset = Vector2.Zero;
                        float totalSpacing = Spacing * (visCount - 1f), 
                            rcpSpanLength = 1f / Math.Max(elementSpanLength, 1E-6f);

                        elementSpanLength = Math.Min(elementSpanLength + totalSpacing, chainSize[alignAxis]);

                        if (alignAxis == 1) // Vertical
                        {
                            startOffset.Y = .5f * chainSize.Y;
                            endOffset.Y = startOffset.Y - elementSpanLength;
                        }
                        else
                        {
                            startOffset.X = -.5f * chainSize.X;
                            endOffset.X = startOffset.X + elementSpanLength;
                        }

                        UpdateMemberOffsets(startOffset, endOffset, rcpSpanLength);
                    }
                }
            }

            /// <summary>
            /// Finds the total number of elements visible as well as the total length of the span along the align axis.
            /// Returns false if no elements are visible.
            /// </summary>
            protected virtual bool TryGetVisibleRange(float alignAxisSize, float offAxisSize, out int visCount, out float elementSpanLength)
            {
                float rcpTotalScale = 0f;

                visCount = 0;
                elementSpanLength = 0f;

                for (int i = 0; i < hudCollectionList.Count; i++)
                {
                    TElementContainer container = hudCollectionList[i];

                    if ((container.Element.State & HudElementStates.IsVisible) > 0)
                    {
                        rcpTotalScale += container.AlignAxisScale;
                        visCount++;
                    }
                }

                rcpTotalScale /= Math.Max(rcpTotalScale, 1f);

                if (visCount > 0)
                {
                    float totalSpacing = Spacing * (visCount - 1f), 
                        availableLength = Math.Max(alignAxisSize - Spacing * totalSpacing, 0f);

                    for (int i = 0; i < hudCollectionList.Count; i++)
                    {
                        TElementContainer container = hudCollectionList[i];

                        if ((container.Element.State & HudElementStates.IsVisible) > 0)
                        {
                            Vector2 size = container.Element.Size;

                            if (container.AlignAxisScale != 0f)
                            {
                                float effectiveScale = container.AlignAxisScale * rcpTotalScale;
                                size[alignAxis] = availableLength * effectiveScale;
                            }

                            // Update off axis size
                            if ((SizingMode & HudChainSizingModes.FitMembersOffAxis) > 0)
                                size[offAxis] = offAxisSize;
                            else if ((SizingMode & HudChainSizingModes.ClampMembersOffAxis) > 0)
                                size[offAxis] = Math.Min(size[offAxis], offAxisSize);

                            elementSpanLength += size[alignAxis];
                            container.Element.Size = size;
                        }
                    }

                    return true;
                }
                else
                    return false;
            }

            /// <summary>
            /// Arrange chain members in a straight line
            /// </summary>
            protected void UpdateMemberOffsets(Vector2 startOffset, Vector2 endOffset, float rcpSpanLength)
            {
                ParentAlignments left = (ParentAlignments)((int)ParentAlignments.Left * (2 - alignAxis)),
                    right = (ParentAlignments)((int)ParentAlignments.Right * (2 - alignAxis)),
                    bitmask = left | right;
                float j = 0f;

                for (int i = 0; i < hudCollectionList.Count; i++)
                {
                    TElementContainer container = hudCollectionList[i];
                    TElement element = container.Element;

                    if ((element.State & HudElementStates.IsVisible) > 0)
                    {
                        // Enforce alignment restrictions
                        element.ParentAlignment &= bitmask;
                        element.ParentAlignment |= ParentAlignments.Inner | ParentAlignments.UsePadding;

                        float increment = element.Size[alignAxis] * rcpSpanLength;
                        element.Offset = Vector2.Lerp(startOffset, endOffset, j + (.5f * increment));
                        j += increment;
                    }
                }
            }
        }

        /// <summary>
        /// HUD element used to organize other elements into straight lines, either horizontal or vertical. Min/Max size
        /// determines the minimum and maximum size of chain members.
        /// </summary>
        public class HudChain<TElementContainer> : HudChain<TElementContainer, HudElementBase>
            where TElementContainer : IChainElementContainer<HudElementBase>, new()
        {
            public HudChain(bool alignVertical, HudParentBase parent = null) : base(alignVertical, parent)
            { }

            public HudChain(HudParentBase parent) : base(true, parent)
            { }
        }

        /// <summary>
        /// HUD element used to organize other elements into straight lines, either horizontal or vertical. Min/Max size
        /// determines the minimum and maximum size of chain members.
        /// </summary>
        public class HudChain : HudChain<HudElementContainer<HudElementBase>, HudElementBase>
        {
            public HudChain(bool alignVertical, HudParentBase parent = null) : base(alignVertical, parent)
            { }

            public HudChain(HudParentBase parent) : base(true, parent)
            { }
        }
    }
}
