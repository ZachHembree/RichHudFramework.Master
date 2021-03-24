using System;
using System.Text;
using VRage;
using VRageMath;
using System.Collections.Generic;
using RichHudFramework.UI.Rendering;
using GlyphFormatMembers = VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>;
using ApiMemberAccessor = System.Func<object, int, object>;
using System.Collections;

namespace RichHudFramework.UI
{
    using CollectionData = MyTuple<Func<int, ApiMemberAccessor>, Func<int>>;
    using RichStringMembers = MyTuple<StringBuilder, GlyphFormatMembers>;

    /// <summary>
    /// Generic SelectionBox using HudChain
    /// </summary>
    public class ChainSelectionBox<TElementContainer, TElement, TValue>
        : SelectionBox<HudChain<TElementContainer, TElement>, TElementContainer, TElement, TValue>
        where TElementContainer : class, IListBoxEntry<TElement, TValue>, new()
        where TElement : HudElementBase, ILabelElement
    { }

    /// <summary>
    /// Generic SelectionBox using ScrollBox
    /// </summary>
    public class ScrollSelectionBox<TElementContainer, TElement, TValue>
        : SelectionBox<ScrollBox<TElementContainer, TElement>, TElementContainer, TElement, TValue>
        where TElementContainer : class, IListBoxEntry<TElement, TValue>, new()
        where TElement : HudElementBase, ILabelElement
    { }

    /// <summary>
    /// Generic list of pooled, selectable entries of fixed size.
    /// </summary>
    public class SelectionBox<THudChain, TElementContainer, TElement, TValue> : SelectionBoxBase<THudChain, TElementContainer, TElement, TValue>
        where THudChain : HudChain<TElementContainer, TElement>, new()
        where TElementContainer : class, IListBoxEntry<TElement, TValue>, new()
        where TElement : HudElementBase, ILabelElement
    {
        /// <summary>
        /// Height of entries in the list.
        /// </summary>
        public float LineHeight
        {
            get { return hudChain.MemberMaxSize.Y; }
            set { hudChain.MemberMaxSize = new Vector2(hudChain.MemberMaxSize.X, value); }
        }

        public readonly BorderBox border;
        protected readonly ObjectPool<TElementContainer> entryPool;

        public SelectionBox(HudParentBase parent) : base(parent)
        {
            entryPool = new ObjectPool<TElementContainer>(GetNewEntry, ResetEntry);
            hudChain.SizingMode = HudChainSizingModes.FitMembersBoth | HudChainSizingModes.ClampChainOffAxis;

            border = new BorderBox(hudChain)
            {
                DimAlignment = DimAlignments.Both,
                Color = new Color(58, 68, 77),
                Thickness = 1f,
            };

            LineHeight = 28f;
        }

        public SelectionBox() : this(null)
        { }

        /// <summary>
        /// Adds a new member to the list box with the given name and associated
        /// object.
        /// </summary>
        public TElementContainer Add(RichText name, TValue assocMember, bool enabled = true)
        {
            TElementContainer entry = entryPool.Get();

            entry.Element.Text = name;
            entry.AssocMember = assocMember;
            entry.Enabled = enabled;
            hudChain.Add(entry);

            return entry;
        }

        /// <summary>
        /// Adds the given range of entries to the list box.
        /// </summary>
        public void AddRange(IReadOnlyList<MyTuple<RichText, TValue, bool>> entries)
        {
            for (int n = 0; n < entries.Count; n++)
            {
                TElementContainer entry = entryPool.Get();

                entry.Element.Text = entries[n].Item1;
                entry.AssocMember = entries[n].Item2;
                entry.Enabled = entries[n].Item3;
                hudChain.Add(entry);
            }
        }

        /// <summary>
        /// Inserts an entry at the given index.
        /// </summary>
        public void Insert(int index, RichText name, TValue assocMember, bool enabled = true)
        {
            TElementContainer entry = entryPool.Get();

            entry.Element.Text = name;
            entry.AssocMember = assocMember;
            entry.Enabled = enabled;
            hudChain.Insert(index, entry);
        }

        /// <summary>
        /// Removes the member at the given index from the list box.
        /// </summary>
        public void RemoveAt(int index)
        {
            TElementContainer entry = hudChain.Collection[index];
            hudChain.RemoveAt(index, true);
            entryPool.Return(entry);
        }

        /// <summary>
        /// Removes the member at the given index from the list box.
        /// </summary>
        public bool Remove(TElementContainer entry)
        {
            if (hudChain.Remove(entry, true))
            {
                entryPool.Return(entry);
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Removes the specified range of indices from the list box.
        /// </summary>
        public void RemoveRange(int index, int count)
        {
            for (int n = index; n < index + count; n++)
                entryPool.Return(hudChain.Collection[n]);

            hudChain.RemoveRange(index, count, true);
        }

        /// <summary>
        /// Removes all entries from the list box.
        /// </summary>
        public void ClearEntries()
        {
            for (int n = 0; n < hudChain.Collection.Count; n++)
                entryPool.Return(hudChain.Collection[n]);

            hudChain.Clear(true);
        }

        protected virtual TElementContainer GetNewEntry()
        {
            var entry = new TElementContainer();
            entry.Element.Format = Format;
            entry.Element.Padding = _memberPadding;
            entry.Element.ZOffset = 1;
            entry.Enabled = true;

            return entry;
        }

        protected virtual void ResetEntry(TElementContainer entry)
        {
            if (Selection == entry)
                listInput.ClearSelection();

            entry.Element.TextBoard.Clear();
            entry.AssocMember = default(TValue);
            entry.Enabled = true;
        }

        public virtual object GetOrSetMember(object data, int memberEnum)
        {
            var member = (ListBoxAccessors)memberEnum;

            switch (member)
            {
                case ListBoxAccessors.ListMembers:
                    return new CollectionData
                    (
                        x => hudChain.Collection[x].GetOrSetMember,
                        () => hudChain.Collection.Count
                     );
                case ListBoxAccessors.Add:
                    {
                        if (data is MyTuple<List<RichStringMembers>, TValue>)
                        {
                            var entryData = (MyTuple<List<RichStringMembers>, TValue>)data;
                            return (ApiMemberAccessor)Add(new RichText(entryData.Item1), entryData.Item2).GetOrSetMember;
                        }
                        else
                        {
                            var entryData = (MyTuple<IList<RichStringMembers>, TValue>)data;
                            var stringList = entryData.Item1 as List<RichStringMembers>;
                            return (ApiMemberAccessor)Add(new RichText(stringList), entryData.Item2).GetOrSetMember;
                        }
                    }
                case ListBoxAccessors.Selection:
                    {
                        if (data == null)
                            return Selection;
                        else
                            SetSelection(data as TElementContainer);

                        break;
                    }
                case ListBoxAccessors.SelectionIndex:
                    {
                        if (data == null)
                            return SelectionIndex;
                        else
                            SetSelectionAt((int)data); break;
                    }
                case ListBoxAccessors.SetSelectionAtData:
                    SetSelection((TValue)data); break;
                case ListBoxAccessors.Insert:
                    {
                        var entryData = (MyTuple<int, List<RichStringMembers>, TValue>)data;
                        Insert(entryData.Item1, new RichText(entryData.Item2), entryData.Item3);
                        break;
                    }
                case ListBoxAccessors.Remove:
                    return Remove(data as TElementContainer);
                case ListBoxAccessors.RemoveAt:
                    RemoveAt((int)data); break;
                case ListBoxAccessors.ClearEntries:
                    ClearEntries(); break;
            }

            return null;
        }
    }
}